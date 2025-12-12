// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Default implementation of the replay cache using Hybrid Cache
/// </summary>
internal class ReplayCache : IReplayCache
{
    private const string Prefix = "DPoPJwtBearerEvents-DPoPReplay-jti-";

    private readonly HybridCache _cache;

    /// <summary>
    /// Constructs new instances of <see cref="ReplayCache"/>.
    /// </summary>
    public ReplayCache(HybridCache cache) => _cache = cache;

    public async Task Add(string handle, TimeSpan expiration, CancellationToken ct)
    {
        var options = new HybridCacheEntryOptions
        {
            Expiration = expiration
        };

        await _cache.SetAsync(Prefix + handle, true, options, cancellationToken: ct);
    }

    private static readonly HybridCacheEntryOptions ReadOnlyEntryOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
                | HybridCacheEntryFlags.DisableDistributedCacheWrite
                | HybridCacheEntryFlags.DisableUnderlyingData
    };

    public async Task<bool> Exists(string handle, CancellationToken ct) => await _cache.GetOrCreateAsync<bool>(
            Prefix + handle,
            // The factory will never be invoked because the ReadOnlyEntryOptions set the DisableUnderlyingData flag
            cancel => throw new InvalidOperationException("Can't Happen"),
            ReadOnlyEntryOptions,
            cancellationToken: ct);
}
