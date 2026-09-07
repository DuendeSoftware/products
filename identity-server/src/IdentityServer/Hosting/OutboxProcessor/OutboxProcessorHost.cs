// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.OutboxProcessor;

/// <summary>
/// Background service that periodically processes outbox events for all registered
/// <see cref="IOutboxSubscriber"/> instances, dispatching each event to the appropriate
/// keyed <see cref="IOutboxSubscriberHandler"/> with retry/backoff semantics.
/// </summary>
internal sealed class OutboxProcessorHost(
    IStorageFactory storageFactory,
    IEnumerable<IOutboxSubscriber> subscribers,
    IServiceScopeFactory serviceScopeFactory,
    IdentityServerOptions options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessorHost> logger) : BackgroundService
{
    // Clamp bounds matching StoragePurgeHost conventions.
    private const int MinBatchSize = 1;
    private const int MaxBatchSize = 1000;
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);

    // In-memory retry state (per subscriber). Lost on restart is acceptable per design.
    private readonly Dictionary<string, RetryState> _retryStates = new();

    /// <inheritdoc />
    public override Task StartAsync(Ct ct) =>
        !options.OutboxProcessor.EnableProcessor
            ? Task.CompletedTask
            : base.StartAsync(ct);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(Ct stoppingToken)
    {
        logger.StartingProcessor(LogLevel.Debug);

        var interval = options.OutboxProcessor.ProcessInterval < MinInterval
            ? MinInterval
            : options.OutboxProcessor.ProcessInterval;

        var intervalSeconds = (int)interval.TotalSeconds;

        // Start the first run at a random interval.
        var delay = options.OutboxProcessor.FuzzStartup
#pragma warning disable CA5394 // Randomness for security does not apply here
            ? TimeSpan.FromSeconds(Random.Shared.Next(Math.Max(1, intervalSeconds)))
#pragma warning restore CA5394
            : interval;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.CancellationRequested(LogLevel.Debug);
                break;
            }
            catch (Exception ex)
            {
                logger.DelayException(LogLevel.Error, ex);
                break;
            }

            await RunProcessorAsync(stoppingToken);

            delay = interval;
        }

        logger.StoppingProcessor(LogLevel.Debug);
    }

    internal async Task RunProcessorAsync(Ct ct)
    {
        if (!options.OutboxProcessor.EnableProcessor)
        {
            return;
        }

        foreach (var subscriber in subscribers)
        {
            if (!subscriber.IsEnabled)
            {
                continue;
            }

            try
            {
                await ProcessSubscriberAsync(subscriber, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                logger.ProcessorCancelled(LogLevel.Debug);
                return;
            }
            catch (Exception ex)
            {
                logger.ProcessorException(LogLevel.Error, ex, subscriber.SubscriberName.Value);
            }
        }
    }

    private async Task ProcessSubscriberAsync(IOutboxSubscriber subscriber, Ct ct)
    {
        var subscriberName = subscriber.SubscriberName.Value;
        var retryState = GetRetryState(subscriberName);

        // If a retry is pending and the delay hasn't elapsed, skip this cycle.
        if (retryState.RetryAfterUtc is { } retryAfter && timeProvider.GetUtcNow() < retryAfter)
        {
            logger.RetryNotYetEligible(LogLevel.Debug, subscriberName, retryAfter);
            return;
        }

        var storage = await storageFactory.GetStorage(ct);
        var batchSize = Math.Clamp(options.OutboxProcessor.BatchSize, MinBatchSize, MaxBatchSize);
        var page = await storage.GetOutboxEventsForSubscriberAsync(subscriber.SubscriberName, batchSize, ct);

        if (page.Events.Count == 0)
        {
            // If we had pending retry state for a message that no longer exists
            // (e.g. deleted externally), clear the stale state.
            if (retryState.FailedMessageId is not null)
            {
                retryState = RetryState.Clear();
                SetRetryState(subscriberName, retryState);
            }

            return;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetKeyedService<IOutboxSubscriberHandler>(subscriberName);

        if (handler is null)
        {
            logger.HandlerNotFound(LogLevel.Warning, subscriberName);
            if (retryState.FailedMessageId is not null)
            {
                retryState = RetryState.Clear();
                SetRetryState(subscriberName, retryState);
            }

            return;
        }

        var processedIds = new List<OutboxEventId>();
        var stopBatch = false;

        foreach (var evt in page.Events)
        {
            ct.ThrowIfCancellationRequested();

            // Force-drop if this event has exceeded max retries.
            if (IsMaxRetriesExceeded(retryState, evt.MessageId))
            {
                logger.ForceDrop(LogLevel.Warning, evt.MessageId.Value, subscriberName, options.OutboxProcessor.MaxRetries);
                processedIds.Add(evt.MessageId);
                retryState = RetryState.Clear();
                SetRetryState(subscriberName, retryState);
                continue;
            }

            HandleOutcomeResult outcome;

#pragma warning disable CA1031 // Catch all handler exceptions as a failing handler must not crash the processor
            try
            {
                outcome = await handler.HandleAsync(evt, ct);
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException && ct.IsCancellationRequested)
                {
                    throw;
                }

                var attempts = retryState.FailedMessageId == evt.MessageId
                    ? retryState.FailedAttempts + 1
                    : 1;
                logger.HandlerException(LogLevel.Warning, ex, subscriberName, evt.MessageId.Value, attempts, options.OutboxProcessor.MaxRetries, ex.Message);
                outcome = HandleOutcomeResult.Retry(ex.Message);
            }
#pragma warning restore CA1031

            switch (outcome)
            {
                case HandleOutcomeResult.SuccessResult:
                    processedIds.Add(evt.MessageId);
                    if (retryState.FailedMessageId == evt.MessageId)
                    {
                        retryState = RetryState.Clear();
                        SetRetryState(subscriberName, retryState);
                    }
                    break;

                case HandleOutcomeResult.RetryResult retryResult:
                    var retryAttempts = retryState.FailedMessageId == evt.MessageId
                        ? retryState.FailedAttempts + 1
                        : 1;
                    var backoffDelay = ComputeDelay(retryAttempts);
                    var retryAfterUtc = timeProvider.GetUtcNow() + backoffDelay;
                    logger.HandlerRetry(LogLevel.Warning, subscriberName, evt.MessageId.Value, retryResult.Reason, retryAttempts, options.OutboxProcessor.MaxRetries, retryAfterUtc);
                    retryState = new RetryState(evt.MessageId, retryAttempts, retryAfterUtc);
                    SetRetryState(subscriberName, retryState);
                    // Stop processing batch on retry to preserve ordering.
                    stopBatch = true;
                    break;

                case HandleOutcomeResult.DropResult dropResult:
                    logger.HandlerDrop(LogLevel.Warning, subscriberName, evt.MessageId.Value, dropResult.Reason);
                    processedIds.Add(evt.MessageId);
                    if (retryState.FailedMessageId == evt.MessageId)
                    {
                        retryState = RetryState.Clear();
                        SetRetryState(subscriberName, retryState);
                    }
                    break;
            }

            if (stopBatch)
            {
                break;
            }
        }

        if (processedIds.Count > 0)
        {
            await storage.DeleteOutboxEventsAsync(processedIds, ct);
        }

        logger.BatchProcessed(LogLevel.Debug, processedIds.Count, subscriberName);
    }

    private bool IsMaxRetriesExceeded(RetryState state, OutboxEventId messageId) =>
        state.FailedMessageId == messageId && state.FailedAttempts >= options.OutboxProcessor.MaxRetries;

    private TimeSpan ComputeDelay(int attempt)
    {
        var multiplier = Math.Pow(options.OutboxProcessor.RetryBackoffMultiplier, attempt - 1);
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier) || multiplier <= 0)
        {
            return options.OutboxProcessor.RetryDelay >= TimeSpan.Zero
                ? options.OutboxProcessor.RetryDelay
                : options.OutboxProcessor.MaxRetryDelay;
        }

        var ticks = options.OutboxProcessor.RetryDelay.Ticks * multiplier;
        if (ticks is > long.MaxValue or < 0)
        {
            return options.OutboxProcessor.MaxRetryDelay;
        }

        var delay = TimeSpan.FromTicks((long)ticks);
        return delay > options.OutboxProcessor.MaxRetryDelay ? options.OutboxProcessor.MaxRetryDelay : delay;
    }

    private RetryState GetRetryState(string subscriberName) =>
        _retryStates.TryGetValue(subscriberName, out var state) ? state : RetryState.None;

    private void SetRetryState(string subscriberName, RetryState state) =>
        _retryStates[subscriberName] = state;

    private sealed record RetryState(OutboxEventId? FailedMessageId, int FailedAttempts, DateTimeOffset? RetryAfterUtc)
    {
        public static readonly RetryState None = new(null, 0, null);

        public static RetryState Clear() => None;
    }
}
