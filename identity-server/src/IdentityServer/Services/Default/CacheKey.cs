// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.IdentityServer.Services.Default;

internal static class CacheKey
{
    private const string Prefix = "IS";

    public static string For<T>(string key) => $"{Prefix}:{typeof(T).FullName}-{key}";

    /// <summary>
    /// Options for read-only lookups: disables all writes and underlying data fetch.
    /// Returns null/default if the key is not in cache.
    /// </summary>
    internal static readonly HybridCacheEntryOptions ReadOnlyOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
              | HybridCacheEntryFlags.DisableDistributedCacheWrite
              | HybridCacheEntryFlags.DisableUnderlyingData
    };

    /// <summary>
    /// Creates write options for caching a value with the specified expiration.
    /// Both L1 (in-memory) and L2 (distributed) cache tiers are enabled so that
    /// registering an <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>
    /// automatically provides distributed caching.
    /// </summary>
    internal static HybridCacheEntryOptions WriteOptions(TimeSpan expiration) => new()
    {
        Expiration = expiration
    };
}
