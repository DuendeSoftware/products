// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Outbox;
using Microsoft.Extensions.DependencyInjection;
using OutboxEventId = Duende.Storage.Internal.Outbox.OutboxEventId;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;
using SubscriberName = Duende.Storage.Internal.Outbox.SubscriberName;

namespace Duende.Storage.IntegrationTests;

public partial class PurgePoolTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static readonly SubscriberName WildcardSubscriberName =
        SubscriberName.Create("purge-pool-subscriber");

    [Fact]
    public async Task purge_empty_pool_returns_zero_result()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var result = await store.PurgePoolAsync(_ct);

        result.ShouldBe(PurgeResult.Empty);
        result.EntitiesDeleted.ShouldBe(0);
        result.EntityLinksDeleted.ShouldBe(0);
        result.OutboxEventsDeleted.ShouldBe(0);
    }

    [Fact]
    public async Task purge_removes_all_entities_in_pool()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var entityType = TestDso.DsoVersion.EntityType;
        var ids = new List<UuidV7>();

        // Insert 5 entities in the pool
        for (var i = 0; i < 5; i++)
        {
            var id = UuidV7.New();
            ids.Add(id);
            (await store.CreateAsync(id, new TestDso($"purge-all-{i}-{Guid.NewGuid()}"), [], [],
                Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        }

        var result = await store.PurgePoolAsync(_ct);

        result.EntitiesDeleted.ShouldBe(5);

        // All entities should be gone
        foreach (var id in ids)
        {
            (await store.TryReadAsync(entityType, id, _ct)).Found.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task purge_does_not_affect_other_pools()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var entityType = TestDso.DsoVersion.EntityType;

        // Insert in pool 1 (default pool from fixture)
        var poolAId = UuidV7.New();
        (await store.CreateAsync(poolAId, new TestDso($"pool-a-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        // Switch to pool 2 and insert there
        store.SetPoolId(2);
        var poolBId = UuidV7.New();
        (await store.CreateAsync(poolBId, new TestDso($"pool-b-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        // Purge pool 2
        var result = await store.PurgePoolAsync(_ct);
        result.EntitiesDeleted.ShouldBe(1);

        // Pool 2 entity should be gone
        (await store.TryReadAsync(entityType, poolBId, _ct)).Found.ShouldBeFalse();

        // Switch back to pool 1 — its entity must be untouched
        store.SetPoolId(1);
        (await store.TryReadAsync(entityType, poolAId, _ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task purge_removes_entity_links_in_pool()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var testLink = new LinkDefinition
        {
            Left = TestDso.DsoVersion.EntityType,
            Right = TestDso2.DsoVersion.EntityType,
            Link = LinkTypeRegistry.MembershipRole
        };

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        (await store.CreateAsync(leftId, new TestDso($"link-left-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await store.CreateAsync(rightId, new TestDso2($"link-right-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await store.LinkAsync(testLink, leftId, rightId, [], _ct)).ShouldBe(LinkResult.Success);

        var result = await store.PurgePoolAsync(_ct);

        result.EntitiesDeleted.ShouldBeGreaterThanOrEqualTo(2);
        result.EntityLinksDeleted.ShouldBeGreaterThanOrEqualTo(1);

        // Entities gone
        (await store.TryReadAsync(TestDso.DsoVersion.EntityType, leftId, _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(TestDso2.DsoVersion.EntityType, rightId, _ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task purge_removes_outbox_events_in_pool()
    {
        await using var fixture = await CreateProviderWithSubscriberAsync();
        var store = fixture.Store;

        // Insert an entity with an outbox event attached
        var id = UuidV7.New();
        var evt = MakeEvent();
        (await store.CreateAsync(id, new TestDso($"outbox-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [evt], _ct)).ShouldBe(CreateResult.Success);

        // Verify outbox event is present before purge
        var before = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 100, _ct);
        before.Events.Count.ShouldBeGreaterThanOrEqualTo(1);

        var result = await store.PurgePoolAsync(_ct);

        result.OutboxEventsDeleted.ShouldBeGreaterThanOrEqualTo(1);

        // Outbox should be empty after purge
        var after = await store.GetOutboxEventsForSubscriberAsync(WildcardSubscriberName, 100, _ct);
        after.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task purge_returns_correct_counts_per_table()
    {
        await using var fixture = await CreateProviderWithSubscriberAsync();
        var store = fixture.Store;

        var testLink = new LinkDefinition
        {
            Left = TestDso.DsoVersion.EntityType,
            Right = TestDso2.DsoVersion.EntityType,
            Link = LinkTypeRegistry.MembershipRole
        };

        // Insert 3 entities of type TestDso
        var leftIds = new List<UuidV7>();
        for (var i = 0; i < 3; i++)
        {
            var id = UuidV7.New();
            leftIds.Add(id);
            var evt = MakeEvent();
            (await store.CreateAsync(id, new TestDso($"count-left-{i}-{Guid.NewGuid()}"), [], [],
                Expiration.NoExpiration, [evt], _ct)).ShouldBe(CreateResult.Success);
        }

        // Insert 2 entities of type TestDso2
        var rightIds = new List<UuidV7>();
        for (var i = 0; i < 2; i++)
        {
            var id = UuidV7.New();
            rightIds.Add(id);
            (await store.CreateAsync(id, new TestDso2($"count-right-{i}-{Guid.NewGuid()}"), [], [],
                Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        }

        // Create 2 links
        (await store.LinkAsync(testLink, leftIds[0], rightIds[0], [], _ct)).ShouldBe(LinkResult.Success);
        (await store.LinkAsync(testLink, leftIds[1], rightIds[1], [], _ct)).ShouldBe(LinkResult.Success);

        var result = await store.PurgePoolAsync(_ct);

        // 3 TestDso + 2 TestDso2 = 5 entities
        result.EntitiesDeleted.ShouldBe(5);
        // 2 links
        result.EntityLinksDeleted.ShouldBe(2);
        // 3 outbox events (one per TestDso create)
        result.OutboxEventsDeleted.ShouldBe(3);
    }

    [Fact]
    public async Task purge_with_small_batch_size_completes_correctly()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var entityType = TestDso.DsoVersion.EntityType;
        var ids = new List<UuidV7>();

        // Insert 5 entities — more than batchSize=2
        for (var i = 0; i < 5; i++)
        {
            var id = UuidV7.New();
            ids.Add(id);
            (await store.CreateAsync(id, new TestDso($"batch-{i}-{Guid.NewGuid()}"), [], [],
                Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        }

        // Purge with batchSize=2 — exercises the internal loop
        var result = await store.PurgePoolAsync(batchSize: 2, _ct);

        result.EntitiesDeleted.ShouldBe(5);

        // All entities must be gone regardless of batching
        foreach (var id in ids)
        {
            (await store.TryReadAsync(entityType, id, _ct)).Found.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task purge_with_invalid_batch_size_throws()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => store.PurgePoolAsync(batchSize: 0, _ct));
        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => store.PurgePoolAsync(batchSize: -1, _ct));
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

    private async Task<IStoreFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });

    private async Task<IStoreFixture> CreateProviderWithSubscriberAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            _ = services.AddSingleton<IOutboxSubscriber>(new WildcardTestSubscriber());
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });

    /// <summary>
    /// Wildcard subscriber that matches all entity types and event names,
    /// used to ensure outbox events are written to the store in tests.
    /// </summary>
    private sealed class WildcardTestSubscriber : IOutboxSubscriber
    {
        public SubscriberName SubscriberName => WildcardSubscriberName;
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames { get; } = new HashSet<OutboxEventName>();
        public IReadOnlySet<int> EntityTypeIds { get; } = new HashSet<int>();
    }
}
