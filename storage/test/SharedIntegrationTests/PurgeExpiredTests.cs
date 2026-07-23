// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Pagination;
using Microsoft.Extensions.DependencyInjection;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;
using SubscriberName = Duende.Storage.Internal.Outbox.SubscriberName;

namespace Duende.Storage.IntegrationTests;

public partial class PurgeExpiredTests
{


    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static readonly SubscriberName WildcardSubscriberName =
        SubscriberName.Create("test-subscriber");

    [Fact]
    public async Task PurgeExpired_should_remove_expired_entities()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;

        var entityType = TestDso.DsoVersion.EntityType;
        var ids = new List<UuidV7>();

        // Create 5 entities that will expire
        for (var i = 0; i < 5; i++)
        {
            var id = UuidV7.New();
            ids.Add(id);
            (await store.CreateAsync(id, new TestDso($"purge-{i}-{Guid.NewGuid()}"), [], [],
                Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct)).ShouldBe(CreateResult.Success);
        }

        // Create 2 that won't expire
        var persistId1 = UuidV7.New();
        var persistId2 = UuidV7.New();
        (await store.CreateAsync(persistId1, new TestDso($"persist-1-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await store.CreateAsync(persistId2, new TestDso($"persist-2-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        // Advance time past expiration
        tp.Advance(TimeSpan.FromHours(2));

        var purged = await store.PurgeExpiredAsync(batchSize: 100, _ct);

        purged.ShouldBeGreaterThanOrEqualTo(5);

        // Expired entities should be gone
        foreach (var id in ids)
        {
            (await store.TryReadAsync(entityType, id, _ct)).Found.ShouldBeFalse();
        }

        // Persistent entities should remain
        (await store.TryReadAsync(entityType, persistId1, _ct)).Found.ShouldBeTrue();
        (await store.TryReadAsync(entityType, persistId2, _ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task PurgeExpired_with_no_expired_records_should_return_zero()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;

        // Create an entity that won't expire
        (await store.CreateAsync(UuidV7.New(), new TestDso($"alive-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        var purged = await store.PurgeExpiredAsync(batchSize: 100, _ct);

        purged.ShouldBe(0);
    }

    [Fact]
    public async Task PurgeExpired_single_batch_should_respect_size()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;

        var entityType = TestDso.DsoVersion.EntityType;

        // Create 10 entities that will expire
        var ids = new List<UuidV7>();
        for (var i = 0; i < 10; i++)
        {
            var id = UuidV7.New();
            ids.Add(id);
            (await store.CreateAsync(id, new TestDso($"multi-batch-{i}-{Guid.NewGuid()}"), [], [],
                Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct)).ShouldBe(CreateResult.Success);
        }

        tp.Advance(TimeSpan.FromHours(2));

        // PurgeExpired only processes a single batch — with batchSize 3, it should purge at most 3
        var purged = await store.PurgeExpiredAsync(batchSize: 3, _ct);
        purged.ShouldBeLessThanOrEqualTo(3);
        purged.ShouldBeGreaterThan(0);

        // Iterate until all expired entities are purged (simulating what the job does)
        var totalPurged = purged;
        while (purged > 0)
        {
            purged = await store.PurgeExpiredAsync(batchSize: 3, _ct);
            totalPurged += purged;
        }

        totalPurged.ShouldBeGreaterThanOrEqualTo(10);

        // All expired entities should be gone
        foreach (var id in ids)
        {
            (await store.TryReadAsync(entityType, id, _ct)).Found.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task PurgeExpired_should_also_remove_links()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        var testLink = new LinkDefinition
        {
            Left = TestDso.DsoVersion.EntityType,
            Right = TestDso2.DsoVersion.EntityType,
            Link = LinkTypeRegistry.MembershipRole
        };

        // Create two entities and link them; left expires in 1 hour
        (await store.CreateAsync(leftId, new TestDso($"left-{Guid.NewGuid()}"), [], [],
            Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct)).ShouldBe(CreateResult.Success);
        (await store.CreateAsync(rightId, new TestDso2($"right-{Guid.NewGuid()}"), [], [],
            Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct)).ShouldBe(CreateResult.Success);
        (await store.LinkAsync(testLink, leftId, rightId, [], _ct)).ShouldBe(LinkResult.Success);

        // Verify the link exists
        var query = LinkQuery.From(TestDso2.DsoVersion.EntityType)
            .Join(testLink)
            .Where(TestDso.DsoVersion.EntityType, leftId)
            .Build();
        var before = await queryStore.QueryLinksAsync<TestDso2>(query, DataRange.FromPage(1, 100), _ct);
        before.Items.Count.ShouldBe(1);

        // Advance past expiration and purge
        tp.Advance(TimeSpan.FromHours(2));
        var purged = await store.PurgeExpiredAsync(batchSize: 100, _ct);
        purged.ShouldBeGreaterThanOrEqualTo(1);

        // Left entity gone
        (await store.TryReadAsync(TestDso.DsoVersion.EntityType, leftId, _ct)).Found.ShouldBeFalse();

        // Link should be gone too
        var after = await queryStore.QueryLinksAsync<TestDso2>(query, DataRange.FromPage(1, 100), _ct);
        after.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task PurgeExpired_should_not_delete_entity_whose_ttl_was_extended()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;

        var id = UuidV7.New();

        // Create entity that expires in 1 hour
        (await store.CreateAsync(id, new TestDso($"ttl-race-{Guid.NewGuid()}"), [], [],
            Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct)).ShouldBe(CreateResult.Success);

        // Advance past expiry
        tp.Advance(TimeSpan.FromHours(2));

        // Extend the entity's TTL before purge runs
        var entityType = TestDso.DsoVersion.EntityType;
        var version = (await store.TryReadAsync(entityType, id, _ct)).Version.ShouldNotBeNull();
        (await store.UpdateAsync(id, new TestDso($"ttl-extended-{Guid.NewGuid()}"), version, [], [],
            Expiration.InRelative(TimeSpan.FromHours(5)), [], _ct)).ShouldBe(UpdateResult.Success);

        // PurgeExpired should skip this entity because expires_at is now in the future
        var purged = await store.PurgeExpiredAsync(batchSize: 100, _ct);
        purged.ShouldBe(0);

        // Entity should still exist
        (await store.TryReadAsync(entityType, id, _ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task PurgeExpired_should_not_delete_links_for_entity_whose_ttl_was_extended()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        var testLink = new LinkDefinition
        {
            Left = TestDso.DsoVersion.EntityType,
            Right = TestDso2.DsoVersion.EntityType,
            Link = LinkTypeRegistry.MembershipRole
        };

        // Create two entities and link them; left expires in 1 hour
        (await store.CreateAsync(leftId, new TestDso($"left-{Guid.NewGuid()}"), [], [],
            Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct)).ShouldBe(CreateResult.Success);
        (await store.CreateAsync(rightId, new TestDso2($"right-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await store.LinkAsync(testLink, leftId, rightId, [], _ct)).ShouldBe(LinkResult.Success);

        // Advance past expiry
        tp.Advance(TimeSpan.FromHours(2));

        // Extend the left entity's TTL before purge runs
        var version = (await store.TryReadAsync(TestDso.DsoVersion.EntityType, leftId, _ct)).Version.ShouldNotBeNull();
        (await store.UpdateAsync(leftId, new TestDso($"extended-{Guid.NewGuid()}"), version, [], [],
            Expiration.InRelative(TimeSpan.FromHours(5)), [], _ct)).ShouldBe(UpdateResult.Success);

        // PurgeExpired should skip this entity — TTL was extended
        var purged = await store.PurgeExpiredAsync(batchSize: 100, _ct);
        purged.ShouldBe(0);

        // Left entity should still exist
        (await store.TryReadAsync(TestDso.DsoVersion.EntityType, leftId, _ct)).Found.ShouldBeTrue();

        // Link should still exist
        var query = LinkQuery.From(TestDso2.DsoVersion.EntityType)
            .Join(testLink)
            .Where(TestDso.DsoVersion.EntityType, leftId)
            .Build();
        var after = await queryStore.QueryLinksAsync<TestDso2>(query, DataRange.FromPage(1, 100), _ct);
        after.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task PurgeExpired_should_write_EntityExpired_outbox_events_when_subscriber_matches()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp, [new WildcardTestSubscriber()]);
        var store = fixture.Store;

        var entityType = TestDso.DsoVersion.EntityType;
        var ids = new List<UuidV7>();

        for (var i = 0; i < 3; i++)
        {
            var id = UuidV7.New();
            ids.Add(id);
            (await store.CreateAsync(id, new TestDso($"outbox-{i}-{Guid.NewGuid()}"), [], [],
                Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct)).ShouldBe(CreateResult.Success);
        }

        tp.Advance(TimeSpan.FromHours(2));
        var purged = await store.PurgeExpiredAsync(batchSize: 100, _ct);
        purged.ShouldBeGreaterThanOrEqualTo(3);

        var page = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 100, _ct);
        page.Events.Count.ShouldBe(3);

        foreach (var id in ids)
        {
            var matching = page.Events.Where(e => e.SubjectId == UuidV7.From(id.Value)).ToList();
            matching.Count.ShouldBe(1);
            var evt = matching[0];
            evt.EventName.ShouldBe(OutboxEventName.EntityExpired);
            evt.EntityTypeId.ShouldBe((int)entityType.Id);
            evt.Payload.ShouldNotBeNullOrEmpty();
            (evt.Dso is TestDso).ShouldBeTrue();

            // Entity should be deleted
            (await store.TryReadAsync(entityType, id, _ct)).Found.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task PurgeExpired_should_not_write_outbox_events_when_outbox_disabled()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        // Use default CreateServiceProvider (no outbox) — outbox is disabled by default
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;

        for (var i = 0; i < 3; i++)
        {
            (await store.CreateAsync(UuidV7.New(), new TestDso($"no-outbox-{i}-{Guid.NewGuid()}"), [], [],
                Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct)).ShouldBe(CreateResult.Success);
        }

        tp.Advance(TimeSpan.FromHours(2));
        var purged = await store.PurgeExpiredAsync(batchSize: 100, _ct);
        purged.ShouldBeGreaterThanOrEqualTo(3);

        var page = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 100, _ct);
        page.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task PurgeExpired_should_only_write_outbox_events_for_matching_entity_types()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var testDsoTypeId = (int)TestDso.DsoVersion.EntityType.Id;
        await using var fixture = await CreateProviderAsync(tp,
            [new TypeFilteredTestSubscriber("typed-sub", [testDsoTypeId])]);
        var store = fixture.Store;

        // Create entities of both types with expiration
        var testDsoId = UuidV7.New();
        var testDso2Id = UuidV7.New();
        (await store.CreateAsync(testDsoId, new TestDso($"typed-{Guid.NewGuid()}"), [], [],
            Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct)).ShouldBe(CreateResult.Success);
        (await store.CreateAsync(testDso2Id, new TestDso2($"typed2-{Guid.NewGuid()}"), [], [],
            Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct)).ShouldBe(CreateResult.Success);

        tp.Advance(TimeSpan.FromHours(2));
        var purged = await store.PurgeExpiredAsync(batchSize: 100, _ct);
        purged.ShouldBeGreaterThanOrEqualTo(2);

        // Both entities should be deleted
        (await store.TryReadAsync(TestDso.DsoVersion.EntityType, testDsoId, _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(TestDso2.DsoVersion.EntityType, testDso2Id, _ct)).Found.ShouldBeFalse();

        // Outbox should only have events for TestDso, not TestDso2
        var page = await store.GetOutboxEventsForSubscriberAsync(SubscriberName.Create("typed-sub"), 100, _ct);
        page.Events.Count.ShouldBe(1);
        page.Events[0].EntityTypeId.ShouldBe(testDsoTypeId);
        page.Events[0].SubjectId.ShouldBe(UuidV7.From(testDsoId.Value));
    }

    [Fact]
    public async Task PurgeExpired_should_not_write_outbox_events_when_no_subscribers()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        // Outbox enabled but NO subscribers
        await using var fixture = await CreateProviderAsync(tp, []);
        var store = fixture.Store;

        for (var i = 0; i < 2; i++)
        {
            (await store.CreateAsync(UuidV7.New(), new TestDso($"no-sub-{i}-{Guid.NewGuid()}"), [], [],
                Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct)).ShouldBe(CreateResult.Success);
        }

        tp.Advance(TimeSpan.FromHours(2));
        var purged = await store.PurgeExpiredAsync(batchSize: 100, _ct);
        purged.ShouldBeGreaterThanOrEqualTo(2);

        var page = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 100, _ct);
        page.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task PurgeExpired_should_fanout_to_multiple_subscribers()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp,
            [new NamedTestSubscriber("sub-a"), new NamedTestSubscriber("sub-b")]);
        var store = fixture.Store;

        for (var i = 0; i < 2; i++)
        {
            var id = UuidV7.New();
            (await store.CreateAsync(id, new TestDso($"fanout-{i}-{Guid.NewGuid()}"), [], [],
                Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct)).ShouldBe(CreateResult.Success);
        }

        tp.Advance(TimeSpan.FromHours(2));
        var purged = await store.PurgeExpiredAsync(batchSize: 100, _ct);
        purged.ShouldBeGreaterThanOrEqualTo(2);

        var pageA = await store.GetOutboxEventsForSubscriberAsync(SubscriberName.Create("sub-a"), 100, _ct);
        var pageB = await store.GetOutboxEventsForSubscriberAsync(SubscriberName.Create("sub-b"), 100, _ct);
        var allEvents = pageA.Events.Concat(pageB.Events).ToList();
        allEvents.Count.ShouldBe(4); // 2 entities × 2 subscribers

        // Each subscriber should appear exactly twice
        var subACounts = allEvents.Count(e => e.SubscriberName == SubscriberName.Create("sub-a"));
        var subBCounts = allEvents.Count(e => e.SubscriberName == SubscriberName.Create("sub-b"));
        subACounts.ShouldBe(2);
        subBCounts.ShouldBe(2);

        // Each message should have a unique MessageId
        allEvents.Select(e => e.MessageId).Distinct().Count().ShouldBe(4);
    }

    [Fact]
    public async Task PurgeExpired_should_share_EventId_across_subscribers_for_same_entity()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateProviderAsync(tp,
            [new NamedTestSubscriber("sub-x"), new NamedTestSubscriber("sub-y")]);
        var store = fixture.Store;

        var id1 = UuidV7.New();
        var id2 = UuidV7.New();
        (await store.CreateAsync(id1, new TestDso($"eid-{Guid.NewGuid()}"), [], [],
            Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct)).ShouldBe(CreateResult.Success);
        (await store.CreateAsync(id2, new TestDso($"eid-{Guid.NewGuid()}"), [], [],
            Expiration.InRelative(TimeSpan.FromHours(1)), [], _ct)).ShouldBe(CreateResult.Success);

        tp.Advance(TimeSpan.FromHours(2));
        var purged = await store.PurgeExpiredAsync(batchSize: 100, _ct);
        purged.ShouldBeGreaterThanOrEqualTo(2);

        var pageX = await store.GetOutboxEventsForSubscriberAsync(SubscriberName.Create("sub-x"), 100, _ct);
        var pageY = await store.GetOutboxEventsForSubscriberAsync(SubscriberName.Create("sub-y"), 100, _ct);
        var allEvents = pageX.Events.Concat(pageY.Events).ToList();
        allEvents.Count.ShouldBe(4); // 2 entities × 2 subscribers

        // Group events by subject (entity) — each entity's events should share the same EventId
        var bySubject = allEvents.GroupBy(e => e.SubjectId).ToList();
        bySubject.Count.ShouldBe(2);
        foreach (var group in bySubject)
        {
            var eventIds = group.Select(e => e.EventId).Distinct().ToList();
            eventIds.Count.ShouldBe(1, $"All subscriber rows for subject {group.Key} should share the same EventId");
        }

        // The two entities should have different EventIds
        var allEventIds = bySubject.Select(g => g.First().EventId).Distinct().ToList();
        allEventIds.Count.ShouldBe(2, "Different entities should have different EventIds");

        // MessageIds must all be unique
        allEvents.Select(e => e.MessageId).Distinct().Count().ShouldBe(4);
    }

    private async Task<IStoreFixture> CreateProviderAsync(FakeTimeProvider tp) =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            _ = services.AddSingleton(tp);
            _ = services.AddSingleton<TimeProvider>(tp);
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });

    private async Task<IStoreFixture> CreateProviderAsync(FakeTimeProvider tp, IOutboxSubscriber[] subscribers) =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            _ = services.AddSingleton(tp);
            _ = services.AddSingleton<TimeProvider>(tp);
            foreach (var subscriber in subscribers)
            {
                _ = services.AddSingleton(subscriber);
            }
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });

    /// <summary>
    /// Wildcard subscriber that matches all entity types and event names.
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
    private sealed class NamedTestSubscriber(string name) : IOutboxSubscriber
    {
        public SubscriberName SubscriberName => SubscriberName.Create(name);
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames => new HashSet<OutboxEventName>();
        public IReadOnlySet<int> EntityTypeIds => new HashSet<int>();
    }

    /// <summary>
    /// Subscriber filtered to specific entity type IDs.
    /// </summary>
    private sealed class TypeFilteredTestSubscriber(string name, int[] entityTypeIds) : IOutboxSubscriber
    {
        public SubscriberName SubscriberName => SubscriberName.Create(name);
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames => new HashSet<OutboxEventName>();
        public IReadOnlySet<int> EntityTypeIds => new HashSet<int>(entityTypeIds);
    }
}
