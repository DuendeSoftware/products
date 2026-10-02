// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// Processes outbox events for all registered <see cref="IOutboxSubscription"/> instances,
/// dispatching each event to the appropriate keyed <see cref="IOutboxSubscriptionHandler"/>
/// with retry/backoff semantics. Holds no dependency on any product-specific type; the
/// caller supplies the storage key and processing knobs through <see cref="OutboxProcessorOptions"/>.
/// </summary>
internal sealed class OutboxProcessor(
    ICrossPartitionStorageFactory crossPartitionStorageFactory,
    IStorageInstanceRouter storageInstanceRouter,
    IEnumerable<IOutboxSubscription> subscriptions,
    IServiceScopeFactory serviceScopeFactory,
    IAmbientOutboxProcessingContextProvider ambientContextProvider,
    IOptions<OutboxProcessorOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger)
{
    private readonly OutboxProcessorOptions _options = options.Value;

    private const int MinBatchSize = 1;
    private const int MaxBatchSize = 1000;

    // In-memory retry state (per storage instance, per subscription). Lost on restart is
    // acceptable per design. Keyed by instance as well as subscription so that a failing
    // event on one storage instance does not affect retry state for the same subscription
    // on a different instance.
    private readonly Dictionary<(StorageInstanceId storageInstanceId, string SubscriberName), RetryState> _retryStates = new();

    /// <summary>
    /// Runs a single pass over all enabled subscriptions: fetches a batch, dispatches to the
    /// keyed handler, applies the Success/Retry/Drop outcome, and deletes processed events.
    /// </summary>
    internal async Task RunProcessorAsync(Ct ct)
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
        var retryState = GetRetryState(storageInstanceId, subscriptionName);

        // If a retry is pending and the delay hasn't elapsed, skip this cycle.
        if (retryState.RetryAfterUtc is { } retryAfter && timeProvider.GetUtcNow() < retryAfter)
        {
            logger.RetryNotYetEligible(LogLevel.Debug, subscriptionName, retryAfter);
            return;
        }

        var crossPartitionStorage = await crossPartitionStorageFactory.GetCrossPartitionStorageAsync(storageInstanceId, ct);
        var batchSize = Math.Clamp(_options.BatchSize, MinBatchSize, MaxBatchSize);
        var page = await crossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, batchSize, ct);

        if (page.Events.Count == 0)
        {
            // If we had pending retry state for a message that no longer exists
            // (e.g. deleted externally), clear the stale state.
            if (retryState.FailedMessageId is not null)
            {
                retryState = RetryState.Clear();
                SetRetryState(storageInstanceId, subscriptionName, retryState);
            }

            return;
        }

        // The message owning the retry slot may have been deleted externally (e.g. by another
        // process, or a manual cleanup) between cycles. If it's no longer present in this page,
        // the slot is stale and must be freed before processing the remaining events, otherwise
        // every other event in this batch stays blocked from ever claiming the slot.
        //
        // This can only be inferred safely from a *complete* page (HasMore is false): the page
        // is fetched in sequence order starting from the oldest pending event, but with a
        // BatchSize smaller than the subscription's full backlog, the owning message could simply
        // be further back in the queue than this batch reaches, still present in the store. On
        // a partial page (HasMore is true), absence proves nothing, so cleanup is deferred until
        // a cycle sees a complete page (or an empty one, handled above).
        if (!page.HasMore &&
            retryState.FailedMessageId is { } failedMessageId &&
            !page.Events.Any(e => e.MessageId == failedMessageId))
        {
            retryState = RetryState.Clear();
            SetRetryState(storageInstanceId, subscriptionName, retryState);
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetKeyedService<IOutboxSubscriptionHandler>(subscriptionName);

        if (handler is null)
        {
            logger.HandlerNotFound(LogLevel.Warning, subscriptionName);
            if (retryState.FailedMessageId is not null)
            {
                retryState = RetryState.Clear();
                SetRetryState(storageInstanceId, subscriptionName, retryState);
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
                logger.ForceDrop(LogLevel.Warning, evt.MessageId.Value, subscriptionName, _options.MaxRetries);
                processedIds.Add(evt.MessageId);
                retryState = RetryState.Clear();
                SetRetryState(storageInstanceId, subscriptionName, retryState);
                continue;
            }

            HandleOutcomeResult outcome;
#pragma warning disable CA1031 // A failing context establishment must not stop the batch or the processor
            try
            {
                outcome = await ambientContextProvider.ExecuteWithContextAsync(
                    evt,
                    innerCt => InvokeHandlerAsync(handler, evt, subscriptionName, retryState, innerCt),
                    ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Context establishment failed for this event (before the handler was ever
                // reached). Unlike a handler-level Retry, this must not stop the batch: other
                // events in this cycle are independent of this one's ambient context and
                // should still be attempted. The retry slot is per-subscription (not per-message),
                // shared with handler retries, so if a different message already owns it, this
                // failure is simply dropped for now and the event is left untouched until that
                // slot frees up (matching how two concurrently-failing messages are handled in
                // sequence rather than resetting each other's attempt counts).
                if (retryState.FailedMessageId is null || retryState.FailedMessageId == evt.MessageId)
                {
                    var attempts = NextAttemptCount(retryState, evt.MessageId);
                    var delay = ComputeDelay(attempts);
                    var retryAfterUtc = timeProvider.GetUtcNow() + delay;
                    logger.ContextEstablishmentFailed(LogLevel.Warning, ex, subscriptionName, evt.PoolId.Value, evt.MessageId.Value, attempts, _options.MaxRetries, retryAfterUtc);
                    retryState = new RetryState(evt.MessageId, attempts, retryAfterUtc);
                    SetRetryState(storageInstanceId, subscriptionName, retryState);
                }

                continue;
            }
#pragma warning restore CA1031

            switch (outcome)
            {
                case HandleOutcomeResult.SuccessResult:
                    processedIds.Add(evt.MessageId);
                    if (retryState.FailedMessageId == evt.MessageId)
                    {
                        retryState = RetryState.Clear();
                        SetRetryState(storageInstanceId, subscriptionName, retryState);
                    }
                    break;

                case HandleOutcomeResult.RetryResult retryResult:
                    // The retry slot is per-subscription, shared with context-establishment
                    // failures (see above). If a different message already owns it (e.g. an
                    // earlier event in this same page failed to establish its context), this
                    // event must not steal or reset that ownership: doing so would let a later,
                    // healthier message perpetually mask an earlier poisoned one from ever
                    // reaching MaxRetries and being force-dropped.
                    //
                    // Regardless of who owns the slot, this event itself was retained (it is
                    // never added to processedIds below), so the batch must still stop here:
                    // subscription ordering is defined by delivery order, and any event after this
                    // one must not be processed or deleted while this one remains undelivered.
                    if (retryState.FailedMessageId is null || retryState.FailedMessageId == evt.MessageId)
                    {
                        var retryAttempts = NextAttemptCount(retryState, evt.MessageId);
                        var backoffDelay = ComputeDelay(retryAttempts);
                        var retryAfterUtc = timeProvider.GetUtcNow() + backoffDelay;
                        logger.HandlerRetry(LogLevel.Warning, subscriptionName, evt.MessageId.Value, retryResult.Reason, retryAttempts, _options.MaxRetries, retryAfterUtc);
                        retryState = new RetryState(evt.MessageId, retryAttempts, retryAfterUtc);
                        SetRetryState(storageInstanceId, subscriptionName, retryState);
                    }

                    // Stop processing batch on retry to preserve ordering.
                    stopBatch = true;
                    break;

                case HandleOutcomeResult.DropResult dropResult:
                    logger.HandlerDrop(LogLevel.Warning, subscriptionName, evt.MessageId.Value, dropResult.Reason);
                    processedIds.Add(evt.MessageId);
                    if (retryState.FailedMessageId == evt.MessageId)
                    {
                        retryState = RetryState.Clear();
                        SetRetryState(storageInstanceId, subscriptionName, retryState);
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
            await crossPartitionStorage.DeleteOutboxEventsAsync(processedIds, ct);
        }

        logger.BatchProcessed(LogLevel.Debug, processedIds.Count, subscriptionName);
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
        RetryState retryState,
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

            var attempts = NextAttemptCount(retryState, evt.MessageId);
            logger.HandlerException(LogLevel.Warning, ex, subscriptionName, evt.MessageId.Value, attempts, _options.MaxRetries, ex.Message);
            return HandleOutcomeResult.Retry(ex.Message);
        }
    }
#pragma warning restore CA1031

    private static int NextAttemptCount(RetryState retryState, OutboxEventId messageId) =>
        retryState.FailedMessageId == messageId ? retryState.FailedAttempts + 1 : 1;

    private bool IsMaxRetriesExceeded(RetryState state, OutboxEventId messageId) =>
        state.FailedMessageId == messageId && state.FailedAttempts >= _options.MaxRetries;

    private TimeSpan ComputeDelay(int attempt)
    {
        var multiplier = Math.Pow(_options.RetryBackoffMultiplier, attempt - 1);
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier) || multiplier <= 0)
        {
            return _options.RetryDelay >= TimeSpan.Zero
                ? _options.RetryDelay
                : _options.MaxRetryDelay;
        }

        var ticks = _options.RetryDelay.Ticks * multiplier;
        if (ticks is > long.MaxValue or < 0)
        {
            return _options.MaxRetryDelay;
        }

        var delay = TimeSpan.FromTicks((long)ticks);
        return delay > _options.MaxRetryDelay ? _options.MaxRetryDelay : delay;
    }

    private RetryState GetRetryState(StorageInstanceId storageInstanceId, string subscriptionName) =>
        _retryStates.TryGetValue((storageInstanceId, subscriptionName), out var state) ? state : RetryState.None;

    private void SetRetryState(StorageInstanceId storageInstanceId, string subscriptionName, RetryState state) =>
        _retryStates[(storageInstanceId, subscriptionName)] = state;

    private sealed record RetryState(OutboxEventId? FailedMessageId, int FailedAttempts, DateTimeOffset? RetryAfterUtc)
    {
        public static readonly RetryState None = new(null, 0, null);

        public static RetryState Clear() => None;
    }
}
