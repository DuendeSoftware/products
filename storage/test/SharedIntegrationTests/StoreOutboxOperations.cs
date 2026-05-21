// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

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
/// Integration tests for outbox event write and read operations across all store types.
/// </summary>
public partial class StoreOutboxOperations
{

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static readonly EntityType EntityType = TestDso.DsoVersion.EntityType;
    private static readonly LinkDefinition TestLink = TestLinkData.TestLink;
    private static readonly SubscriberName WildcardSubscriberName =
        SubscriberName.Create("test-subscriber");

    [Fact]
    public async Task OutboxEventsAreWrittenOnCreate()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var id = UuidV7.New();
        var evt = MakeEvent();

        var result = await store.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], _ct);
        result.ShouldBe(CreateResult.Success);

        var page = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task OutboxEventsAreWrittenOnUpdate()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var id = UuidV7.New();
        (await store.CreateAsync(id, new TestDso("v"), [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        var version = (await store.TryReadAsync(EntityType, id, _ct)).Version!.Value;

        var evt = MakeEvent();
        var result = await store.UpdateAsync(id, new TestDso("v2"), version, [], SearchFieldCollection.Empty, null, [evt], _ct);
        result.ShouldBe(UpdateResult.Success);

        var page = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task OutboxEventsAreWrittenOnDelete()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var id = UuidV7.New();
        (await store.CreateAsync(id, new TestDso("v"), [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        var evt = MakeEvent();
        var result = await store.DeleteAsync(EntityType, id, [evt], _ct);
        result.ShouldBe(DeleteResult.Success);

        var page = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task OutboxEventsAreWrittenOnLink()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        var evt = MakeEvent();
        var result = await store.LinkAsync(TestLink, leftId, rightId, [evt], _ct);
        result.ShouldBe(LinkResult.Success);

        var page = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task OutboxEventsAreWrittenOnUnlink()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();
        _ = await store.LinkAsync(TestLink, leftId, rightId, [], _ct);

        var evt = MakeEvent();
        var result = await store.UnlinkAsync(TestLink, leftId, rightId, [evt], _ct);
        result.ShouldBe(UnlinkResult.Success);

        var page = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task OutboxEventsAreWrittenOnBatch()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var id = UuidV7.New();
        var operations = new IStoreOperation[]
        {
            CreateOperation.For(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration)
        };

        var evt = MakeEvent();
        var result = await store.ExecuteBatchAsync(operations, [evt], _ct);
        result.Success.ShouldBeTrue();

        var page = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task MultipleOutboxEventsPerTransactionAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var id = UuidV7.New();
        var evt1 = MakeEvent();
        var evt2 = MakeEvent();
        var evt3 = MakeEvent();

        var result = await store.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt1, evt2, evt3], _ct);
        result.ShouldBe(CreateResult.Success);

        var page = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 10, _ct);
        page.Events.Select(e => e.EventId).ShouldContain(evt1.Id);
        page.Events.Select(e => e.EventId).ShouldContain(evt2.Id);
        page.Events.Select(e => e.EventId).ShouldContain(evt3.Id);
    }

    [Fact]
    public async Task DeleteOutboxEventsRemovesByIdAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var evt1 = MakeEvent();
        var evt2 = MakeEvent();
        var evt3 = MakeEvent();

        var id = UuidV7.New();
        _ = await store.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt1, evt2, evt3], _ct);

        // Get persisted events to retrieve their MessageIds
        var allEvents = (await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 10, _ct)).Events;
        var msgId1 = allEvents.Single(e => e.EventId == evt1.Id).MessageId;
        var msgId2 = allEvents.Single(e => e.EventId == evt2.Id).MessageId;

        // Delete first two by MessageId
        await store.DeleteOutboxEventsAsync([msgId1, msgId2], _ct);

        var page = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldNotContain(e => e.EventId == evt1.Id);
        page.Events.ShouldNotContain(e => e.EventId == evt2.Id);
        page.Events.ShouldContain(e => e.EventId == evt3.Id);
    }

    [Fact]
    public async Task OutboxEventsNotWrittenWhenOperationFailsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var id = UuidV7.New();
        (await store.CreateAsync(id, new TestDso("existing"), [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        var evt = MakeEvent();
        var result = await store.CreateAsync(id, new TestDso("duplicate"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], _ct);
        result.ShouldBe(CreateResult.AlreadyExists);

        var page = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldNotContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task BatchOutboxEventsNotWrittenWhenBatchFailsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Pre-create an entity to cause conflict
        var existingId = UuidV7.New();
        (await store.CreateAsync(existingId, new TestDso("existing"), [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        var newId = UuidV7.New();
        var operations = new IStoreOperation[]
        {
            CreateOperation.For(newId, new TestDso("new"), [], SearchFieldCollection.Empty, Expiration.NoExpiration),
            CreateOperation.For(existingId, new TestDso("conflict"), [], SearchFieldCollection.Empty, Expiration.NoExpiration), // will fail
        };

        var evt = MakeEvent();
        var result = await store.ExecuteBatchAsync(operations, [evt], _ct);
        result.Success.ShouldBeFalse();

        var page = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldNotContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task OutboxEventsNotWrittenWhenDeletingNonExistentEntityAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var nonExistentId = UuidV7.New();
        var evt = MakeEvent();

        var result = await store.DeleteAsync(EntityType, nonExistentId, [evt], _ct);
        result.ShouldBe(DeleteResult.Success);

        var page = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldNotContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task NoMessagesWrittenWhenNoSubscribersAsync()
    {
        await using var fixture = await CreateProviderAsync([]);
        var store = fixture.Store;

        var id = UuidV7.New();
        var evt = MakeEvent();

        var result = await store.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], _ct);
        result.ShouldBe(CreateResult.Success);

        var page = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task OneMessagePerSubscriber()
    {
        var subscriber = new TestSubscriber("sub-a");
        await using var fixture = await CreateProviderAsync([subscriber]);
        var store = fixture.Store;

        var id = UuidV7.New();
        var evt = MakeEvent();

        var result = await store.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], _ct);
        result.ShouldBe(CreateResult.Success);

        var page = await store.GetOutboxEventsForSubscriberAsync(subscriber.SubscriberName, 10, _ct);
        page.Events.Count.ShouldBe(1);
        page.Events[0].EventId.ShouldBe(evt.Id);
        page.Events[0].SubscriberName.ShouldBe(subscriber.SubscriberName);
    }

    [Fact]
    public async Task MultipleSubscribersProduceMultipleMessagesAsync()
    {
        var subA = new TestSubscriber("sub-a");
        var subB = new TestSubscriber("sub-b");
        var subC = new TestSubscriber("sub-c");
        await using var fixture = await CreateProviderAsync([subA, subB, subC]);
        var store = fixture.Store;

        var id = UuidV7.New();
        var evt = MakeEvent();

        var result = await store.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], _ct);
        result.ShouldBe(CreateResult.Success);

        var pageA = await store.GetOutboxEventsForSubscriberAsync(subA.SubscriberName, 10, _ct);
        var pageB = await store.GetOutboxEventsForSubscriberAsync(subB.SubscriberName, 10, _ct);
        var pageC = await store.GetOutboxEventsForSubscriberAsync(subC.SubscriberName, 10, _ct);
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
                // No IOutboxSubscriber registrations → outbox is effectively disabled
                services.AddDsoRegistration<TestDso>();
                services.AddDsoRegistration<TestDso2>();
            });

        var store = fixture.Store;

        var id = UuidV7.New();
        var evt = MakeEvent();

        var result = await store.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], _ct);
        result.ShouldBe(CreateResult.Success);

        var page = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 10, _ct);
        page.Events.ShouldNotContain(e => e.EventId == evt.Id);
    }

    [Fact]
    public async Task GetOutboxEventsForSubscriberFiltersBySubscriberAsync()
    {
        var subA = new TestSubscriber("sub-a");
        var subB = new TestSubscriber("sub-b");
        await using var fixture = await CreateProviderAsync([subA, subB]);
        var store = fixture.Store;

        var id = UuidV7.New();
        var evt = MakeEvent();
        _ = await store.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], _ct);

        var pageA = await store.GetOutboxEventsForSubscriberAsync(subA.SubscriberName, 10, _ct);
        pageA.Events.ShouldAllBe(e => e.SubscriberName == subA.SubscriberName);
        pageA.Events.Count.ShouldBe(1);

        var pageB = await store.GetOutboxEventsForSubscriberAsync(subB.SubscriberName, 10, _ct);
        pageB.Events.ShouldAllBe(e => e.SubscriberName == subB.SubscriberName);
        pageB.Events.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetOutboxEventsForSubscriberReturnsPagedAsync()
    {
        var sub = new TestSubscriber("sub-paged");
        await using var fixture = await CreateProviderAsync([sub]);
        var store = fixture.Store;

        for (var i = 1; i <= 5; i++)
        {
            var id = UuidV7.New();
            _ = await store.CreateAsync(id, new TestDso($"v{i}"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [MakeEvent()], _ct);
        }

        var page1 = await store.GetOutboxEventsForSubscriberAsync(sub.SubscriberName, 3, _ct);
        page1.Events.Count.ShouldBe(3);
        page1.HasMore.ShouldBeTrue();

        await store.DeleteOutboxEventsAsync(page1.Events.Select(e => e.MessageId).ToList(), _ct);

        var page2 = await store.GetOutboxEventsForSubscriberAsync(sub.SubscriberName, 3, _ct);
        page2.Events.Count.ShouldBe(2);
        page2.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task GetOutboxEventsForSubscriberReturnsEmptyWhenNoMatchAsync()
    {
        var subA = new TestSubscriber("sub-a");
        var subB = new TestSubscriber("sub-b");
        await using var fixture = await CreateProviderAsync([subA]);
        var store = fixture.Store;

        var id = UuidV7.New();
        _ = await store.CreateAsync(id, new TestDso("v"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [MakeEvent()], _ct);

        var page = await store.GetOutboxEventsForSubscriberAsync(subB.SubscriberName, 10, _ct);
        page.Events.ShouldBeEmpty();
        page.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task GetOutboxEventsForSubscriberReturnsInSequenceOrderAsync()
    {
        var sub = new TestSubscriber("sub-ordered");
        await using var fixture = await CreateProviderAsync([sub]);
        var store = fixture.Store;

        for (var i = 1; i <= 3; i++)
        {
            var id = UuidV7.New();
            _ = await store.CreateAsync(id, new TestDso($"v{i}"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [MakeEvent()], _ct);
        }

        var page = await store.GetOutboxEventsForSubscriberAsync(sub.SubscriberName, 10, _ct);
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
        Payload = "{}",
    };

    private async Task<IStoreFixture> CreateProviderAsync()
        => await CreateProviderAsync([new WildcardTestSubscriber()]);

    private async Task<IStoreFixture> CreateProviderAsync(IOutboxSubscriber[] subscribers) =>
        await FixtureFactory.CreateAsync(
            _ct,
            services =>
            {
                foreach (var subscriber in subscribers)
                {
                    _ = services.AddSingleton(subscriber);
                }
                services.AddDsoRegistration<TestDso>();
                services.AddDsoRegistration<TestDso2>();
            });

    /// <summary>
    /// Wildcard subscriber that matches all entity types and event names, used to ensure
    /// outbox events are written to the store in tests.
    /// </summary>
    private sealed class WildcardTestSubscriber : IOutboxSubscriber
    {
        public SubscriberName SubscriberName => SubscriberName.Create("test-subscriber");
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames => new HashSet<OutboxEventName>();
        public IReadOnlySet<int> EntityTypeIds => new HashSet<int>();
    }

    /// <summary>
    /// Named wildcard subscriber for multi-subscriber fanout tests.
    /// </summary>
    private sealed class TestSubscriber(string name) : IOutboxSubscriber
    {
        public SubscriberName SubscriberName => SubscriberName.Create(name);
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames => new HashSet<OutboxEventName>();
        public IReadOnlySet<int> EntityTypeIds => new HashSet<int>();
    }
}
