// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.SearchFields;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Deterministic (not stress/timing-based) reproductions, on every server engine (PostgreSQL, SQL
/// Server, Oracle — not SQLite, which has one writer and so can neither reach the delete's repeat
/// path nor deadlock), of:
/// <list type="bullet">
/// <item>the entity delete's single repeat: step 1 (delete the entity's own links) misses a
/// Cascade link inserted concurrently on a second connection/transaction, step 2's foreign key
/// check then blocks on that transaction's uncommitted row, and once it commits, step 2 fails with
/// an FK violation that the engine repeats both steps for, once, successfully;</item>
/// <item>the whole-operation deadlock retry: a batch update of two entities deadlocks, in the
/// database, against a second connection/transaction that locks them in the opposite order; the
/// engine's transaction is made the victim, and <see cref="Internal.DeadlockRetry"/> retries the
/// whole batch from the start and succeeds.</item>
/// </list>
/// Both schedules synchronize via a bounded poll of the engine's own lock/blocking system views
/// (see <see cref="IConcurrencySession.WaitUntilBlockingAnotherSessionAsync"/>), never a timing sleep.
/// </summary>
public partial class StoreLinkDeterministicConcurrencyTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task DeleteAsync_retries_once_when_its_step_2_blocks_on_a_concurrently_inserted_linkAsync()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);

        var definition = TestLinkData.TestLink;
        var dataCategory = DataCategoryName.Create("link-delete-retry-deterministic");
        var storage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(dataCategory, _ct);

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();
        (await storage.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);
        (await storage.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);

        // A second connection/transaction locks the right entity's row (without modifying it) before
        // the delete starts, so the delete's step 1 (no links yet) passes but step 2's foreign key
        // check on this row blocks.
        await using var session = await ConcurrencyTestFixture.BeginSessionAsync(fixture, _ct);
        await session.LockEntityRowAsync(definition.Right, rightId.Value, _ct);

        var deleteTask = storage.DeleteAsync(definition.Right, rightId, [], _ct);

        await session.WaitUntilBlockingAnotherSessionAsync(_ct);

        // The session holds the right entity's row lock, so this Cascade link insert's own foreign
        // key check (against that same row) succeeds immediately.
        await session.InsertCascadeLinkAsync(definition, leftId, rightId, _ct);
        await session.CommitAsync(_ct);

        var deleteResult = await deleteTask;

        deleteResult.ShouldBe(DeleteResult.Success,
            "The delete must succeed after its single repeat re-deletes the concurrently committed link.");
        (await storage.TryReadAsync(definition.Right, rightId, _ct)).Found.ShouldBeFalse(
            "The right entity must be deleted.");

        var remainingLinks = await fixture.DumpEntityLinksAsync(_ct);
        remainingLinks.ShouldNotContain(
            l => l.LeftEntityId == leftId.Value && l.RightEntityId == rightId.Value,
            "The concurrently inserted link must be gone along with the entity.");
    }

    [Fact]
    public async Task ExecuteBatchAsync_retries_the_whole_batch_once_after_a_deadlockAsync()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
        });
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);

        var dataCategory = DataCategoryName.Create("deadlock-retry-deterministic");
        var storage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(dataCategory, _ct);

        // Most engines (SQL Server, PostgreSQL) let this session try to force itself to lose the
        // deadlock (PreferOtherSessionAsDeadlockVictimAsync), so the engine is normally the victim
        // on the first attempt. SQL Server's DEADLOCK_PRIORITY is a real guarantee; PostgreSQL's
        // deadlock_timeout trick is not (see that method's own comment) — either way, Oracle has no
        // such knob at all. It always aborts whichever session's lock *request* completes the wait
        // cycle, which this schedule arranges to normally be the engine's, but not with a guarantee.
        // A run where the *session* loses instead doesn't exercise DeadlockRetry at all (the
        // engine's batch then just succeeds once the session's lock is released, with no retry
        // involved), so that attempt proves nothing and must be redone with a fresh scenario instead
        // of counted as evidence either way. The bounded retry loop below covers exactly that case.
        const int maxAttempts = 5;
        var engineWasVictim = false;
        for (var attempt = 1; attempt <= maxAttempts && !engineWasVictim; attempt++)
        {
            var r0 = UuidV7.New();
            var r1 = UuidV7.New();
            (await storage.CreateAsync(r0, new TestDso("r0"), [], [], Expiration.NoExpiration, [], _ct))
                .ShouldBe(CreateResult.Success);
            (await storage.CreateAsync(r1, new TestDso("r1"), [], [], Expiration.NoExpiration, [], _ct))
                .ShouldBe(CreateResult.Success);

            var v0 = (await storage.TryReadAsync(TestDso.DsoVersion.EntityType, r0, _ct)).Version!.Value;
            var v1 = (await storage.TryReadAsync(TestDso.DsoVersion.EntityType, r1, _ct)).Version!.Value;

            // The session locks R1 first, so the engine's batch (which updates R0, then blocks trying to
            // update R1) and the session (which then requests R0) form a cycle: engine holds R0 waits for
            // R1, session holds R1 waits for R0.
            await using var session = await ConcurrencyTestFixture.BeginSessionAsync(fixture, _ct);
            await session.LockEntityRowAsync(TestDso.DsoVersion.EntityType, r1.Value, _ct);

            var batchTask = storage.ExecuteBatchAsync(
                [
                    UpdateOperation.For(r0, new TestDso("r0-updated"), v0, [], SearchFieldCollection.Empty, null),
                    UpdateOperation.For(r1, new TestDso("r1-updated"), v1, [], SearchFieldCollection.Empty, null)
                ],
                [],
                _ct);

            await session.WaitUntilBlockingAnotherSessionAsync(_ct);

            // Arrange for the engine's transaction, not the session's, to be chosen as the deadlock
            // victim, then close the cycle by requesting R0's lock (held by the engine).
            await session.PreferOtherSessionAsDeadlockVictimAsync(_ct);
            var sessionSurvived = await session.TryLockEntityRowAsync(TestDso.DsoVersion.EntityType, r0.Value, _ct);

            if (!sessionSurvived)
            {
                // The session, not the engine, was the deadlock victim this attempt: the engine's
                // batch now just completes normally once the session's lock was released by its own
                // rollback, without DeadlockRetry ever retrying anything. Drain it so the connection
                // isn't left with a pending operation, then try again with a fresh pair of entities.
                _ = await batchTask;
                continue;
            }

            engineWasVictim = true;

            // Release both of the session's locks so the engine's retried attempt (a brand new
            // transaction, from DeadlockRetry.RunAsync) can proceed and commit.
            await session.CommitAsync(_ct);

            var batchResult = await batchTask;

            batchResult.Success.ShouldBeTrue(
                "The batch must succeed after DeadlockRetry retries the whole operation once.");

            // The retried attempt must start from a fresh results list: if the engine instead kept
            // appending to the results list from the failed first attempt, this batch's successful
            // retry would report more than 2 results, with the first attempt's (failed) outcomes
            // still mixed in ahead of these two.
            batchResult.Results.Count.ShouldBe(2,
                "A results list leaking across retry attempts would include outcomes from the failed first attempt too.");
            batchResult.Results[0].Index.ShouldBe(0);
            batchResult.Results[0].Outcome.ShouldBe(OperationOutcome.Success);
            batchResult.Results[1].Index.ShouldBe(1);
            batchResult.Results[1].Outcome.ShouldBe(OperationOutcome.Success);

            var read0 = await storage.TryReadAsync(TestDso.DsoVersion.EntityType, r0, _ct);
            var read1 = await storage.TryReadAsync(TestDso.DsoVersion.EntityType, r1, _ct);
            read0.Found.ShouldBeTrue();
            read1.Found.ShouldBeTrue();
            ((TestDso)read0.Dso!).Value.ShouldBe("r0-updated");
            ((TestDso)read1.Dso!).Value.ShouldBe("r1-updated");
            read0.Version.ShouldBe(v0 + 1);
            read1.Version.ShouldBe(v1 + 1);
        }

        engineWasVictim.ShouldBeTrue(
            $"The engine was never the deadlock victim in {maxAttempts} attempts, so DeadlockRetry's whole-batch retry was never exercised or proven.");
    }
}
