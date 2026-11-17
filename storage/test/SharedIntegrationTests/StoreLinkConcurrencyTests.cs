// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Stress tests for Link vs Delete races on both link delete behaviours. These exercise the
/// database-enforced invariants added alongside <c>entity_links</c>: no link can end up pointing at a
/// deleted entity, and a Restrict delete never both "succeeds" and leaves a dangling link behind.
/// Scheduler-dependent, not deterministic: only end-state invariants are asserted, never timing.
/// </summary>
public partial class StoreLinkConcurrencyTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    // Kept small and bounded so the test runs quickly even on engines that serialize writers (SQLite).
    private const int Iterations = 20;

    [Fact]
    public async Task Concurrent_Link_and_Delete_of_the_right_entity_Cascade_reaches_a_consistent_end_stateAsync() =>
        await RunAsync(TestLinkData.TestLink);

    [Fact]
    public async Task Concurrent_Link_and_Delete_of_the_right_entity_Restrict_reaches_a_consistent_end_stateAsync() =>
        await RunAsync(TestLinkData.TestLinkRestrict);

    private async Task RunAsync(LinkDefinition definition)
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);

        var dataCategory = DataCategoryName.Create("link-concurrency");
        var storage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(dataCategory, _ct);

        for (var i = 0; i < Iterations; i++)
        {
            var leftId = UuidV7.New();
            var rightId = UuidV7.New();
            (await storage.CreateAsync(leftId, new TestDso($"left-{i}"), [], [], Expiration.NoExpiration, [], _ct))
                .ShouldBe(CreateResult.Success);
            (await storage.CreateAsync(rightId, new TestDso2($"right-{i}"), [], [], Expiration.NoExpiration, [], _ct))
                .ShouldBe(CreateResult.Success);

            var linkTask = storage.LinkAsync(definition, leftId, rightId, [], _ct);
            var deleteTask = storage.DeleteAsync(definition.Right, rightId, [], _ct);

            Exception? unexpected = null;
            LinkResult? linkResult = null;
            DeleteResult? deleteResult = null;
            try
            {
                linkResult = await linkTask;
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                unexpected = ex;
            }

            try
            {
                deleteResult = await deleteTask;
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                unexpected ??= ex;
            }

            unexpected.ShouldBeNull($"Iteration {i}: no unhandled exception is expected from a Link/Delete race, got: {unexpected}");

            var rightStillExists = (await storage.TryReadAsync(definition.Right, rightId, _ct)).Found;

            // Raw entity_links row count, not a QueryLinksAsync lookup: QueryLinksAsync joins
            // entities, so once the right entity is gone it reports "no link" whether or not a
            // dangling link row actually survives, making that check vacuous for the very cases
            // this test is about (the right entity is gone). entity_links has NO ACTION FKs to
            // entities on both sides, so this raw count is also what proves a dangling link is
            // actually impossible, not merely invisible to the join.
            var linkExists = await fixture.CountEntityLinksAsync(
                PoolId.Default.Value, (int)definition.Link.Id, (int)definition.Left.Id, leftId.Value, (int)definition.Right.Id, rightId.Value, _ct) > 0;

            if (definition.OnRightDelete == LinkDeleteBehavior.Cascade)
            {
                // Cascade never refuses the delete, so it always succeeds, removing the right entity
                // and cascading away any link. The concurrent link attempt either won the race and
                // succeeded (then got cascaded away) or lost it and correctly reported DoesNotExist.
                deleteResult.ShouldBe(DeleteResult.Success,
                    $"Iteration {i}: a Cascade delete of the right entity must always succeed, got {deleteResult}.");
                linkResult.ShouldBeOneOf([LinkResult.Success, LinkResult.DoesNotExist],
                    $"Iteration {i}: Cascade link attempt must be Success or DoesNotExist, got {linkResult}.");
                rightStillExists.ShouldBeFalse($"Iteration {i}: the right entity {rightId} must be gone after a successful Cascade delete.");
                linkExists.ShouldBeFalse($"Iteration {i}: the right entity {rightId} is gone (Cascade), so its link must be gone too.");
            }
            else
            {
                // Restrict allows exactly two consistent end states: the link won the race (the right
                // entity still existed when it inserted) and the delete was refused as StillLinked, with
                // both the entity and the link retained; or the delete won the race (it ran before, or
                // the link's existence check observed, the entity's removal) and reported Success, with
                // both the entity and the link gone. No other (linkResult, deleteResult) pair is valid.
                if (linkResult == LinkResult.Success && deleteResult == DeleteResult.StillLinked)
                {
                    rightStillExists.ShouldBeTrue($"Iteration {i}: link Success + delete StillLinked must retain the right entity {rightId}.");
                    linkExists.ShouldBeTrue($"Iteration {i}: link Success + delete StillLinked must retain the Restrict link.");
                }
                else if (linkResult == LinkResult.DoesNotExist && deleteResult == DeleteResult.Success)
                {
                    rightStillExists.ShouldBeFalse($"Iteration {i}: link DoesNotExist + delete Success must remove the right entity {rightId}.");
                    linkExists.ShouldBeFalse($"Iteration {i}: link DoesNotExist + delete Success must leave no Restrict link.");
                }
                else
                {
                    false.ShouldBeTrue(
                        $"Iteration {i}: unexpected (linkResult={linkResult}, deleteResult={deleteResult}) pair for a Restrict link; " +
                        "only (Success, StillLinked) or (DoesNotExist, Success) are valid end states.");
                }
            }
        }
    }
}
