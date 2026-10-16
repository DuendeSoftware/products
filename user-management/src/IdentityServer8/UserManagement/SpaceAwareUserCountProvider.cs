// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.UserManagement.Internal.Licensing;
using Duende.UserManagement.Internal.Storage;

namespace Duende.IdentityServer.UserManagement;

/// <summary>
/// Counts licensed users across every eligible space in the configured User Management store.
/// </summary>
/// <remarks>
/// <para>
/// Storage queries cannot span pools, so each space is counted separately and the counts are summed.
/// Eligible spaces are the default space plus every enabled, non-deleted space. The management space
/// is never counted. Each space has its own pool, so the per-space counts do not overlap.
/// </para>
/// <para>
/// Any failure aborts the whole count, so a partial total is never reported.
/// </para>
/// </remarks>
internal sealed class SpaceAwareUserCountProvider(
    ISpaceAdmin spaceAdmin,
    ISpaceContextAccessor spaceContextAccessor,
    UserRepository userRepository) : IUserCountProvider
{
    public async Task<long> GetUserCountAsync(Ct ct)
    {
        var spaceIds = await GetEligibleSpaceIdsAsync(spaceAdmin, ct);

        var total = await CountSpaceAsync(spaceContextAccessor, SpaceId.Default, ct);
        foreach (var spaceId in spaceIds)
        {
            total = checked(total + await CountSpaceAsync(spaceContextAccessor, spaceId, ct));
        }

        return total;
    }

    private async Task<long> CountSpaceAsync(ISpaceContextAccessor spaceContextAccessor, SpaceId spaceId, Ct ct)
    {
        using var spaceScope = spaceContextAccessor.SetSpace(spaceId);
        return await CountCurrentPartitionAsync(ct);
    }

    private Task<long> CountCurrentPartitionAsync(Ct ct) => userRepository.GetCountAsync(ct);

    private static async Task<IReadOnlyCollection<SpaceId>> GetEligibleSpaceIdsAsync(ISpaceAdmin spaceAdmin, Ct ct)
    {
        var filter = new SpaceFilter { Enabled = true, IsDeleted = false };
        var spaceIds = new HashSet<SpaceId>();
        ContinuationToken? continuationToken = null;

        while (true)
        {
            var range = continuationToken is null ? null : DataRange.FromContinuationToken(continuationToken);
            var page = await spaceAdmin.QueryAsync(QueryRequest.Create<SpaceFilter, SpaceSortField>(filter, range), ct);

            foreach (var space in page.Items)
            {
                // Re-check eligibility so a store that ignores the filter cannot inflate the count.
                if (space.Enabled && !space.IsDeleted && space.Id != SpaceId.Default && space.Id != SpaceId.Management)
                {
                    _ = spaceIds.Add(space.Id);
                }
            }

            if (!page.HasMoreData)
            {
                return spaceIds;
            }

            if (page.NextToken is null || page.NextToken.Equals(continuationToken))
            {
                throw new InvalidOperationException(
                    "The space query reported more results without a usable continuation token, so the licensed user count could not be completed.");
            }

            continuationToken = page.NextToken;
        }
    }
}
