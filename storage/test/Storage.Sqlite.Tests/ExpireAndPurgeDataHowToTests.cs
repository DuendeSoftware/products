// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Sqlite;

public sealed class ExpireAndPurgeDataHowToTests
{
    // begin-snippet: expire-session-token-dso
    public static class SessionTokenDso
    {
        // Illustrative id only. Real DSOs must use an unused id from the owning product's reserved range.
        internal static readonly EntityType EntityType = new(7001, nameof(SessionTokenDso));

        public sealed record V1(string Subject, string ClientId) : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
        }
    }
    // end-snippet

    private static async Task<IStorageFixture> CreateFixtureAsync(FakeTimeProvider tp, Ct ct) =>
        await new SqliteStorageEngineFixtureFactory().CreateAsync(ct, services =>
        {
            _ = services.AddSingleton<TimeProvider>(tp);
            services.AddDsoRegistration<SessionTokenDso.V1>();
        });

    private static Task<CreateResult> CreateSessionTokenAsync(
        IPartitionedStorage storage, UuidV7 id, SessionTokenDso.V1 token, TimeSpan lifetime, Ct ct) =>
        storage.CreateAsync(id, token, [], SearchFieldCollection.Empty, Expiration.InRelative(lifetime), [], ct);

    [Fact]
    public async Task Creating_with_a_past_expiration_is_a_noop()
    {
        var ct = TestContext.Current.CancellationToken;
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateFixtureAsync(tp, ct);
        var storage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var token = new SessionTokenDso.V1("alice", "spa-client");

        // A lifetime that's already in the past resolves to an expiration before "now".
        var createResult = await storage.CreateAsync(
            id, token, [], SearchFieldCollection.Empty, Expiration.AtAbsolute(tp.GetUtcNow().AddMinutes(-1)), [], ct);

        createResult.ShouldBe(CreateResult.Success);
        (await storage.TryReadAsync(SessionTokenDso.EntityType, id, ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task Update_can_extend_the_expiration()
    {
        var ct = TestContext.Current.CancellationToken;
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateFixtureAsync(tp, ct);
        var storage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var token = new SessionTokenDso.V1("alice", "spa-client");

        // begin-snippet: expire-create-with-relative-ttl
        // Short-lived operational data: the token is only useful until it expires, so give it
        // a lifetime up front instead of deleting it explicitly once the session ends.
        var createResult = await storage.CreateAsync(
            id, token, [], SearchFieldCollection.Empty, Expiration.InRelative(TimeSpan.FromMinutes(30)), [], ct);
        // end-snippet
        createResult.ShouldBe(CreateResult.Success);
        var version = (await storage.TryReadAsync(SessionTokenDso.EntityType, id, ct)).Version.ShouldNotBeNull();

        // Extend well past the original 30-minute lifetime.
        // begin-snippet: expire-update-extend
        // A non-null Expiration replaces whatever expiration the entity had before.
        var extendResult = await storage.UpdateAsync(
            id, token, version, [], SearchFieldCollection.Empty, Expiration.InRelative(TimeSpan.FromHours(1)), [], ct);
        // end-snippet
        extendResult.ShouldBe(UpdateResult.Success);

        tp.Advance(TimeSpan.FromMinutes(45));

        // The original expiration would have passed by now; the extended one hasn't.
        (await storage.TryReadAsync(SessionTokenDso.EntityType, id, ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_with_NoExpiration_clears_the_expiration_permanently()
    {
        var ct = TestContext.Current.CancellationToken;
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateFixtureAsync(tp, ct);
        var storage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var token = new SessionTokenDso.V1("alice", "spa-client");
        _ = await CreateSessionTokenAsync(storage, id, token, TimeSpan.FromMinutes(30), ct);
        var version = (await storage.TryReadAsync(SessionTokenDso.EntityType, id, ct)).Version.ShouldNotBeNull();

        // begin-snippet: expire-update-clear
        // Expiration.NoExpiration explicitly clears an existing expiration.
        var clearResult = await storage.UpdateAsync(
            id, token, version, [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        // end-snippet
        clearResult.ShouldBe(UpdateResult.Success);

        tp.Advance(TimeSpan.FromHours(10));

        var batchCrossPartition = fixture.DefaultCrossPartitionStorage;
        _ = await batchCrossPartition.PurgeExpiredAsync(100, ct);

        // Not expired, so the purge above did not remove it.
        (await storage.TryReadAsync(SessionTokenDso.EntityType, id, ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_with_null_expiration_leaves_the_existing_expiration_untouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateFixtureAsync(tp, ct);
        var storage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var token = new SessionTokenDso.V1("alice", "spa-client");
        _ = await CreateSessionTokenAsync(storage, id, token, TimeSpan.FromMinutes(30), ct);
        var version = (await storage.TryReadAsync(SessionTokenDso.EntityType, id, ct)).Version.ShouldNotBeNull();

        var touched = token with { ClientId = "spa-client-v2" };

        // begin-snippet: expire-update-unchanged
        // expiration: null means "don't change whatever expiration is already stored", not the
        // same as Expiration.NoExpiration, which clears it.
        var touchResult = await storage.UpdateAsync(
            id, touched, version, [], SearchFieldCollection.Empty, expiration: null, [], ct);
        // end-snippet
        touchResult.ShouldBe(UpdateResult.Success);

        tp.Advance(TimeSpan.FromMinutes(31));
        var batchCrossPartition = fixture.DefaultCrossPartitionStorage;
        var deleted = await batchCrossPartition.PurgeExpiredAsync(100, ct);

        // The original 30-minute expiration still applies, so the purge removed it.
        deleted.ShouldBeGreaterThanOrEqualTo(1);
        (await storage.TryReadAsync(SessionTokenDso.EntityType, id, ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task Expiration_can_be_set_in_a_batch_operation()
    {
        var ct = TestContext.Current.CancellationToken;
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateFixtureAsync(tp, ct);
        var storage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var token = new SessionTokenDso.V1("alice", "spa-client");
        _ = await CreateSessionTokenAsync(storage, id, token, TimeSpan.FromMinutes(5), ct);
        var version = (await storage.TryReadAsync(SessionTokenDso.EntityType, id, ct)).Version.ShouldNotBeNull();

        // begin-snippet: expire-batch-update
        var batchResult = await storage.ExecuteBatchAsync(
            [UpdateOperation.For(
                id, token, version, [], SearchFieldCollection.Empty, Expiration.InRelative(TimeSpan.FromHours(2)))],
            [], ct);
        // end-snippet
        batchResult.Success.ShouldBeTrue();

        tp.Advance(TimeSpan.FromMinutes(30));
        (await storage.TryReadAsync(SessionTokenDso.EntityType, id, ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task Expired_but_unpurged_rows_remain_visible_to_reads_and_queries()
    {
        var ct = TestContext.Current.CancellationToken;
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateFixtureAsync(tp, ct);
        var storage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var token = new SessionTokenDso.V1("alice", "spa-client");
        _ = await CreateSessionTokenAsync(storage, id, token, TimeSpan.FromMinutes(5), ct);

        tp.Advance(TimeSpan.FromMinutes(10));

        // begin-snippet: expire-expired-rows-still-readable
        // Storage does not filter expired-but-not-yet-purged rows out of reads or queries.
        var read = await storage.TryReadAsync(SessionTokenDso.EntityType, id, ct);

        var queryResult = await storage.QueryAsync<SessionTokenDso.V1>(
            SessionTokenDso.EntityType, Query.All(), SortParameter.Empty, DataRange.FromPage(1, 100), ct);

        var count = await storage.CountAsync(SessionTokenDso.EntityType, null, ct);
        // end-snippet

        read.Found.ShouldBeTrue();
        queryResult.Items.Any(item => item.Id == id).ShouldBeTrue();
        count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task PurgeExpiredAsync_removes_expired_entities_and_their_links()
    {
        var ct = TestContext.Current.CancellationToken;
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateFixtureAsync(tp, ct);
        var storage = fixture.PartitionedStorage;
        var crossPartitionStorage = fixture.DefaultCrossPartitionStorage;

        var ids = new List<UuidV7>();
        for (var i = 0; i < 3; i++)
        {
            var id = UuidV7.New();
            ids.Add(id);
            _ = await CreateSessionTokenAsync(
                storage, id, new SessionTokenDso.V1($"user-{i}", "spa-client"), TimeSpan.FromMinutes(5), ct);
        }

        tp.Advance(TimeSpan.FromMinutes(10));

        // begin-snippet: expire-purge-expired-loop
        var totalDeleted = 0;
        int deletedInBatch;

        // PurgeExpiredAsync only removes a single batch. Loop until a batch comes back smaller
        // than the requested size, which signals there's nothing left to purge right now.
        const int batchSize = 100;
        do
        {
            deletedInBatch = await crossPartitionStorage.PurgeExpiredAsync(batchSize, ct);
            totalDeleted += deletedInBatch;
        } while (deletedInBatch >= batchSize);
        // end-snippet

        totalDeleted.ShouldBeGreaterThanOrEqualTo(3);
        foreach (var id in ids)
        {
            (await storage.TryReadAsync(SessionTokenDso.EntityType, id, ct)).Found.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task PurgeExpiredAsync_rejects_a_batch_size_outside_1_1000()
    {
        var ct = TestContext.Current.CancellationToken;
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateFixtureAsync(tp, ct);
        var crossPartitionStorage = fixture.DefaultCrossPartitionStorage;

        // 0 and 1001 are both outside the documented [1, 1000] range. Out-of-range values are
        // rejected outright, not clamped into range.
        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            async () => await crossPartitionStorage.PurgeExpiredAsync(0, ct));
        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            async () => await crossPartitionStorage.PurgeExpiredAsync(1001, ct));
    }

    // begin-snippet: expire-purge-expired-raises-outbox-event
    public sealed class SessionExpiredSubscription : IOutboxSubscription
    {
        public static readonly SubscriberName Name = SubscriberName.Create("session-token-expired");

        public SubscriberName SubscriberName => Name;
        public bool IsEnabled => true;

        // Narrow to the one entity type this subscription cares about; the outbox event name is
        // always Duende.Storage.Internal.Outbox.OutboxEventName.EntityExpired for a purge-raised event.
        public IReadOnlySet<int> EntityTypeIds { get; } = new HashSet<int> { (int)SessionTokenDso.EntityType.Id };
        public IReadOnlySet<OutboxEventName> EventNames { get; } = new HashSet<OutboxEventName> { OutboxEventName.EntityExpired };
    }
    // end-snippet

    [Fact]
    public async Task PurgeExpiredAsync_raises_an_EntityExpired_outbox_event_per_entity()
    {
        var ct = TestContext.Current.CancellationToken;
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await new SqliteStorageEngineFixtureFactory().CreateAsync(ct, services =>
        {
            _ = services.AddSingleton<TimeProvider>(tp);
            services.AddDsoRegistration<SessionTokenDso.V1>();
            _ = services.AddSingleton<IOutboxSubscription>(new SessionExpiredSubscription());
        });
        var storage = fixture.PartitionedStorage;
        var crossPartitionStorage = fixture.DefaultCrossPartitionStorage;

        var id = UuidV7.New();
        _ = await CreateSessionTokenAsync(storage, id, new SessionTokenDso.V1("alice", "spa-client"), TimeSpan.FromMinutes(5), ct);

        tp.Advance(TimeSpan.FromMinutes(10));
        var deleted = await crossPartitionStorage.PurgeExpiredAsync(100, ct);
        deleted.ShouldBeGreaterThanOrEqualTo(1);

        var page = await crossPartitionStorage.GetOutboxEventsForSubscriptionAsync(SessionExpiredSubscription.Name, 100, ct);
        page.Events.Count.ShouldBe(1);
        page.Events[0].EventName.ShouldBe(OutboxEventName.EntityExpired);
        page.Events[0].SubjectId.ShouldBe(UuidV7.From(id.Value));
    }

    [Fact]
    public async Task PurgePoolAsync_deletes_everything_in_the_pool_and_reports_counts()
    {
        var ct = TestContext.Current.CancellationToken;
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await CreateFixtureAsync(tp, ct);
        var storage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        // Not expired, so PurgePoolAsync removes it anyway, unlike PurgeExpiredAsync.
        _ = await storage.CreateAsync(
            id, new SessionTokenDso.V1("alice", "spa-client"), [], SearchFieldCollection.Empty,
            Expiration.NoExpiration, [], ct);

        // begin-snippet: expire-purge-pool
        // Deletes every entity, link, and outbox row in the pool this IPartitionedStorage is
        // scoped to, for example, when a tenant (Duende.Spaces space) is deleted outright.
        var result = await storage.PurgePoolAsync(ct);
        // end-snippet

        result.EntitiesDeleted.ShouldBeGreaterThanOrEqualTo(1);
        (await storage.TryReadAsync(SessionTokenDso.EntityType, id, ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task PurgePoolAsync_does_not_raise_new_outbox_events()
    {
        var ct = TestContext.Current.CancellationToken;
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = await new SqliteStorageEngineFixtureFactory().CreateAsync(ct, services =>
        {
            _ = services.AddSingleton<TimeProvider>(tp);
            services.AddDsoRegistration<SessionTokenDso.V1>();
            _ = services.AddSingleton<IOutboxSubscription>(new SessionExpiredSubscription());
        });
        var storage = fixture.PartitionedStorage;
        var crossPartitionStorage = fixture.DefaultCrossPartitionStorage;

        var id = UuidV7.New();
        _ = await storage.CreateAsync(
            id, new SessionTokenDso.V1("alice", "spa-client"), [], SearchFieldCollection.Empty,
            Expiration.NoExpiration, [], ct);

        var result = await storage.PurgePoolAsync(ct);
        result.EntitiesDeleted.ShouldBeGreaterThanOrEqualTo(1);

        // Purging a pool deletes any outbox rows it finds; it does not write new EntityExpired
        // events for the entities it removes (unlike PurgeExpiredAsync).
        var page = await crossPartitionStorage.GetOutboxEventsForSubscriptionAsync(SessionExpiredSubscription.Name, 100, ct);
        page.Events.ShouldBeEmpty();
    }
}
