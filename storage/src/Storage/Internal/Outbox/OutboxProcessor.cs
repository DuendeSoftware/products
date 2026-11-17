// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// Processes outbox events for all registered <see cref="IOutboxSubscription"/> instances,
/// dispatching each event to the appropriate keyed <see cref="IOutboxSubscriptionHandler"/>.
/// Every event in a page is attempted independently with no ordering guarantee; a failed
/// event is retried on the next pass, up to <see cref="OutboxProcessorOptions.MaxRetries"/>
/// times. Holds no dependency on any product-specific type; the caller supplies the storage
/// key and processing knobs through <see cref="OutboxProcessorOptions"/>.
/// </summary>
public sealed class OutboxProcessor(
    ICrossPartitionStorageFactory crossPartitionStorageFactory,
    IStorageInstanceRouter storageInstanceRouter,
    IEnumerable<IOutboxSubscription> subscriptions,
    IServiceScopeFactory serviceScopeFactory,
    IAmbientOutboxProcessingContextProvider ambientContextProvider,
    IOptions<OutboxProcessorOptions> options,
    ILogger<OutboxProcessor> logger)
{
    private readonly OutboxProcessorOptions _options = options.Value;

    private const int MinBatchSize = 1;
    private const int MaxBatchSize = 1000;

    // In-memory failed-attempt counts (per storage instance, per subscription). Lost on
    // restart is acceptable per design. Keyed by instance as well as subscription so that a
    // failing event on one storage instance does not affect attempt counts for the same
    // subscription on a different instance. Rebuilt from each page, so it holds at most
    // BatchSize entries.
    private readonly Dictionary<(StorageInstanceId StorageInstanceId, string SubscriberName), Dictionary<OutboxEventId, int>> _failedAttempts = new();

    /// <summary>
    /// Runs a single pass over all enabled subscriptions: fetches a batch, dispatches to the
    /// keyed handler, applies the Success/Retry/Drop outcome, and deletes processed events.
    /// </summary>
    public async Task RunProcessorAsync(Ct ct)
    {
        foreach (var subscription in subscriptions)
        {
            // Re-checked every cycle (not just once, e.g. via OutboxSubscriptions' own
            // construction-time filter): IOutboxSubscription.IsEnabled can be a computed
            // property backed by DI/feature-flag state that changes after startup, and
            // main's host re-evaluated it on every pass.
            if (!subscription.IsEnabled)
            {
                continue;
            }

            foreach (var storageInstanceId in storageInstanceRouter.GetAll())
            {
#pragma warning disable CA1031 // A failing storageInstanceId must not stop processing of the others
                try
                {
                    await ProcessSubscriptionAsync(subscription, storageInstanceId, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    logger.ProcessorCancelled(LogLevel.Debug);
                    return;
                }
                catch (Exception ex)
                {
                    logger.ProcessorException(LogLevel.Error, ex, subscription.SubscriberName.Value);
                }
#pragma warning restore CA1031
            }
        }
    }

    private async Task ProcessSubscriptionAsync(IOutboxSubscription subscription, StorageInstanceId storageInstanceId, Ct ct)
    {
        var subscriptionName = subscription.SubscriberName.Value;
        var key = (storageInstanceId, subscriptionName);

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetKeyedService<IOutboxSubscriptionHandler>(subscriptionName);

        if (handler is null)
        {
            logger.HandlerNotFound(LogLevel.Warning, subscriptionName);
            return;
        }

        var crossPartitionStorage = await crossPartitionStorageFactory.GetCrossPartitionStorageAsync(storageInstanceId, ct);
        var batchSize = Math.Clamp(_options.BatchSize, MinBatchSize, MaxBatchSize);
        var page = await crossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, batchSize, ct);

        if (page.Events.Count == 0)
        {
            _ = _failedAttempts.Remove(key);
            return;
        }

        // Keep only the counts of rows still in this page, and publish the map before handling
        // any event, so failures completed earlier in the page survive a later cancellation.
        var previous = _failedAttempts.GetValueOrDefault(key);
        var failedAttempts = new Dictionary<OutboxEventId, int>();
        if (previous is not null)
        {
            foreach (var evt in page.Events)
            {
                if (previous.TryGetValue(evt.MessageId, out var count))
                {
                    failedAttempts[evt.MessageId] = count;
                }
            }
        }

        _failedAttempts[key] = failedAttempts;

        var processedIds = new List<OutboxEventId>();
        var maxAttempts = _options.MaxRetries == int.MaxValue ? int.MaxValue : _options.MaxRetries + 1;

        foreach (var evt in page.Events)
        {
            ct.ThrowIfCancellationRequested();

            var failedSoFar = failedAttempts.GetValueOrDefault(evt.MessageId);

            // Attempts already exhausted in an earlier pass whose delete never completed (it was
            // cancelled or failed): delete the row without calling the handler again.
            if (failedSoFar > _options.MaxRetries)
            {
                processedIds.Add(evt.MessageId);
                continue;
            }

            var attempt = failedSoFar + 1;

            HandleOutcomeResult outcome;
#pragma warning disable CA1031 // A failing context establishment must not stop the batch or the processor
            try
            {
                outcome = await ambientContextProvider.ExecuteWithContextAsync(
                    evt,
                    innerCt => InvokeHandlerAsync(handler, evt, subscriptionName, attempt, maxAttempts, innerCt),
                    ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Context establishment failed for this event (before the handler was ever
                // reached). This must not stop the page: other events in this cycle are
                // independent of this one's ambient context and are still attempted.
                logger.ContextEstablishmentFailed(LogLevel.Warning, ex, subscriptionName, evt.PoolId.Value, evt.MessageId.Value, attempt, maxAttempts);
                RecordFailure(evt, attempt, subscriptionName, failedAttempts, processedIds);
                continue;
            }
#pragma warning restore CA1031

            switch (outcome)
            {
                case HandleOutcomeResult.SuccessResult:
                    processedIds.Add(evt.MessageId);
                    break;

                case HandleOutcomeResult.RetryResult retryResult:
                    logger.HandlerRetry(LogLevel.Warning, subscriptionName, evt.MessageId.Value, retryResult.Reason, attempt, maxAttempts);
                    RecordFailure(evt, attempt, subscriptionName, failedAttempts, processedIds);
                    break;

                case HandleOutcomeResult.DropResult dropResult:
                    logger.HandlerDrop(LogLevel.Warning, subscriptionName, evt.MessageId.Value, dropResult.Reason);
                    processedIds.Add(evt.MessageId);
                    break;
            }
        }

        if (processedIds.Count > 0)
        {
            await crossPartitionStorage.DeleteOutboxEventsAsync(processedIds, ct);

            foreach (var id in processedIds)
            {
                _ = failedAttempts.Remove(id);
            }
        }

        if (failedAttempts.Count == 0)
        {
            _ = _failedAttempts.Remove(key);
        }

        logger.BatchProcessed(LogLevel.Debug, processedIds.Count, subscriptionName);
    }

    private void RecordFailure(
        PersistedOutboxEvent evt,
        int attempt,
        string subscriptionName,
        Dictionary<OutboxEventId, int> failedAttempts,
        List<OutboxEventId> processedIds)
    {
        // Recorded even when exhausted, so a later pass can still delete the row without calling
        // the handler again if this pass is cancelled before its delete.
        failedAttempts[evt.MessageId] = attempt;

        if (attempt > _options.MaxRetries)
        {
            logger.ForceDrop(LogLevel.Warning, evt.MessageId.Value, subscriptionName, attempt);
            processedIds.Add(evt.MessageId);
        }
    }

    // Invoked by the ambient context provider's ExecuteWithContextAsync while its established
    // context is active, so that a failure here is distinguishable (by the caller) from a context
    // establishment failure: exceptions from this method are converted to a Retry outcome and
    // never propagate, whereas an exception thrown by context establishment itself propagates out
    // of ExecuteWithContextAsync unmodified.
#pragma warning disable CA1031 // Catch all handler exceptions as a failing handler must not crash the processor
    private async Task<HandleOutcomeResult> InvokeHandlerAsync(
        IOutboxSubscriptionHandler handler,
        PersistedOutboxEvent evt,
        string subscriptionName,
        int attempt,
        int maxAttempts,
        Ct ct)
    {
        try
        {
            return await handler.HandleAsync(evt, ct);
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException && ct.IsCancellationRequested)
            {
                throw;
            }

            logger.HandlerException(LogLevel.Warning, ex, subscriptionName, evt.MessageId.Value, attempt, maxAttempts, ex.Message);
            return HandleOutcomeResult.Retry(ex.Message);
        }
    }
#pragma warning restore CA1031
}
