// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Duende.Storage.Internal;

/// <summary>
/// Verifies that <see cref="OutboxProcessor"/> keys its in-memory retry state by
/// <c>(StorageInstanceId, SubscriberName)</c> rather than by subscription name alone, so that
/// failing events on one storage instance do not delay, steal, or clear retry state that
/// belongs to the same subscription on a different storage instance.
/// </summary>
public sealed class OutboxProcessorInstanceIsolationTests
{
    private static readonly StorageInstanceId InstanceA = StorageInstanceId.Create("instance-a");
    private static readonly StorageInstanceId InstanceB = StorageInstanceId.Create("instance-b");
    private static readonly SubscriberName SharedSubscriberName = SubscriberName.Create("shared-subscription");

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task healthy_instance_is_still_processed_in_the_same_cycle_when_the_other_instance_with_the_same_subscription_fails()
    {
        // instance A's event always asks for a retry (poolId 1); instance B's event always
        // succeeds (poolId 2). Both are dispatched to the same keyed handler, because the
        // retry slot is only ever shared by subscription name, never by storage instance.
        var subscription = new FakeSubscription(SharedSubscriberName);
        var eventA = CreateEvent(subscription.SubscriberName, poolId: 1);
        var eventB = CreateEvent(subscription.SubscriberName, poolId: 2);
        var storageA = new FakeCrossPartitionStorage([eventA]);
        var storageB = new FakeCrossPartitionStorage([eventB]);

        var handler = new PoolAwareHandler(retryForPoolId: 1);
        var processor = CreateProcessor(
            [subscription],
            new TwoInstanceRouter(InstanceA, InstanceB),
            new MappedCrossPartitionStorageFactory((InstanceA, storageA), (InstanceB, storageB)),
            CreateScopeFactory((subscription.SubscriberName.Value, handler)));

        await processor.RunProcessorAsync(_ct);

        // Instance B's event was handled and deleted in the very same cycle as instance A's
        // retry, proving the two instances do not share a retry gate.
        storageB.Events.ShouldBeEmpty();
        storageA.Events.Select(e => e.MessageId).ShouldBe([eventA.MessageId]);
        handler.ObservedPoolIds.ShouldBe([1, 2]);
    }

    [Fact]
    public async Task attempt_counts_and_force_drop_are_isolated_per_instance_for_the_same_subscription()
    {
        // Both instances always ask for a retry, forever. If the retry slot were keyed only
        // by subscription name, instance A would permanently own the slot (processed first each
        // cycle) and instance B's failures would never be recorded — it would never reach
        // MaxRetries and would never be force-dropped.
        var subscription = new FakeSubscription(SharedSubscriberName);
        var eventA = CreateEvent(subscription.SubscriberName, poolId: 1);
        var eventB = CreateEvent(subscription.SubscriberName, poolId: 2);
        var storageA = new FakeCrossPartitionStorage([eventA]);
        var storageB = new FakeCrossPartitionStorage([eventB]);

        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var handler = new AlwaysRetryHandler();
        var options = new OutboxProcessorOptions { MaxRetries = 2, RetryDelay = TimeSpan.FromSeconds(1) };
        var processor = CreateProcessor(
            [subscription],
            new TwoInstanceRouter(InstanceA, InstanceB),
            new MappedCrossPartitionStorageFactory((InstanceA, storageA), (InstanceB, storageB)),
            CreateScopeFactory((subscription.SubscriberName.Value, handler)),
            options,
            timeProvider);

        await processor.RunProcessorAsync(_ct); // attempt 1 for both
        timeProvider.Advance(TimeSpan.FromSeconds(2));
        await processor.RunProcessorAsync(_ct); // attempt 2 for both, reaches MaxRetries
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        await processor.RunProcessorAsync(_ct); // force-drop for both

        storageA.Events.ShouldBeEmpty("instance A's event must be force-dropped once its own MaxRetries is exceeded");
        storageB.Events.ShouldBeEmpty("instance B's event must independently be force-dropped once its own MaxRetries is exceeded, not left permanently retried because instance A owns a shared slot");
    }

