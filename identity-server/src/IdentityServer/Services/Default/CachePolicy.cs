// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.Spaces;
using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.IdentityServer.Services.Default;

/// <summary>
/// Cache policy for a specific type T, providing space-aware key generation, expiration, and tagging.
/// </summary>
/// <typeparam name="T">The type being cached</typeparam>
public sealed class CachePolicy<T>(ISpaceContextAccessor? spaceContextAccessor)
{
    /// <summary>
    /// Builds a cache key for the given identifier, optionally partitioned by space.
    /// </summary>
    /// <param name="identifier">The identifier to include in the key</param>
    /// <returns>A cache key string</returns>
    public string BuildKey(string identifier)
    {
        if (spaceContextAccessor is null || !spaceContextAccessor.IsSpaceIdConfigured())
        {
            return $"IS:{typeof(T).FullName}-{identifier}";
        }

        return $"{spaceContextAccessor.GetSpaceId().Value}:IS:{typeof(T).FullName}-{identifier}";
    }

    /// <summary>
    /// Creates write options with the specified expiration duration.
    /// </summary>
    /// <param name="duration">The cache entry expiration duration</param>
    /// <returns>HybridCache entry options with the specified expiration</returns>
    public HybridCacheEntryOptions WriteOptions(TimeSpan duration) =>
        new HybridCacheEntryOptions { Expiration = duration };

    /// <summary>
    /// Gets the tags for this cache policy, used for space-based eviction.
    /// </summary>
    public IReadOnlyList<string> Tags
    {
        get
        {
            if (spaceContextAccessor is null || !spaceContextAccessor.IsSpaceIdConfigured())
            {
                return [];
            }

            return [$"space:{spaceContextAccessor.GetSpaceId().Value}"];
        }
    }
}
