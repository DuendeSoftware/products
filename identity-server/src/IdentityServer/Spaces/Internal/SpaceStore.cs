// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Stores;
using Duende.Spaces.Internal.Storage;
using Duende.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.Spaces.Internal;

internal sealed class SpaceStore(
    SpaceRepository spaceRepository,
    [FromKeyedServices(SpacesServiceProviderKeys.SpacesCache)] HybridCache cache,
    IOptions<SpacesOptions> options) : ISpaceStore
{
    private readonly HybridCacheEntryOptions CacheEntryOptions = new()
    {
        LocalCacheExpiration = options.Value.LocalCacheExpiration,
        Expiration = options.Value.Expiration
    };

    public async Task<SpaceResolutionResult?> TryResolveSpace(SpaceMatchPattern matchingCriteria, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(matchingCriteria);

        var criteriaCacheKey = SpaceCacheKeys.ForPattern(matchingCriteria.Origin, matchingCriteria.Path);

        // The SpaceMatchPattern => stored space resolution is cached, but only for patterns that
        // belong to a space. The key comes from the request, so caching misses would let an
        // anonymous client grow the cache without limit. HybridCache stores nothing when the
        // factory throws. The record stored by SpaceRepository (SpaceDso.V1) is cached, not the
        // mapped SpaceConfiguration: its abstract AttributeValue items are not something
        // HybridCache can deserialize.
        SpaceDso.V1 dso;
        try
        {
            dso = await cache.GetOrCreateAsync(
                key: criteriaCacheKey,
                state: (matchingCriteria, spaceRepository),
                factory: static async (state, token) =>
                {
                    var space = await state.spaceRepository.TryGetDsoByPatternAsync(state.matchingCriteria, token);
                    return space ?? throw new NotCachedException();
                },
                options: CacheEntryOptions,
                cancellationToken: ct);
        }
        catch (NotCachedException)
        {
            return null;
        }

        return dso.Enabled
            ? new SpaceResolutionResult
            {
                SpaceId = dso.SpaceId,
                MatchedPath = matchingCriteria.Path != null ? new PathString(matchingCriteria.Path) : null
            }
            : null;
    }

    public async Task<bool> IsOriginClaimed(string origin, Ct ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(origin);

        var cacheKey = SpaceCacheKeys.ForOriginClaim(origin);

        // Only a "claimed" answer is cached. The origin comes from the request, so caching
        // "not claimed" would let an anonymous client grow the cache without limit.
        try
        {
            return await cache.GetOrCreateAsync(
                key: cacheKey,
                state: (origin, spaceRepository),
                factory: static async (state, token) =>
                    await state.spaceRepository.IsOriginClaimedAsync(state.origin, token)
                        ? true
                        : throw new NotCachedException(),
                options: CacheEntryOptions,
                cancellationToken: ct);
        }
        catch (NotCachedException)
        {
            return false;
        }
    }

    public async Task<Space?> TryGetSpace(SpaceId spaceId, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(spaceId);

        var cacheKey = SpaceCacheKeys.ForSpaceId(spaceId);

        var dso = await cache.GetOrCreateAsync(cacheKey, (spaceId, spaceRepository), static async (state, token) =>
            await state.spaceRepository.GetDsoByIdAsync(state.spaceId, token), CacheEntryOptions, cancellationToken: ct);

        if (dso is not { Enabled: true })
        {
            return null;
        }

        var config = await spaceRepository.MapToConfigurationAsync(dso, ct);
        return config.ToSpace();
    }

    public async Task<Space?> TryGetSpaceByPoolId(PoolId poolId, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(poolId);

        var cacheKey = SpaceCacheKeys.ForPoolId(poolId);

        var dso = await cache.GetOrCreateAsync(cacheKey, (poolId, spaceRepository), static async (state, token) =>
            await state.spaceRepository.GetDsoByPoolIdAsync(state.poolId, token), CacheEntryOptions, cancellationToken: ct);

        if (dso is not { Enabled: true })
        {
            return null;
        }

        var config = await spaceRepository.MapToConfigurationAsync(dso, ct);
        return config.ToSpace();
    }
}