    [Fact]
    public async Task empty_page_on_one_instance_does_not_clear_retry_state_of_another_instance_with_the_same_subscription()
    {
        // Instance A has a pending retry. Instance B is (and remains) fully drained. If the
        // retry slot were keyed only by subscription name, instance B's empty-page branch would
        // see instance A's non-null FailedMessageId and incorrectly clear it, resetting
        // instance A's attempt count back to zero every cycle so it would never reach
        // MaxRetries.
        var subscription = new FakeSubscription(SharedSubscriberName);
        var eventA = CreateEvent(subscription.SubscriberName, poolId: 1);
        var storageA = new FakeCrossPartitionStorage([eventA]);
        var storageB = new FakeCrossPartitionStorage([]);

        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var handler = new AlwaysRetryHandler();
        var options = new OutboxProcessorOptions { MaxRetries = 2, RetryDelay = TimeSpan.FromSeconds(1) };
        var processor = CreateProcessor(
            [subscription],
            new TwoInstanceRouter(InstanceA, InstanceB),
            new MappedCrossPartitionStorageFactory((InstanceA, storageA), (InstanceB, storageB)),
            CreateScopeFactory((subscription.SubscriberName.Value, handler)),
            options,
            timeProvider);

        await processor.RunProcessorAsync(_ct); // A attempt 1; B empty (no-op)
        timeProvider.Advance(TimeSpan.FromSeconds(2));
        await processor.RunProcessorAsync(_ct); // A attempt 2, reaches MaxRetries; B still empty
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        await processor.RunProcessorAsync(_ct); // A force-dropped

        storageA.Events.ShouldBeEmpty("instance A's retry attempts must accumulate across cycles rather than being reset by instance B's unrelated empty page");
    }

    [Fact]
    public async Task exception_on_one_instance_does_not_prevent_the_next_instance_from_being_processed()
    {
        var subscription = new FakeSubscription(SharedSubscriberName);
        var eventB = CreateEvent(subscription.SubscriberName, poolId: 2);
        var storageB = new FakeCrossPartitionStorage([eventB]);

        var handler = new PoolAwareHandler(retryForPoolId: null);
        var processor = CreateProcessor(
            [subscription],
            new TwoInstanceRouter(InstanceA, InstanceB),
            new MappedCrossPartitionStorageFactory(throwFor: InstanceA, (InstanceB, storageB)),
            CreateScopeFactory((subscription.SubscriberName.Value, handler)));

        var exception = await Record.ExceptionAsync(() => processor.RunProcessorAsync(_ct));

        exception.ShouldBeNull("a failing instance must not abort processing of the remaining instances for the same subscription");
        storageB.Events.ShouldBeEmpty("instance B must still be processed and its event handled despite instance A's factory throwing");
    }

    private static OutboxProcessor CreateProcessor(
        IEnumerable<IOutboxSubscription> subscriptions,
        IStorageInstanceRouter storageInstanceRouter,
        ICrossPartitionStorageFactory crossPartitionStorageFactory,
        IServiceScopeFactory scopeFactory,
        OutboxProcessorOptions? options = null,
        TimeProvider? timeProvider = null) =>
        new(
            crossPartitionStorageFactory,
            storageInstanceRouter,
            subscriptions,
            scopeFactory,
            new DefaultAmbientOutboxProcessingContextProvider(),
            Options.Create(options ?? new OutboxProcessorOptions()),
            timeProvider ?? TimeProvider.System,
            NullLogger<OutboxProcessor>.Instance);

