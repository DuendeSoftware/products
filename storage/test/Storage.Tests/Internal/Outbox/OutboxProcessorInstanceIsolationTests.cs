// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static Duende.Storage.Internal.OutboxProcessorTestHelpers;

namespace Duende.Storage.Internal;

/// <summary>
/// Verifies that <see cref="OutboxProcessor"/> keys its in-memory failure tracking by
/// <c>(StorageInstanceId, SubscriberName)</c> rather than by subscription name alone, so that
/// failing events on one storage instance do not delay, steal, or clear failure tracking that
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
        // succeeds (poolId 2). Both are dispatched to the same keyed handler, because failure
        // tracking is only ever shared by subscription name, never by storage instance.
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
        // retry, proving the two instances do not share failure tracking.
        storageB.Events.ShouldBeEmpty();
        storageA.Events.Select(e => e.MessageId).ShouldBe([eventA.MessageId]);
        handler.ObservedPoolIds.ShouldBe([1, 2]);
    }

    [Fact]
    public async Task attempt_counts_and_force_drop_are_isolated_per_instance_for_the_same_subscription()
    {
        // Both instances always ask for a retry, forever. With a shared key, instance B's
        // rebuilt failure map would overwrite instance A's every pass, so neither would ever
        // reach its last attempt.
        var subscription = new FakeSubscription(SharedSubscriberName);
        var eventA = CreateEvent(subscription.SubscriberName, poolId: 1);
        var eventB = CreateEvent(subscription.SubscriberName, poolId: 2);
        var storageA = new FakeCrossPartitionStorage([eventA]);
        var storageB = new FakeCrossPartitionStorage([eventB]);

        var handler = new AlwaysRetryHandler();
        var options = new OutboxProcessorOptions { MaxRetries = 1 };
        var processor = CreateProcessor(
            [subscription],
            new TwoInstanceRouter(InstanceA, InstanceB),
            new MappedCrossPartitionStorageFactory((InstanceA, storageA), (InstanceB, storageB)),
            CreateScopeFactory((subscription.SubscriberName.Value, handler)),
            options);

        await processor.RunProcessorAsync(_ct); // attempt 1 for both; one retry left for each

        storageA.Events.ShouldNotBeEmpty("instance A's event must still be queued after its first failed attempt");
        storageB.Events.ShouldNotBeEmpty("instance B's event must still be queued after its first failed attempt");

        await processor.RunProcessorAsync(_ct); // attempt 2 for both, reaches MaxRetries, force-dropped

        storageA.Events.ShouldBeEmpty("instance A's event must be force-dropped once its own MaxRetries is exceeded");
        storageB.Events.ShouldBeEmpty("instance B's event must independently be force-dropped once its own MaxRetries is exceeded, not left permanently retried because instance A owns a shared key");
    }

    [Fact]
    public async Task empty_page_on_one_instance_does_not_reset_attempt_counts_of_another_instance_with_the_same_subscription()
    {
        // Instance A has a pending retry. Instance B is (and remains) fully drained. If
        // failure tracking were keyed only by subscription name, instance B's empty-page
        // branch would clear instance A's attempt count every cycle so it would never reach
        // MaxRetries.
        var subscription = new FakeSubscription(SharedSubscriberName);
        var eventA = CreateEvent(subscription.SubscriberName, poolId: 1);
        var storageA = new FakeCrossPartitionStorage([eventA]);
        var storageB = new FakeCrossPartitionStorage([]);

        var handler = new AlwaysRetryHandler();
        var options = new OutboxProcessorOptions { MaxRetries = 1 };
        var processor = CreateProcessor(
            [subscription],
            new TwoInstanceRouter(InstanceA, InstanceB),
            new MappedCrossPartitionStorageFactory((InstanceA, storageA), (InstanceB, storageB)),
            CreateScopeFactory((subscription.SubscriberName.Value, handler)),
            options);

        await processor.RunProcessorAsync(_ct); // A attempt 1; B empty (no-op)

        storageA.Events.ShouldNotBeEmpty("instance A's event must still be queued after its first failed attempt");

        await processor.RunProcessorAsync(_ct); // A attempt 2, reaches MaxRetries, force-dropped; B still empty

        storageA.Events.ShouldBeEmpty("instance A's attempt count must accumulate across cycles rather than being reset by instance B's unrelated empty page");
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
        OutboxProcessorOptions? options = null) =>
        new(
            crossPartitionStorageFactory,
            storageInstanceRouter,
            subscriptions,
            scopeFactory,
            new DefaultAmbientOutboxProcessingContextProvider(),
            Options.Create(options ?? new OutboxProcessorOptions()),
            NullLogger<OutboxProcessor>.Instance);

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
