// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Outbox;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static Duende.Storage.Internal.OutboxProcessorTestHelpers;

namespace Duende.Storage.Internal;

/// <summary>
/// Unit tests for <see cref="OutboxProcessor"/> behaviour that real storage cannot show: the
/// per-page rebuild of the in-memory failure map, keeping completed failures when a pass is
/// cancelled mid-page, batch-size clamping, <c>MaxRetries = 0</c>,
/// the distinct context-establishment-failure log event, and skipping the read when no handler
/// is registered.
/// </summary>
public sealed class OutboxProcessorFailureTrackingTests
{
    private static readonly StorageInstanceId InstanceId = StorageInstanceId.Create("instance");
    private static readonly SubscriberName SubscriptionName = SubscriberName.Create("subscription");

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task event_missing_from_a_page_is_forgotten_and_restarts_its_attempt_count()
    {
        var subscription = new FakeSubscription(SubscriptionName);
        var evt = CreateEvent(SubscriptionName, poolId: 1);
        var storage = new FakeCrossPartitionStorage([evt]);

        var handler = new AlwaysRetryHandler();
        var options = new OutboxProcessorOptions { MaxRetries = 1 };
        var processor = CreateProcessor(subscription, storage, handler, options);

        // Pass 1: fails, attempt 1 recorded; one retry left before it would be force-dropped.
        await processor.RunProcessorAsync(_ct);
        storage.Events.ShouldNotBeEmpty();

        // The row is omitted from the next page (e.g. filtered out by another consumer),
        // without being removed from the fake storage's underlying collection: the page is
        // effectively empty, so the map entry for this (instance, subscription) is forgotten.
        storage.HideFromNextPage(evt.MessageId);
        await processor.RunProcessorAsync(_ct);

        // The row returns in a later page.
        storage.RestoreToNextPage(evt.MessageId);
        await processor.RunProcessorAsync(_ct); // fails again

        // If the map entry had not been forgotten, this would be attempt 2, force-dropped
        // (MaxRetries = 1). Because it was forgotten while the row was absent, this is
        // attempt 1 again, so the row is still queued.
        storage.Events.ShouldNotBeEmpty(
            "the map entry must be forgotten while the row is missing from a page, so its " +
            "attempt count restarts at 1 instead of being force-dropped as attempt 2");
    }

