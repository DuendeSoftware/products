// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal.Storage;
using Duende.Storage;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace Duende.Spaces.Internal;

/// <summary>
/// Resolves a space's pool from the raw space record, without a schema, so the partitioned
/// storage factory doesn't depend on the schema store (which may itself route through it).
/// </summary>
internal sealed class SpaceRouting(ManagementStorageAccessor storageAccessor, HybridCache cache, IOptions<SpacesOptions> options)
{
    public async Task<PoolId?> TryGetPoolIdAsync(SpaceId spaceId, Ct ct)
    {
        var poolId = await cache.GetOrCreateAsync(
            SpaceCacheKeys.ForSpaceIdRouting(spaceId),
            (spaceId, storageAccessor),
            static async (state, token) =>
            {
                var result = await state.storageAccessor.GetManagementStorage()
                    .TryReadAsync(SpaceDso.EntityType, UuidV7.From(state.spaceId.Value), token);
                return result.Found && result.Dso is SpaceDso.V1 { IsDeleted: false, Enabled: true } dso ? dso.PoolId : (int?)null;
            },
            new HybridCacheEntryOptions { LocalCacheExpiration = options.Value.LocalCacheExpiration, Expiration = options.Value.Expiration },
            cancellationToken: ct);

        return poolId is { } value ? (PoolId)value : null;
    }
}
