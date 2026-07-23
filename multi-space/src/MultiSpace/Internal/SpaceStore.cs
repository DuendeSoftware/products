// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.MultiSpace.Internal.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace Duende.MultiSpace.Internal;

internal sealed class SpaceStore(
    SpaceRepository spaceRepository,
    HybridCache cache,
    IOptions<MultiSpaceOptions> options) : ISpaceStore
{
    private readonly HybridCacheEntryOptions CacheEntryOptions = new()
    {
        LocalCacheExpiration = options.Value.LocalCacheExpiration,
        Expiration = options.Value.Expiration
    };

    public async Task<SpaceResolutionResult?> TryResolveSpace(SpaceMatchPattern matchingCriteria, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(matchingCriteria);

        var criteriaCacheKey = SpaceCacheKeys.ForPattern(matchingCriteria.Origin, matchingCriteria.Path);

        // The SpaceMatchPattern => SpaceConfiguration resolution is cached.
        var config = await cache.GetOrCreateAsync(
            key: criteriaCacheKey,
            state: (matchingCriteria, spaceRepository),
            factory: static async (state, token) =>
            {
                var space = await state.spaceRepository.TryGetByPatternAsync(state.matchingCriteria, token);
                return space;
            },
            options: CacheEntryOptions,
            cancellationToken: ct);

        if (config is { Enabled: true })
        {
            // The SpaceID => SpaceConfiguration is also cached. Since we already retrieved the config in
            // the previous lookup, we can prime the by-ID cache without an extra round-trip.
            var spaceCacheKey = SpaceCacheKeys.ForSpaceId(config.Id);
            _ = await cache.GetOrCreateAsync(
                key: spaceCacheKey,
                state: config,
                factory: static (state, _) => ValueTask.FromResult<SpaceConfiguration?>(state?.Enabled == true ? state : null),
                options: CacheEntryOptions,
                cancellationToken: ct);
        }

        return config is { Enabled: true }
            ? new SpaceResolutionResult
            {
                SpaceId = config.Id,
                MatchedPath = matchingCriteria.Path != null ? new PathString(matchingCriteria.Path) : null
            }
            : null;
    }

    public async Task<bool> IsOriginClaimed(string origin, Ct ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(origin);

        var cacheKey = SpaceCacheKeys.ForOriginClaim(origin);

        return await cache.GetOrCreateAsync(
            key: cacheKey,
            state: (origin, spaceRepository),
            factory: static async (state, token) =>
                await state.spaceRepository.IsOriginClaimedAsync(state.origin, token),
            options: CacheEntryOptions,
            cancellationToken: ct);
    }

    public async Task<Space?> TryGetSpace(SpaceId spaceId, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(spaceId);

        var cacheKey = SpaceCacheKeys.ForSpaceId(spaceId);

        var config = await cache.GetOrCreateAsync(cacheKey, (spaceId, spaceRepository), static async (state, token) =>
        {
            var result = await state.spaceRepository.GetByIdAsync(state.spaceId, token);
            return result is { Found: true, Item.Enabled: true } ? result.Item : null;
        }, CacheEntryOptions, cancellationToken: ct);

        return config?.ToSpace();
    }
}
