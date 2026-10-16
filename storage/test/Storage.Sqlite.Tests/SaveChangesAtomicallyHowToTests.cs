// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Querying.SearchFields;
using Microsoft.Extensions.DependencyInjection;
using OutboxEventId = Duende.Storage.Internal.Outbox.OutboxEventId;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;
using SubscriberName = Duende.Storage.Internal.Outbox.SubscriberName;

namespace Duende.Storage.Sqlite;

public sealed class SaveChangesAtomicallyHowToTests
{
    // begin-snippet: batches-dsos
    public static class TeamDso
    {
        // Illustrative id only. Real DSOs must use an unused id from the owning product's reserved range.
        internal static readonly EntityType EntityType = new(2201, nameof(TeamDso));

        public sealed record V1(string Name) : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
        }
    }

    public static class MembershipDso
    {
        // Illustrative id only. Real DSOs must use an unused id from the owning product's reserved range.
        internal static readonly EntityType EntityType = new(2202, nameof(MembershipDso));

        public sealed record V1(Guid TeamId, string UserEmail) : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
        }
    }
    // end-snippet

    [Fact]
    public async Task Can_create_team_with_members_in_one_batch()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;

        var teamId = UuidV7.New();
        var memberId1 = UuidV7.New();
        var memberId2 = UuidV7.New();

        // begin-snippet: batches-build-and-execute
        var operations = new IStorageOperation[]
        {
            CreateOperation.For(teamId, new TeamDso.V1("Platform"), [], SearchFieldCollection.Empty, Expiration.NoExpiration),
            CreateOperation.For(memberId1, new MembershipDso.V1(teamId.Value, "ada@example.com"), [], SearchFieldCollection.Empty, Expiration.NoExpiration),
            CreateOperation.For(memberId2, new MembershipDso.V1(teamId.Value, "grace@example.com"), [], SearchFieldCollection.Empty, Expiration.NoExpiration)
        };

        var batchResult = await storage.ExecuteBatchAsync(operations, [], ct);
        // end-snippet

        batchResult.Success.ShouldBeTrue();
        batchResult.Results.Count.ShouldBe(3);
        batchResult.Results.ShouldAllBe(r => r.Outcome == OperationOutcome.Success);

        (await storage.TryReadAsync(TeamDso.EntityType, teamId, ct)).Found.ShouldBeTrue();
        (await storage.TryReadAsync(MembershipDso.EntityType, memberId1, ct)).Found.ShouldBeTrue();
        (await storage.TryReadAsync(MembershipDso.EntityType, memberId2, ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task Nothing_is_persisted_when_one_operation_in_the_batch_fails()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;

        // A member that already exists, so the second create in the batch below collides with it.
        var existingMemberId = UuidV7.New();
        var teamIdForExisting = UuidV7.New();
        (await storage.CreateAsync(existingMemberId, new MembershipDso.V1(teamIdForExisting.Value, "ada@example.com"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct))
            .ShouldBe(CreateResult.Success);

        var teamId = UuidV7.New();

        // begin-snippet: batches-atomicity
        var operations = new IStorageOperation[]
        {
            CreateOperation.For(teamId, new TeamDso.V1("Platform"), [], SearchFieldCollection.Empty, Expiration.NoExpiration),
            // This create fails: existingMemberId is already in use.
            CreateOperation.For(existingMemberId, new MembershipDso.V1(teamId.Value, "grace@example.com"), [], SearchFieldCollection.Empty, Expiration.NoExpiration)
        };

        var batchResult = await storage.ExecuteBatchAsync(operations, [], ct);

        batchResult.Success.ShouldBeFalse();
        // Only results up to and including the failed operation are reported.
        batchResult.Results.Count.ShouldBe(2);
        batchResult.Results[0].Outcome.ShouldBe(OperationOutcome.Success);
        batchResult.Results[1].Outcome.ShouldBe(OperationOutcome.AlreadyExists);
        // end-snippet

        // Nothing from the batch was persisted, including the team create that reported Success.
        (await storage.TryReadAsync(TeamDso.EntityType, teamId, ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task Update_in_batch_fails_with_a_stale_expected_version()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;

        var teamId = UuidV7.New();
        (await storage.CreateAsync(teamId, new TeamDso.V1("Platform"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct))
            .ShouldBe(CreateResult.Success);
        var currentVersion = (await storage.TryReadAsync(TeamDso.EntityType, teamId, ct)).Version!.Value;

        // Someone else updates the team first, so our caller's in-memory version is now stale.
        (await storage.UpdateAsync(teamId, new TeamDso.V1("Platform (renamed)"), currentVersion, [], SearchFieldCollection.Empty, null, [], ct))
            .ShouldBe(UpdateResult.Success);

        var newMemberId = UuidV7.New();

        // begin-snippet: batches-optimistic-concurrency
        var operations = new IStorageOperation[]
        {
            // Still using the stale version read before the concurrent update above.
            UpdateOperation.For(teamId, new TeamDso.V1("Platform (my edit)"), currentVersion, [], SearchFieldCollection.Empty, null),
            CreateOperation.For(newMemberId, new MembershipDso.V1(teamId.Value, "ada@example.com"), [], SearchFieldCollection.Empty, Expiration.NoExpiration)
        };

        var batchResult = await storage.ExecuteBatchAsync(operations, [], ct);

        batchResult.Success.ShouldBeFalse();
        batchResult.Results[0].Outcome.ShouldBe(OperationOutcome.UnexpectedVersion);
        // end-snippet

        // The concurrent rename survived; our batch's create did not.
        var team = await storage.TryReadAsync(TeamDso.EntityType, teamId, ct);
        ((TeamDso.V1)team.Dso!).Name.ShouldBe("Platform (renamed)");
        (await storage.TryReadAsync(MembershipDso.EntityType, newMemberId, ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task Retries_after_a_batch_concurrency_conflict()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;

        var teamId = UuidV7.New();
        (await storage.CreateAsync(teamId, new TeamDso.V1("Platform"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct))
            .ShouldBe(CreateResult.Success);
        var current = await storage.TryReadAsync(TeamDso.EntityType, teamId, ct);

        // Simulate a concurrent writer getting there between our read and our write.
        (await storage.UpdateAsync(teamId, new TeamDso.V1("Platform (concurrent edit)"), current.Version!.Value, [], SearchFieldCollection.Empty, null, [], ct))
            .ShouldBe(UpdateResult.Success);

        // begin-snippet: batches-retry-on-conflict
        const int maxAttempts = 3;
        BatchResult? batchResult = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (!current.Found)
            {
                // Someone else deleted the team between reads. There is nothing left to
                // archive, so stop retrying and let the caller decide how to report that.
                return;
            }

            // Build every operation from the state that was read, not from values decided earlier.
            var team = (TeamDso.V1)current.Dso;
            var operations = new IStorageOperation[]
            {
                UpdateOperation.For(teamId, team with { Name = $"{team.Name} (archived)" }, current.Version!.Value, [], SearchFieldCollection.Empty, null)
            };

            batchResult = await storage.ExecuteBatchAsync(operations, [], ct);

            if (batchResult.Success || batchResult.Results[0].Outcome != OperationOutcome.UnexpectedVersion)
            {
                break;
            }

            // Someone else changed the data after it was read. Resubmitting the same operations
            // with a newer version would overwrite their change, so read the current state again
            // and rebuild the batch from it on the next attempt.
            current = await storage.TryReadAsync(TeamDso.EntityType, teamId, ct);
        }
        // end-snippet

        batchResult!.Success.ShouldBeTrue();
        var saved = await storage.TryReadAsync(TeamDso.EntityType, teamId, ct);
        // The concurrent edit is kept: the retry rebuilt the change on top of it.
        ((TeamDso.V1)saved.Dso!).Name.ShouldBe("Platform (concurrent edit) (archived)");
    }

    [Fact]
    public async Task Retry_loop_stops_without_crashing_when_the_entity_was_deleted_concurrently()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;

        var teamId = UuidV7.New();

        // "current" here stands in for whatever TryReadResult the retry loop is holding when
        // it discovers the entity is gone, whether that's the very first read or a re-read
        // after a version conflict. Either way, the guard must stop before dereferencing Dso.
        var current = await storage.TryReadAsync(TeamDso.EntityType, teamId, ct);
        current.Found.ShouldBeFalse();

        const int maxAttempts = 3;
        BatchResult? batchResult = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (!current.Found)
            {
                // Nothing left to archive: stop retrying instead of dereferencing a null Dso.
                return;
            }

            var team = (TeamDso.V1)current.Dso;
            var operations = new IStorageOperation[]
            {
                UpdateOperation.For(teamId, team with { Name = $"{team.Name} (archived)" }, current.Version!.Value, [], SearchFieldCollection.Empty, null)
            };

            batchResult = await storage.ExecuteBatchAsync(operations, [], ct);

            if (batchResult.Success || batchResult.Results[0].Outcome != OperationOutcome.UnexpectedVersion)
            {
                break;
            }

            current = await storage.TryReadAsync(TeamDso.EntityType, teamId, ct);
        }

        // Reaching here (instead of throwing NullReferenceException on current.Dso) means the
        // not-found branch above returned before dereferencing a nonexistent entity.
        batchResult.ShouldBeNull();
    }

    [Fact]
    public async Task Outbox_events_commit_with_the_batch()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct, [new WildcardTestSubscription()]);
        var storage = fixture.PartitionedStorage;

        var teamId = UuidV7.New();

        // begin-snippet: batches-outbox-events
        var operations = new IStorageOperation[]
        {
            CreateOperation.For(teamId, new TeamDso.V1("Platform"), [], SearchFieldCollection.Empty, Expiration.NoExpiration)
        };

        var teamCreated = new OutboxEvent
        {
            Id = OutboxEventId.New(),
            Timestamp = DateTimeOffset.UtcNow,
            EventName = OutboxEventName.Create("TeamCreated"),
            SubjectId = teamId,
            EntityTypeName = nameof(TeamDso),
            EntityTypeId = (int)TeamDso.EntityType.Id,
            Payload = JsonSerializer.Serialize(new TeamDso.V1("Platform")),
            DsoTypeSchemaVersion = (int)TeamDso.V1.DsoVersion.SchemaVersion
        };

        var batchResult = await storage.ExecuteBatchAsync(operations, [teamCreated], ct);
        // end-snippet

        batchResult.Success.ShouldBeTrue();

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(SubscriberName.Create("test-subscription"), 10, ct);
        page.Events.ShouldContain(e => e.EventId == teamCreated.Id);
    }

    private static async Task<IStorageFixture> CreateFixtureAsync(Ct ct, IOutboxSubscription[]? subscriptions = null) =>
        await new SqliteStorageEngineFixtureFactory().CreateAsync(ct, services =>
        {
            services.AddDsoRegistration<TeamDso.V1>();
            services.AddDsoRegistration<MembershipDso.V1>();
            foreach (var subscription in subscriptions ?? [])
            {
                _ = services.AddSingleton(subscription);
            }
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
}