    private static IServiceScopeFactory CreateScopeFactory(params (string Key, IOutboxSubscriptionHandler Handler)[] handlers)
    {
        var services = new ServiceCollection();
        foreach (var (key, handler) in handlers)
        {
            _ = services.AddKeyedSingleton(key, handler);
        }

        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static PersistedOutboxEvent CreateEvent(SubscriberName subscriptionName, int poolId) =>
        new()
        {
            MessageId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            SequenceNumber = 1,
            EventName = OutboxEventName.Create("TestEvent"),
            SubjectId = UuidV7.New(),
            EntityTypeName = "TestEntity",
            EntityTypeId = 1,
            PoolId = poolId,
            Payload = "{}",
            SubscriberName = subscriptionName,
        };

    private sealed class FakeSubscription(SubscriberName subscriptionName) : IOutboxSubscription
    {
        public SubscriberName SubscriberName { get; } = subscriptionName;
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames { get; } = new HashSet<OutboxEventName>();
        public IReadOnlySet<int> EntityTypeIds { get; } = new HashSet<int>();
    }

    private sealed class FakeCrossPartitionStorage(IEnumerable<PersistedOutboxEvent> events) : ICrossPartitionStorage
    {
        private readonly List<PersistedOutboxEvent> _events = [.. events];

        public IReadOnlyList<PersistedOutboxEvent> Events => _events;

        public Task<OutboxEventsPage> GetOutboxEventsForSubscriptionAsync(SubscriberName subscriptionName, int count, Ct ct)
        {
            var page = _events.Take(count).ToList();
            return Task.FromResult(new OutboxEventsPage(page, HasMore: false));
        }

        public Task DeleteOutboxEventsAsync(IReadOnlyList<OutboxEventId> ids, Ct ct)
        {
            _ = _events.RemoveAll(e => ids.Contains(e.MessageId));
            return Task.CompletedTask;
        }

        public Task<int> PurgeExpiredAsync(int batchSize, Ct ct) => Task.FromResult(0);
    }

    private sealed class TwoInstanceRouter(params StorageInstanceId[] instances) : IStorageInstanceRouter
    {
        public void RegisterInstance(StorageInstanceId storageInstanceId)
        {
        }

        public void AddMapping(DataCategoryName dataCategory, StorageInstanceId storageInstanceId)
        {
        }

        public StorageInstanceId Resolve(DataCategoryName dataCategory) => instances[0];

        public IReadOnlyCollection<StorageInstanceId> GetAll() => instances;
    }

    private sealed class MappedCrossPartitionStorageFactory : ICrossPartitionStorageFactory
    {
        private readonly Dictionary<StorageInstanceId, ICrossPartitionStorage> _map;
        private readonly StorageInstanceId? _throwFor;

        public MappedCrossPartitionStorageFactory(params (StorageInstanceId StorageInstanceId, ICrossPartitionStorage CrossPartitionStorage)[] mappings) : this(throwFor: null, mappings)
        {
        }

        public MappedCrossPartitionStorageFactory(StorageInstanceId? throwFor, params (StorageInstanceId StorageInstanceId, ICrossPartitionStorage CrossPartitionStorage)[] mappings)
        {
            _throwFor = throwFor;
            _map = mappings.ToDictionary(m => m.StorageInstanceId, m => m.CrossPartitionStorage);
        }

        public Task<ICrossPartitionStorage> GetCrossPartitionStorageAsync(StorageInstanceId storageInstanceId, Ct ct) =>
            storageInstanceId == _throwFor
                ? throw new InvalidOperationException($"Simulated failure for instance '{storageInstanceId.Value}'.")
                : Task.FromResult(_map[storageInstanceId]);
    }

    private sealed class PoolAwareHandler(int? retryForPoolId) : IOutboxSubscriptionHandler
    {
        public List<int> ObservedPoolIds { get; } = [];

        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
        {
            ObservedPoolIds.Add(item.PoolId.Value);
            return Task.FromResult(item.PoolId.Value == retryForPoolId
                ? HandleOutcomeResult.Retry("simulated failure")
                : HandleOutcomeResult.Success());
        }
    }

    private sealed class AlwaysRetryHandler : IOutboxSubscriptionHandler
    {
        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct) =>
            Task.FromResult(HandleOutcomeResult.Retry("always fails"));
    }
}
