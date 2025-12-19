// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Default implementation of the replay cache using Hybrid Cache
/// </summary>
/// <remarks>
/// Constructs new instances of <see cref="ReplayCache"/>.
/// </remarks>
internal class ReplayCache(DPoPHybridCacheProvider cacheProvider) : IReplayCache
{
    private const string Prefix = "DPoPJwtBearerEvents-DPoPReplay-jti-";

    private HybridCache Cache
    {
        get
        {
            field ??= cacheProvider.GetCache();
            return field;
        }
    }

    public async Task Add(string handle, TimeSpan expiration, CancellationToken ct)
    {
        using var activity = Tracing.ActivitySource.StartActivity("ReplayCache.Add");

        var options = new HybridCacheEntryOptions
        {
            Expiration = expiration
        };

        await Cache.SetAsync(Prefix + handle, true, options, cancellationToken: ct);
    }

    private static readonly HybridCacheEntryOptions ReadOnlyEntryOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
                | HybridCacheEntryFlags.DisableDistributedCacheWrite
                | HybridCacheEntryFlags.DisableUnderlyingData
    };

    public async Task<bool> Exists(string handle, CancellationToken ct)
    {
        using var activity = Tracing.ActivitySource.StartActivity("ReplayCache.Exists");

        return await Cache.GetOrCreateAsync<bool>(
            Prefix + handle,
            // The factory will never be invoked because the ReadOnlyEntryOptions set the DisableUnderlyingData flag
            cancel => throw new InvalidOperationException("Can't Happen"),
            ReadOnlyEntryOptions,
            cancellationToken: ct);
    }
}