    [Theory]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(5000, 1000)]
    public async Task batch_size_is_clamped_between_1_and_1000(int configuredBatchSize, int expectedReadCount)
    {
        var subscription = new FakeSubscription(SubscriptionName);
        var storage = new FakeCrossPartitionStorage([]);
        var handler = new AlwaysRetryHandler();
        var options = new OutboxProcessorOptions { BatchSize = configuredBatchSize };
        var processor = CreateProcessor(subscription, storage, handler, options);

        await processor.RunProcessorAsync(_ct);

        storage.ReadCounts.ShouldBe([expectedReadCount]);
    }

    [Fact]
    public async Task cancellation_mid_page_keeps_failures_completed_earlier_in_the_page()
    {
        var subscription = new FakeSubscription(SubscriptionName);
        var a = CreateEvent(SubscriptionName, poolId: 1);
        var b = CreateEvent(SubscriptionName, poolId: 1);
        var c = CreateEvent(SubscriptionName, poolId: 1);
        var storage = new FakeCrossPartitionStorage([a, b, c]);

        using var cts = new CancellationTokenSource();
        var handler = new RetryOrCancelHandler(cts);
        var options = new OutboxProcessorOptions { MaxRetries = 1 };
        var processor = CreateProcessor(subscription, storage, handler, options);

        // Pass 1: all three fail once.
        await processor.RunProcessorAsync(_ct);

        // Pass 2: A fails its last allowed attempt, then B cancels the pass before the delete,
        // and C is never visited.
        handler.CancelOn = b.MessageId;
        await processor.RunProcessorAsync(cts.Token);
        storage.Events.Count.ShouldBe(3, "a cancelled pass deletes nothing");

        // Pass 3: A is deleted without another handler call, B's cancelled call did not count,
        // and C kept its count, so B and C both use their last attempt and are dropped.
        handler.CancelOn = null;
        await processor.RunProcessorAsync(_ct);

        handler.Calls(a.MessageId).ShouldBe(2, "A exhausted its attempts before the cancellation and must not be called again");
        handler.Calls(b.MessageId).ShouldBe(3);
        handler.Calls(c.MessageId).ShouldBe(2);
        storage.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task max_retries_of_zero_drops_on_the_first_failure()
    {
        var subscription = new FakeSubscription(SubscriptionName);
        var evt = CreateEvent(SubscriptionName, poolId: 1);
        var storage = new FakeCrossPartitionStorage([evt]);

        var handler = new AlwaysRetryHandler();
        var options = new OutboxProcessorOptions { MaxRetries = 0 };
        var processor = CreateProcessor(subscription, storage, handler, options);

        await processor.RunProcessorAsync(_ct);

        handler.CallCount.ShouldBe(1);
        storage.Events.ShouldBeEmpty("MaxRetries = 0 means a failing row is dropped on its first failed attempt");
    }

    [Fact]
    public async Task max_retries_of_int_max_value_keeps_retrying_a_failing_event()
    {
        var subscription = new FakeSubscription(SubscriptionName);
        var evt = CreateEvent(SubscriptionName, poolId: 1);
        var storage = new FakeCrossPartitionStorage([evt]);

        var handler = new AlwaysRetryHandler();
        var options = new OutboxProcessorOptions { MaxRetries = int.MaxValue };
        var processor = CreateProcessor(subscription, storage, handler, options);

        for (var i = 0; i < 3; i++)
        {
            await processor.RunProcessorAsync(_ct);
        }

        handler.CallCount.ShouldBe(3);
        storage.Events.ShouldNotBeEmpty(
            "MaxRetries = int.MaxValue must not overflow into force-dropping the row on its first attempt");
    }

    [Fact]
    public async Task context_establishment_failure_is_logged_as_its_own_event_with_the_pool_id()
    {
        var subscription = new FakeSubscription(SubscriptionName);
        var evt = CreateEvent(SubscriptionName, poolId: 7);
        var storage = new FakeCrossPartitionStorage([evt]);

        var handler = new AlwaysRetryHandler();
        var scopeFactory = CreateScopeFactory((SubscriptionName.Value, handler));
        var recordingLogger = new RecordingLogger<OutboxProcessor>();
        var processor = new OutboxProcessor(
            new MappedCrossPartitionStorageFactory((InstanceId, storage)),
            new TwoInstanceRouter(InstanceId),
            [subscription],
            scopeFactory,
            new ThrowingAmbientContextProvider(),
            Options.Create(new OutboxProcessorOptions { MaxRetries = 5 }),
            recordingLogger);

        await processor.RunProcessorAsync(_ct);

        var entry = recordingLogger.Entries.Single(e => e.EventName == nameof(OutboxProcessorLog.ContextEstablishmentFailed));
        entry.State.ShouldContain(kv => kv.Key == "PoolId" && Equals(kv.Value, 7));
        entry.State.ShouldContain(kv => kv.Key == "MessageId" && Equals(kv.Value, evt.MessageId.Value));
        recordingLogger.Entries.ShouldNotContain(e => e.EventName == nameof(OutboxProcessorLog.HandlerException),
            "a context establishment failure must not also be logged as a HandlerException");
        handler.CallCount.ShouldBe(0, "the handler must never be reached when context establishment fails");
    }

    [Fact]
    public async Task missing_handler_skips_reading_the_queue()
    {
        var subscription = new FakeSubscription(SubscriptionName);
        var evt = CreateEvent(SubscriptionName, poolId: 1);
        var storage = new FakeCrossPartitionStorage([evt]);

        // No keyed handler registered for this subscription's name.
        var scopeFactory = CreateScopeFactory();
        var processor = new OutboxProcessor(
            new MappedCrossPartitionStorageFactory((InstanceId, storage)),
            new TwoInstanceRouter(InstanceId),
            [subscription],
            scopeFactory,
            new DefaultAmbientOutboxProcessingContextProvider(),
            Options.Create(new OutboxProcessorOptions()),
            NullLogger<OutboxProcessor>.Instance);

        var exception = await Record.ExceptionAsync(() => processor.RunProcessorAsync(_ct));

        exception.ShouldBeNull();
        storage.ReadCounts.ShouldBeEmpty("a subscription without a registered handler must not read its queue");
        storage.Events.ShouldBe([evt], "the row must stay queued when there is no handler to process it");
    }

    private static OutboxProcessor CreateProcessor(
        FakeSubscription subscription,
        FakeCrossPartitionStorage storage,
        IOutboxSubscriptionHandler handler,
        OutboxProcessorOptions options) =>
        new(
            new MappedCrossPartitionStorageFactory((InstanceId, storage)),
            new TwoInstanceRouter(InstanceId),
            [subscription],
            CreateScopeFactory((subscription.SubscriberName.Value, handler)),
            new DefaultAmbientOutboxProcessingContextProvider(),
            Options.Create(options),
            NullLogger<OutboxProcessor>.Instance);

    private sealed class AlwaysRetryHandler : IOutboxSubscriptionHandler
    {
        public int CallCount { get; private set; }

        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
        {
            CallCount++;
            return Task.FromResult(HandleOutcomeResult.Retry("always fails"));
        }
    }

    private sealed class RetryOrCancelHandler(CancellationTokenSource cts) : IOutboxSubscriptionHandler
    {
        private readonly Dictionary<OutboxEventId, int> _calls = [];

        public OutboxEventId? CancelOn { get; set; }

        public int Calls(OutboxEventId messageId) => _calls.GetValueOrDefault(messageId);

        public async Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
        {
            _calls[item.MessageId] = Calls(item.MessageId) + 1;
            if (item.MessageId == CancelOn)
            {
                await cts.CancelAsync();
                ct.ThrowIfCancellationRequested();
            }

            return HandleOutcomeResult.Retry("always fails");
        }
    }

    private sealed class ThrowingAmbientContextProvider : IAmbientOutboxProcessingContextProvider
    {
        public Task<HandleOutcomeResult> ExecuteWithContextAsync(
            PersistedOutboxEvent evt,
            Func<Ct, Task<HandleOutcomeResult>> action,
            Ct ct) =>
            throw new InvalidOperationException("Simulated context establishment failure.");
    }

    private sealed record LoggedEntry(string? EventName, IReadOnlyList<KeyValuePair<string, object?>> State);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly List<LoggedEntry> _entries = [];

        public IReadOnlyList<LoggedEntry> Entries => _entries;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state as IReadOnlyList<KeyValuePair<string, object?>>
                ?? (state as IEnumerable<KeyValuePair<string, object?>>)?.ToList()
                ?? [];
            _entries.Add(new LoggedEntry(eventId.Name, values));
        }
    }
}
