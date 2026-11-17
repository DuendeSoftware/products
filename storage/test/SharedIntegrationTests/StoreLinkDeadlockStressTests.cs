// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Shared stress tests for deadlock- and FK-race handling in batches: crossed-order link/delete
/// batches, and link-vs-delete-of-the-same-entity races run through <c>ExecuteBatchAsync</c>. Unlike
/// <see cref="StoreLinkConcurrencyTests"/> (single-call <c>LinkAsync</c>/<c>DeleteAsync</c>), these races
/// go through whole-batch transactions, which is where server engines (Oracle, SQL Server, PostgreSQL)
/// can see a transaction-level deadlock/serialization failure rather than only a statement-level FK
/// error. These races are scheduler-dependent, not deterministic: only the allowed outcome set and
/// end-state invariants are asserted, never timing or which side of a race wins.
/// </summary>
public partial class StoreLinkDeadlockStressTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    // Kept small and bounded so the test runs quickly even on engines that serialize writers (SQLite).
    private const int Iterations = 20;

    private static readonly OperationOutcome[] AllowedOutcomes =
    [
        OperationOutcome.Success,
        OperationOutcome.DoesNotExist,
        OperationOutcome.StillLinked,
        OperationOutcome.AlreadyLinked
    ];

    [Fact]
    public async Task Crossed_order_link_and_delete_batches_reach_a_consistent_end_state_CascadeAsync() =>
        await RunCrossedOrderAsync(TestLinkData.TestLink);

    [Fact]
    public async Task Crossed_order_link_and_delete_batches_reach_a_consistent_end_state_RestrictAsync() =>
        await RunCrossedOrderAsync(TestLinkData.TestLinkRestrict);

    [Fact]
    public async Task Batched_link_vs_delete_of_the_same_entity_reaches_a_consistent_end_state_CascadeAsync() =>
        await RunSameEntityAsync(TestLinkData.TestLink);

    [Fact]
    public async Task Batched_link_vs_delete_of_the_same_entity_reaches_a_consistent_end_state_RestrictAsync() =>
        await RunSameEntityAsync(TestLinkData.TestLinkRestrict);

    /// <summary>
    /// Batch 1 links (A, X1) then (A, X2); batch 2 deletes X2 then X1 — the reverse order, so any
    /// engine that takes locks in statement order can race across the two entities as well as within
    /// one. Both batches are "all or nothing": a batch that hits an unexpected mid-batch failure (an
    /// entity it needed was concurrently deleted) is a valid, consistent outcome as long as no link
    /// ever survives without both its entities.
    /// </summary>
    private async Task RunCrossedOrderAsync(LinkDefinition definition)
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);

        var dataCategory = DataCategoryName.Create("link-deadlock-crossed");
        var storage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(dataCategory, _ct);

        for (var i = 0; i < Iterations; i++)
        {
            var a = UuidV7.New();
            var x1 = UuidV7.New();
            var x2 = UuidV7.New();

            (await storage.CreateAsync(a, new TestDso($"a-{i}"), [], [], Expiration.NoExpiration, [], _ct))
                .ShouldBe(CreateResult.Success);
            (await storage.CreateAsync(x1, new TestDso2($"x1-{i}"), [], [], Expiration.NoExpiration, [], _ct))
                .ShouldBe(CreateResult.Success);
            (await storage.CreateAsync(x2, new TestDso2($"x2-{i}"), [], [], Expiration.NoExpiration, [], _ct))
                .ShouldBe(CreateResult.Success);

            var linkBatch = new IStorageOperation[]
            {
                LinkOperation.For(definition, a, x1),
                LinkOperation.For(definition, a, x2)
            };
            var deleteBatch = new IStorageOperation[]
            {
                DeleteOperation.ById(definition.Right, x2),
                DeleteOperation.ById(definition.Right, x1)
            };

            var linkTask = storage.ExecuteBatchAsync(linkBatch, [], _ct);
            var deleteTask = storage.ExecuteBatchAsync(deleteBatch, [], _ct);

            var unexpected = await RunBothAsync(linkTask, deleteTask);
            unexpected.ShouldBeNull($"Iteration {i}: no unhandled exception is expected from crossed-order batches, got: {unexpected}");

            var linkBatchResult = await linkTask;
            var deleteBatchResult = await deleteTask;

            AssertAllowedOutcomes(i, linkBatchResult);
            AssertAllowedOutcomes(i, deleteBatchResult);
        }
    }

    /// <summary>
    /// One batch links (A, X); a concurrent batch deletes X. Exercises the same race as
    /// <see cref="StoreLinkConcurrencyTests"/> but through <c>ExecuteBatchAsync</c>'s whole-transaction
    /// path instead of a single-operation call.
    /// </summary>
    private async Task RunSameEntityAsync(LinkDefinition definition)
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);

        var dataCategory = DataCategoryName.Create("link-deadlock-same-entity");
        var storage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(dataCategory, _ct);

        for (var i = 0; i < Iterations; i++)
        {
            var left = UuidV7.New();
            var right = UuidV7.New();

            (await storage.CreateAsync(left, new TestDso($"left-{i}"), [], [], Expiration.NoExpiration, [], _ct))
                .ShouldBe(CreateResult.Success);
            (await storage.CreateAsync(right, new TestDso2($"right-{i}"), [], [], Expiration.NoExpiration, [], _ct))
                .ShouldBe(CreateResult.Success);

            var linkTask = storage.ExecuteBatchAsync([LinkOperation.For(definition, left, right)], [], _ct);
            var deleteTask = storage.ExecuteBatchAsync([DeleteOperation.ById(definition.Right, right)], [], _ct);

            var unexpected = await RunBothAsync(linkTask, deleteTask);
            unexpected.ShouldBeNull($"Iteration {i}: no unhandled exception is expected from a batched Link/Delete race, got: {unexpected}");

            var linkBatchResult = await linkTask;
            var deleteBatchResult = await deleteTask;

            AssertAllowedOutcomes(i, linkBatchResult);
            AssertAllowedOutcomes(i, deleteBatchResult);

            var rightStillExists = (await storage.TryReadAsync(definition.Right, right, _ct)).Found;

            // Raw entity_links row count, not QueryLinksAsync: QueryLinksAsync joins entities, so
            // once the right entity is gone it reports "no link" whether or not a dangling link row
            // actually survives, which would make a check here vacuous for the only case that
            // matters (the right entity is gone). entity_links has NO ACTION FKs to entities on both
            // sides, so this raw count is also what proves a dangling link is actually impossible,
            // not merely invisible to the join.
            var linkExists = await fixture.CountEntityLinksAsync(
                PoolId.Default.Value, (int)definition.Link.Id, (int)definition.Left.Id, left.Value, (int)definition.Right.Id, right.Value, _ct) > 0;

            // The right entity's existence and the link's existence must agree: a link can't outlive
            // its right entity (both delete behaviours forbid that), and the right entity can't be gone
            // while a link to it still exists.
            if (!rightStillExists)
            {
                linkExists.ShouldBeFalse($"Iteration {i}: the right entity {right} is gone, so no link to it may remain.");
            }
        }
    }

    private static async Task<Exception?> RunBothAsync(Task<BatchResult> first, Task<BatchResult> second)
    {
        Exception? unexpected = null;
        try
        {
            _ = await first;
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            unexpected = ex;
        }

        try
        {
            _ = await second;
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            unexpected ??= ex;
        }

        return unexpected;
    }

    private static void AssertAllowedOutcomes(int iteration, BatchResult result)
    {
        foreach (var operationResult in result.Results)
        {
            AllowedOutcomes.ShouldContain(operationResult.Outcome,
                $"Iteration {iteration}: operation {operationResult.Index} returned unexpected outcome {operationResult.Outcome}.");
        }
    }
}
