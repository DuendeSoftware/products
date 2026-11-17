// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Internal.Pagination;
using Microsoft.Extensions.DependencyInjection;
using OutboxEventId = Duende.Storage.Internal.Outbox.OutboxEventId;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;
using SubscriberName = Duende.Storage.Internal.Outbox.SubscriberName;

namespace Duende.Storage.IntegrationTests;

public partial class PurgePoolTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static readonly SubscriberName WildcardSubscriberName =
        SubscriberName.Create("purge-pool-subscription");

    [Fact]
    public async Task purge_empty_pool_returns_zero_result()
    {
        await using var fixture = await CreateProviderAsync();

        var result = await fixture.DefaultCrossPartitionStorage.PurgePoolAsync(PoolId.Default, _ct);

        result.ShouldBe(PurgeResult.Empty);
        result.EntitiesDeleted.ShouldBe(0);
        result.EntityLinksDeleted.ShouldBe(0);
        result.OutboxEventsDeleted.ShouldBe(0);
    }

    [Fact]
    public async Task purge_removes_all_entities_in_pool()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var entityType = TestDso.DsoVersion.EntityType;
        var ids = new List<UuidV7>();

        // Insert 5 entities in the pool
        for (var i = 0; i < 5; i++)
        {
            var id = UuidV7.New();
            ids.Add(id);
            (await partitionedStorage.CreateAsync(id, new TestDso($"purge-all-{i}-{Guid.NewGuid()}"), [], [],
                Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        }

        var result = await fixture.DefaultCrossPartitionStorage.PurgePoolAsync(PoolId.Default, _ct);

        result.EntitiesDeleted.ShouldBe(5);

        // All entities should be gone
        foreach (var id in ids)
        {
            (await partitionedStorage.TryReadAsync(entityType, id, _ct)).Found.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task purge_does_not_affect_other_pools()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var entityType = TestDso.DsoVersion.EntityType;

        // Insert in pool 1 (default pool from fixture)
        var poolAId = UuidV7.New();
        (await partitionedStorage.CreateAsync(poolAId, new TestDso($"pool-a-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        // Switch to pool 2 and insert there
        partitionedStorage.SetPoolId(2);
        var poolBId = UuidV7.New();
        (await partitionedStorage.CreateAsync(poolBId, new TestDso($"pool-b-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        // Purge pool 2
        var result = await fixture.DefaultCrossPartitionStorage.PurgePoolAsync(2, _ct);
        result.EntitiesDeleted.ShouldBe(1);

        // Pool 2 entity should be gone
        (await partitionedStorage.TryReadAsync(entityType, poolBId, _ct)).Found.ShouldBeFalse();

        // Switch back to default — its entity must be untouched
        partitionedStorage.SetPoolId(PoolId.Default);
        (await partitionedStorage.TryReadAsync(entityType, poolAId, _ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task purge_removes_entities_in_every_data_category_of_the_pool()
    {
        await using var fixture = await CreateProviderAsync();
        PoolId poolId = 2;

        var categoryA = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Create("purge-category-a"), _ct);
        var categoryB = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Create("purge-category-b"), _ct);
        categoryA.SetPoolId(poolId);
        categoryB.SetPoolId(poolId);

        var idA = UuidV7.New();
        var idB = UuidV7.New();
        (await categoryA.CreateAsync(idA, new TestDso($"category-a-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await categoryB.CreateAsync(idB, new TestDso2($"category-b-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        (await categoryA.TryReadAsync(TestDso.DsoVersion.EntityType, idA, _ct)).Found.ShouldBeTrue();
        (await categoryB.TryReadAsync(TestDso2.DsoVersion.EntityType, idB, _ct)).Found.ShouldBeTrue();

        var result = await fixture.DefaultCrossPartitionStorage.PurgePoolAsync(poolId, _ct);

        result.EntitiesDeleted.ShouldBe(2);
        (await categoryA.TryReadAsync(TestDso.DsoVersion.EntityType, idA, _ct)).Found.ShouldBeFalse();
        (await categoryB.TryReadAsync(TestDso2.DsoVersion.EntityType, idB, _ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task purge_removes_entity_links_in_pool()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var testLink = new LinkDefinition
        {
            Left = TestDso.DsoVersion.EntityType,
            Right = TestDso2.DsoVersion.EntityType,
            Link = LinkTypeRegistry.MembershipRole
        };

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        (await partitionedStorage.CreateAsync(leftId, new TestDso($"link-left-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await partitionedStorage.CreateAsync(rightId, new TestDso2($"link-right-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await partitionedStorage.LinkAsync(testLink, leftId, rightId, [], _ct)).ShouldBe(LinkResult.Success);

        var result = await fixture.DefaultCrossPartitionStorage.PurgePoolAsync(PoolId.Default, _ct);

        result.EntitiesDeleted.ShouldBeGreaterThanOrEqualTo(2);
        result.EntityLinksDeleted.ShouldBeGreaterThanOrEqualTo(1);

        // Entities gone
        (await partitionedStorage.TryReadAsync(TestDso.DsoVersion.EntityType, leftId, _ct)).Found.ShouldBeFalse();
        (await partitionedStorage.TryReadAsync(TestDso2.DsoVersion.EntityType, rightId, _ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task purge_removes_outbox_events_in_pool()
    {
        await using var fixture = await CreateProviderWithSubscriptionAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        // Insert an entity with an outbox event attached
        var id = UuidV7.New();
        var evt = MakeEvent();
        (await partitionedStorage.CreateAsync(id, new TestDso($"outbox-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [evt], _ct)).ShouldBe(CreateResult.Success);

        // Verify outbox event is present before purge
        var before = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 100, _ct);
        before.Events.Count.ShouldBeGreaterThanOrEqualTo(1);

        var result = await fixture.DefaultCrossPartitionStorage.PurgePoolAsync(PoolId.Default, _ct);

        result.OutboxEventsDeleted.ShouldBeGreaterThanOrEqualTo(1);

        // Outbox should be empty after purge
        var after = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 100, _ct);
        after.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task purge_returns_correct_counts_per_table()
    {
        await using var fixture = await CreateProviderWithSubscriptionAsync();
        var partitionedStorage = fixture.PartitionedStorage;

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
            (await partitionedStorage.CreateAsync(id, new TestDso($"count-left-{i}-{Guid.NewGuid()}"), [], [],
                Expiration.NoExpiration, [evt], _ct)).ShouldBe(CreateResult.Success);
        }

        // Insert 2 entities of type TestDso2
        var rightIds = new List<UuidV7>();
        for (var i = 0; i < 2; i++)
        {
            var id = UuidV7.New();
            rightIds.Add(id);
            (await partitionedStorage.CreateAsync(id, new TestDso2($"count-right-{i}-{Guid.NewGuid()}"), [], [],
                Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        }

        // Create 2 links
        (await partitionedStorage.LinkAsync(testLink, leftIds[0], rightIds[0], [], _ct)).ShouldBe(LinkResult.Success);
        (await partitionedStorage.LinkAsync(testLink, leftIds[1], rightIds[1], [], _ct)).ShouldBe(LinkResult.Success);

        var result = await fixture.DefaultCrossPartitionStorage.PurgePoolAsync(PoolId.Default, _ct);

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
        var partitionedStorage = fixture.PartitionedStorage;

        var entityType = TestDso.DsoVersion.EntityType;
        var ids = new List<UuidV7>();

        // Insert 5 entities — more than batchSize=2
        for (var i = 0; i < 5; i++)
        {
            var id = UuidV7.New();
            ids.Add(id);
            (await partitionedStorage.CreateAsync(id, new TestDso($"batch-{i}-{Guid.NewGuid()}"), [], [],
                Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        }

        // Purge with batchSize=2 — exercises the internal loop
        var result = await fixture.DefaultCrossPartitionStorage.PurgePoolAsync(PoolId.Default, batchSize: 2, _ct);

        result.EntitiesDeleted.ShouldBe(5);

        // All entities must be gone regardless of batching
        foreach (var id in ids)
        {
            (await partitionedStorage.TryReadAsync(entityType, id, _ct)).Found.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task purge_with_invalid_batch_size_throws()
    {
        await using var fixture = await CreateProviderAsync();

        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => fixture.DefaultCrossPartitionStorage.PurgePoolAsync(PoolId.Default, batchSize: 0, _ct));
        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => fixture.DefaultCrossPartitionStorage.PurgePoolAsync(PoolId.Default, batchSize: -1, _ct));
    }

    [Fact]
    public async Task purge_removes_restricted_links_with_small_batch_sizeAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        (await partitionedStorage.CreateAsync(leftId, new TestDso($"restrict-left-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await partitionedStorage.CreateAsync(rightId, new TestDso2($"restrict-right-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await partitionedStorage.LinkAsync(TestLinkData.TestLinkRestrict, leftId, rightId, [], _ct)).ShouldBe(LinkResult.Success);

        var result = await fixture.DefaultCrossPartitionStorage.PurgePoolAsync(PoolId.Default, batchSize: 1, _ct);

        result.EntitiesDeleted.ShouldBe(2);
        result.EntityLinksDeleted.ShouldBe(1);
        (await partitionedStorage.TryReadAsync(TestDso.DsoVersion.EntityType, leftId, _ct)).Found.ShouldBeFalse();
        (await partitionedStorage.TryReadAsync(TestDso2.DsoVersion.EntityType, rightId, _ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task purge_reports_EntityLinksDeleted_summed_across_cascade_and_restrict_linksAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var cascadeLeft = UuidV7.New();
        var cascadeRight = UuidV7.New();
        var restrictLeft = UuidV7.New();
        var restrictRight = UuidV7.New();

        (await partitionedStorage.CreateAsync(cascadeLeft, new TestDso($"cascade-left-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await partitionedStorage.CreateAsync(cascadeRight, new TestDso2($"cascade-right-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await partitionedStorage.CreateAsync(restrictLeft, new TestDso($"restrict-left-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await partitionedStorage.CreateAsync(restrictRight, new TestDso2($"restrict-right-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        (await partitionedStorage.LinkAsync(TestLinkData.TestLink, cascadeLeft, cascadeRight, [], _ct)).ShouldBe(LinkResult.Success);
        (await partitionedStorage.LinkAsync(TestLinkData.TestLinkRestrict, restrictLeft, restrictRight, [], _ct)).ShouldBe(LinkResult.Success);

        var result = await fixture.DefaultCrossPartitionStorage.PurgePoolAsync(PoolId.Default, _ct);

        result.EntityLinksDeleted.ShouldBe(2);
    }

    // "hub" is the heavily linked right entity, which is:
    //  - the right side of 3 Restrict links (from 3 distinct left entities)
    //  - the right side of 3 Cascade links (from 3 distinct left entities)
    //  - the left side of 1 Cascade link to a "satellite" entity
    // With batchSize = 1, entity batches go out in PK order (pool_id, entity_type_id, entity_id), so
    // whichever entity type sorts first could be selected for deletion while it still has links
    // pointing at or from it, which must not cause a foreign-key violation that rolls back the purge.
    // Covers both PK orderings of TestDso (type 99) and TestDso2 (type 100): the hub is the lower
    // entity type when reversed is true, and the higher one when reversed is false.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task purge_with_batch_size_one_drains_all_links_to_a_heavily_linked_right_entity_before_deleting_entitiesAsync(bool reversed)
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var cascadeLink = reversed ? TestLinkData.TestLink2 : TestLinkData.TestLink;
        var cascadeLinkToHub = reversed ? TestLinkData.TestLink : TestLinkData.TestLink2;
        var restrictLink = reversed ? TestLinkData.TestLinkRestrict2 : TestLinkData.TestLinkRestrict;
        var hubEntityType = reversed ? TestDso.DsoVersion.EntityType : TestDso2.DsoVersion.EntityType;
        var leftEntityType = reversed ? TestDso2.DsoVersion.EntityType : TestDso.DsoVersion.EntityType;

        var hub = UuidV7.New();
        (await CreateHubOrLeftAsync(partitionedStorage, hub, reversed, $"hub-{Guid.NewGuid()}", _ct)).ShouldBe(CreateResult.Success);

        var satellite = UuidV7.New();
        (await CreateHubOrLeftAsync(partitionedStorage, satellite, !reversed, $"satellite-{Guid.NewGuid()}", _ct)).ShouldBe(CreateResult.Success);
        (await partitionedStorage.LinkAsync(cascadeLinkToHub, hub, satellite, [], _ct)).ShouldBe(LinkResult.Success);

        var restrictedLefts = new List<UuidV7>();
        for (var i = 0; i < 3; i++)
        {
            var leftId = UuidV7.New();
            restrictedLefts.Add(leftId);
            (await CreateHubOrLeftAsync(partitionedStorage, leftId, !reversed, $"restrict-left-{i}-{Guid.NewGuid()}", _ct)).ShouldBe(CreateResult.Success);
            (await partitionedStorage.LinkAsync(restrictLink, leftId, hub, [], _ct)).ShouldBe(LinkResult.Success);
        }

        var cascadeLefts = new List<UuidV7>();
        for (var i = 0; i < 3; i++)
        {
            var leftId = UuidV7.New();
            cascadeLefts.Add(leftId);
            (await CreateHubOrLeftAsync(partitionedStorage, leftId, !reversed, $"cascade-left-{i}-{Guid.NewGuid()}", _ct)).ShouldBe(CreateResult.Success);
            (await partitionedStorage.LinkAsync(cascadeLink, leftId, hub, [], _ct)).ShouldBe(LinkResult.Success);
        }

        // 3 restrict + 3 cascade + 1 (hub -> satellite) links exist in the pool.
        const int expectedLinkCount = 7;
        const int expectedEntityCount = 8; // hub + satellite + 3 restrict lefts + 3 cascade lefts

        var result = await fixture.DefaultCrossPartitionStorage.PurgePoolAsync(PoolId.Default, batchSize: 1, _ct);

        result.EntityLinksDeleted.ShouldBe(expectedLinkCount);
        result.EntitiesDeleted.ShouldBe(expectedEntityCount);

        (await partitionedStorage.TryReadAsync(hubEntityType, hub, _ct)).Found.ShouldBeFalse();
        (await partitionedStorage.TryReadAsync(leftEntityType, satellite, _ct)).Found.ShouldBeFalse();
        foreach (var leftId in restrictedLefts.Concat(cascadeLefts))
        {
            (await partitionedStorage.TryReadAsync(leftEntityType, leftId, _ct)).Found.ShouldBeFalse();
        }
    }

    private static Task<CreateResult> CreateHubOrLeftAsync(IPartitionedStorage storage, UuidV7 id, bool asTestDso, string value, Ct ct) =>
        asTestDso
            ? storage.CreateAsync(id, new TestDso(value), [], [], Expiration.NoExpiration, [], ct)
            : storage.CreateAsync(id, new TestDso2(value), [], [], Expiration.NoExpiration, [], ct);

    [Fact]
    public async Task purge_removes_keys_and_search_values_so_key_can_be_reused()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var key = new TestJsonKeyDsk($"purge-pool-key-{Guid.NewGuid()}");
        var searchFieldsBuilder = new SearchFieldsBuilder();
        _ = searchFieldsBuilder.Add("tag", "purge-pool-searchable");
        var searchFields = searchFieldsBuilder.Build();

        (await partitionedStorage.CreateAsync(id, new TestDso($"keyed-{Guid.NewGuid()}"),
            [DataStorageKey.Create(key)], searchFields, Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);

        // Sanity check: the key and search field resolve before purge.
        (await partitionedStorage.TryReadAsync(TestDso.DsoVersion.EntityType, DataStorageKey.Create(key), _ct)).Found.ShouldBeTrue();
        var filter = new StringField("tag").Equals("purge-pool-searchable");
        (await partitionedStorage.QueryAsync(TestDso.DsoVersion.EntityType, filter, SortParameter.Empty, DataRange.FromPage(1, 100), _ct))
            .Items.Count.ShouldBe(1);

        var result = await fixture.DefaultCrossPartitionStorage.PurgePoolAsync(PoolId.Default, _ct);
        result.EntitiesDeleted.ShouldBeGreaterThanOrEqualTo(1);

        // Entity gone.
        (await partitionedStorage.TryReadAsync(TestDso.DsoVersion.EntityType, id, _ct)).Found.ShouldBeFalse();
        // Key lookup no longer resolves.
        (await partitionedStorage.TryReadAsync(TestDso.DsoVersion.EntityType, DataStorageKey.Create(key), _ct)).Found.ShouldBeFalse();
        // Search filter no longer matches.
        (await partitionedStorage.QueryAsync(TestDso.DsoVersion.EntityType, filter, SortParameter.Empty, DataRange.FromPage(1, 100), _ct))
            .Items.ShouldBeEmpty();

        // Recreate the same id without search fields: orphaned search values would make the filter match again.
        (await partitionedStorage.CreateAsync(id, new TestDso($"recreated-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await partitionedStorage.QueryAsync(TestDso.DsoVersion.EntityType, filter, SortParameter.Empty, DataRange.FromPage(1, 100), _ct))
            .Items.ShouldBeEmpty();

        // The key can be reused in the (now-purged) pool — proves the entity_keys row is gone.
        var newId = UuidV7.New();
        (await partitionedStorage.CreateAsync(newId, new TestDso($"reused-key-{Guid.NewGuid()}"),
            [DataStorageKey.Create(key)], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);
        (await partitionedStorage.TryReadAsync(TestDso.DsoVersion.EntityType, DataStorageKey.Create(key), _ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task purge_called_again_on_already_purged_pool_returns_empty_result()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var testLink = new LinkDefinition
        {
            Left = TestDso.DsoVersion.EntityType,
            Right = TestDso2.DsoVersion.EntityType,
            Link = LinkTypeRegistry.MembershipRole
        };

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();
        (await partitionedStorage.CreateAsync(leftId, new TestDso($"idempotent-left-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await partitionedStorage.CreateAsync(rightId, new TestDso2($"idempotent-right-{Guid.NewGuid()}"), [], [],
            Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await partitionedStorage.LinkAsync(testLink, leftId, rightId, [], _ct)).ShouldBe(LinkResult.Success);

        var firstResult = await fixture.DefaultCrossPartitionStorage.PurgePoolAsync(PoolId.Default, _ct);
        firstResult.EntitiesDeleted.ShouldBeGreaterThanOrEqualTo(2);

        var secondResult = await fixture.DefaultCrossPartitionStorage.PurgePoolAsync(PoolId.Default, _ct);
        secondResult.ShouldBe(PurgeResult.Empty);
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

    private async Task<IStorageFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });

    private async Task<IStorageFixture> CreateProviderWithSubscriptionAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            _ = services.AddSingleton<IOutboxSubscription>(new WildcardTestSubscription());
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });

    /// <summary>
    /// Wildcard subscription that matches all entity types and event names,
    /// used to ensure outbox events are written to the storage in tests.
    /// </summary>
    private sealed class WildcardTestSubscription : IOutboxSubscription
    {
        public SubscriberName SubscriberName => WildcardSubscriberName;
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames { get; } = new HashSet<OutboxEventName>();
        public IReadOnlySet<int> EntityTypeIds { get; } = new HashSet<int>();
    }
}
