// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Querying.SearchFields;
using Microsoft.Extensions.DependencyInjection;
using OutboxEventId = Duende.Storage.Internal.Outbox.OutboxEventId;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;
using SubscriberName = Duende.Storage.Internal.Outbox.SubscriberName;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Integration tests for outbox event write and read operations across all storage types.
/// </summary>
public partial class StoreOutboxOperations
{

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static readonly EntityType EntityType = TestDso.DsoVersion.EntityType;
    private static readonly LinkDefinition TestLink = TestLinkData.TestLink;
    private static readonly SubscriberName WildcardSubscriberName =
        SubscriberName.Create("test-subscription");

    [Fact]
    public async Task OutboxEventsAreWrittenOnCreate()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var evt = MakeEvent();

        var result = await partitionedStorage.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], _ct);
        result.ShouldBe(CreateResult.Success);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldContain(e => e.EventId == evt.Id);
        var persistedEvt = page.Events.Single(e => e.EventId == evt.Id);
        (persistedEvt.Dso is not null).ShouldBeTrue();
        (persistedEvt.Dso is TestDso).ShouldBeTrue();
    }

    [Fact]
    public async Task OutboxEventWithoutDsoTypeSchemaVersionHasNullDso()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var evt = MakeEvent() with { DsoTypeSchemaVersion = null };

        var result = await partitionedStorage.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], _ct);
        result.ShouldBe(CreateResult.Success);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 10, _ct);
        var persistedEvt = page.Events.Single(e => e.EventId == evt.Id);
        (persistedEvt.Dso is null).ShouldBeTrue();
    }

    [Fact]
    public async Task OutboxEventsAreWrittenOnUpdate()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        (await partitionedStorage.CreateAsync(id, new TestDso("v"), [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        var version = (await partitionedStorage.TryReadAsync(EntityType, id, _ct)).Version!.Value;

        var evt = MakeEvent();
        var result = await partitionedStorage.UpdateAsync(id, new TestDso("v2"), version, [], SearchFieldCollection.Empty, null, [evt], _ct);
        result.ShouldBe(UpdateResult.Success);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task OutboxEventsAreWrittenOnDelete()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        (await partitionedStorage.CreateAsync(id, new TestDso("v"), [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        var evt = MakeEvent();
        var result = await partitionedStorage.DeleteAsync(EntityType, id, [evt], _ct);
        result.ShouldBe(DeleteResult.Success);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task OutboxEventsAreWrittenOnLink()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        var evt = MakeEvent();
        var result = await partitionedStorage.LinkAsync(TestLink, leftId, rightId, [evt], _ct);
        result.ShouldBe(LinkResult.Success);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task OutboxEventsAreWrittenOnUnlink()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();
        _ = await partitionedStorage.LinkAsync(TestLink, leftId, rightId, [], _ct);

        var evt = MakeEvent();
        var result = await partitionedStorage.UnlinkAsync(TestLink, leftId, rightId, [evt], _ct);
        result.ShouldBe(UnlinkResult.Success);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task OutboxEventsAreWrittenOnBatch()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var operations = new IStorageOperation[]
        {
            CreateOperation.For(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration)
        };

        var evt = MakeEvent();
        var result = await partitionedStorage.ExecuteBatchAsync(operations, [evt], _ct);
        result.Success.ShouldBeTrue();

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task MultipleOutboxEventsPerTransactionAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var evt1 = MakeEvent();
        var evt2 = MakeEvent();
        var evt3 = MakeEvent();

        var result = await partitionedStorage.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt1, evt2, evt3], _ct);
        result.ShouldBe(CreateResult.Success);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 10, _ct);
        page.Events.Select(e => e.EventId).ShouldContain(evt1.Id);
        page.Events.Select(e => e.EventId).ShouldContain(evt2.Id);
        page.Events.Select(e => e.EventId).ShouldContain(evt3.Id);
    }

    [Fact]
    public async Task DeleteOutboxEventsRemovesByIdAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var evt1 = MakeEvent();
        var evt2 = MakeEvent();
        var evt3 = MakeEvent();

        var id = UuidV7.New();
        _ = await partitionedStorage.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt1, evt2, evt3], _ct);

        // Get persisted events to retrieve their MessageIds
        var allEvents = (await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 10, _ct)).Events;
        var msgId1 = allEvents.Single(e => e.EventId == evt1.Id).MessageId;
        var msgId2 = allEvents.Single(e => e.EventId == evt2.Id).MessageId;

        // Delete first two by MessageId
        await fixture.DefaultCrossPartitionStorage.DeleteOutboxEventsAsync([msgId1, msgId2], _ct);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldNotContain(e => e.EventId == evt1.Id);
        page.Events.ShouldNotContain(e => e.EventId == evt2.Id);
        page.Events.ShouldContain(e => e.EventId == evt3.Id);
    }

    [Fact]
    public async Task OutboxEventsNotWrittenWhenOperationFailsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        (await partitionedStorage.CreateAsync(id, new TestDso("existing"), [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        var evt = MakeEvent();
        var result = await partitionedStorage.CreateAsync(id, new TestDso("duplicate"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], _ct);
        result.ShouldBe(CreateResult.AlreadyExists);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldNotContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task BatchOutboxEventsNotWrittenWhenBatchFailsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        // Pre-create an entity to cause conflict
        var existingId = UuidV7.New();
        (await partitionedStorage.CreateAsync(existingId, new TestDso("existing"), [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        var newId = UuidV7.New();
        var operations = new IStorageOperation[]
        {
            CreateOperation.For(newId, new TestDso("new"), [], SearchFieldCollection.Empty, Expiration.NoExpiration),
            CreateOperation.For(existingId, new TestDso("conflict"), [], SearchFieldCollection.Empty, Expiration.NoExpiration), // will fail
        };

        var evt = MakeEvent();
        var result = await partitionedStorage.ExecuteBatchAsync(operations, [evt], _ct);
        result.Success.ShouldBeFalse();

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldNotContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task OutboxEventsNotWrittenWhenDeletingNonExistentEntityAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var nonExistentId = UuidV7.New();
        var evt = MakeEvent();

        var result = await partitionedStorage.DeleteAsync(EntityType, nonExistentId, [evt], _ct);
        result.ShouldBe(DeleteResult.Success);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldNotContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task NoMessagesWrittenWhenNoSubscriptionsAsync()
    {
        await using var fixture = await CreateProviderAsync([]);
        var partitionedStorage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var evt = MakeEvent();

        var result = await partitionedStorage.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], _ct);
        result.ShouldBe(CreateResult.Success);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task OneMessagePerSubscription()
    {
        var subscription = new TestSubscription("sub-a");
        await using var fixture = await CreateProviderAsync([subscription]);
        var partitionedStorage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var evt = MakeEvent();

        var result = await partitionedStorage.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], _ct);
        result.ShouldBe(CreateResult.Success);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 10, _ct);
        page.Events.Count.ShouldBe(1);
        page.Events[0].EventId.ShouldBe(evt.Id);
        page.Events[0].SubscriberName.ShouldBe(subscription.SubscriberName);
    }

    [Fact]
    public async Task MultipleSubscriptionsProduceMultipleMessagesAsync()
    {
        var subA = new TestSubscription("sub-a");
        var subB = new TestSubscription("sub-b");
        var subC = new TestSubscription("sub-c");
        await using var fixture = await CreateProviderAsync([subA, subB, subC]);
        var partitionedStorage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var evt = MakeEvent();

        var result = await partitionedStorage.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], _ct);
        result.ShouldBe(CreateResult.Success);

        var pageA = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subA.SubscriberName, 10, _ct);
        var pageB = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subB.SubscriberName, 10, _ct);
        var pageC = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subC.SubscriberName, 10, _ct);
        var allEvents = pageA.Events.Concat(pageB.Events).Concat(pageC.Events).ToList();

        // All 3 rows share the same EventId but have distinct MessageIds and SubscriberNames
        allEvents.Count.ShouldBe(3);
        allEvents.ShouldAllBe(e => e.EventId == evt.Id);
        allEvents.Select(e => e.MessageId).Distinct().Count().ShouldBe(3);
        allEvents.Select(e => e.SubscriberName).ShouldBe(
            [subA.SubscriberName, subB.SubscriberName, subC.SubscriberName], ignoreOrder: true);
    }

    [Fact]
    public async Task OutboxEventsNotWrittenWhenDisabledAsync()
    {
        await using var fixture = await FixtureFactory.CreateAsync(
            _ct,
            services =>
            {
                // No IOutboxSubscription registrations → outbox is effectively disabled
                services.AddDsoRegistration<TestDso>();
                services.AddDsoRegistration<TestDso2>();
            });

        var partitionedStorage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var evt = MakeEvent();

        var result = await partitionedStorage.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], _ct);
        result.ShouldBe(CreateResult.Success);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldNotContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task GetOutboxEventsForSubscriptionFiltersBySubscriptionAsync()
    {
        var subA = new TestSubscription("sub-a");
        var subB = new TestSubscription("sub-b");
        await using var fixture = await CreateProviderAsync([subA, subB]);
        var partitionedStorage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var evt = MakeEvent();
        _ = await partitionedStorage.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], _ct);

        var pageA = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subA.SubscriberName, 10, _ct);
        pageA.Events.ShouldAllBe(e => e.SubscriberName == subA.SubscriberName);
        pageA.Events.Count.ShouldBe(1);

        var pageB = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subB.SubscriberName, 10, _ct);
        pageB.Events.ShouldAllBe(e => e.SubscriberName == subB.SubscriberName);
        pageB.Events.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetOutboxEventsForSubscriptionReturnsPagedAsync()
    {
        var sub = new TestSubscription("sub-paged");
        await using var fixture = await CreateProviderAsync([sub]);
        var partitionedStorage = fixture.PartitionedStorage;

        for (var i = 1; i <= 5; i++)
        {
            var id = UuidV7.New();
            _ = await partitionedStorage.CreateAsync(id, new TestDso($"v{i}"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [MakeEvent()], _ct);
        }

        var page1 = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(sub.SubscriberName, 3, _ct);
        page1.Events.Count.ShouldBe(3);
        page1.HasMore.ShouldBeTrue();

        await fixture.DefaultCrossPartitionStorage.DeleteOutboxEventsAsync(page1.Events.Select(e => e.MessageId).ToList(), _ct);

        var page2 = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(sub.SubscriberName, 3, _ct);
        page2.Events.Count.ShouldBe(2);
        page2.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task GetOutboxEventsForSubscriptionReturnsEmptyWhenNoMatchAsync()
    {
        var subA = new TestSubscription("sub-a");
        var subB = new TestSubscription("sub-b");
        await using var fixture = await CreateProviderAsync([subA]);
        var partitionedStorage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        _ = await partitionedStorage.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [MakeEvent()], _ct);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subB.SubscriberName, 10, _ct);
        page.Events.ShouldBeEmpty();
        page.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task GetOutboxEventsForSubscriptionReturnsInSequenceOrderAsync()
    {
        var sub = new TestSubscription("sub-ordered");
        await using var fixture = await CreateProviderAsync([sub]);
        var partitionedStorage = fixture.PartitionedStorage;

        for (var i = 1; i <= 3; i++)
        {
            var id = UuidV7.New();
            _ = await partitionedStorage.CreateAsync(id, new TestDso($"v{i}"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [MakeEvent()], _ct);
        }

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(sub.SubscriberName, 10, _ct);
        page.Events.Count.ShouldBe(3);
        page.Events.Select(e => e.SequenceNumber)
            .ShouldBe(page.Events.Select(e => e.SequenceNumber).OrderBy(n => n));
    }

    private static OutboxEvent MakeEvent() => new()
    {
        Id = OutboxEventId.New(),
        Timestamp = DateTimeOffset.UtcNow,
        EventName = OutboxEventName.Create("TestEvent"),
        SubjectId = UuidV7.New(),
        EntityTypeName = nameof(TestDso),
        EntityTypeId = (int)TestDso.DsoVersion.EntityType.Id,
        Payload = JsonSerializer.Serialize(new TestDso("outbox-test")),
        DsoTypeSchemaVersion = (int)TestDso.DsoVersion.SchemaVersion,
    };

    private async Task<IStorageFixture> CreateProviderAsync()
        => await CreateProviderAsync([new WildcardTestSubscription()]);

    private async Task<IStorageFixture> CreateProviderAsync(IOutboxSubscription[] subscriptions) =>
        await FixtureFactory.CreateAsync(
            _ct,
            services =>
            {
                foreach (var subscription in subscriptions)
                {
                    _ = services.AddSingleton(subscription);
                }
                services.AddDsoRegistration<TestDso>();
                services.AddDsoRegistration<TestDso2>();
            });

    /// <summary>
    /// Wildcard subscription that matches all entity types and event names, used to ensure
    /// outbox events are written to the storage in tests.
    /// </summary>
    private sealed class WildcardTestSubscription : IOutboxSubscription
    {
        public SubscriberName SubscriberName => SubscriberName.Create("test-subscription");
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames => new HashSet<OutboxEventName>();
        public IReadOnlySet<int> EntityTypeIds => new HashSet<int>();
    }

    /// <summary>
    /// Named wildcard subscription for multi-subscription fanout tests.
    /// </summary>
    private sealed class TestSubscription(string name) : IOutboxSubscription
    {
        public SubscriberName SubscriberName => SubscriberName.Create(name);
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames => new HashSet<OutboxEventName>();
        public IReadOnlySet<int> EntityTypeIds => new HashSet<int>();
    }
}
