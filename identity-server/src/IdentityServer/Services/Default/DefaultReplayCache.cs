// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.Extensions.Caching.Distributed;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Default implementation of the replay cache using IDistributedCache
/// </summary>
public class DefaultReplayCache : IReplayCache
{
    private const string Prefix = nameof(DefaultReplayCache) + "-";

    private readonly IDistributedCache _cache;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="cache"></param>
    public DefaultReplayCache(IDistributedCache cache) => _cache = cache;

    /// <inheritdoc />
    public async Task AddAsync(string purpose, string handle, DateTimeOffset expiration, CT ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultReplayCache.Add");

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = expiration
        };

        await _cache.SetAsync(Prefix + purpose + handle, [], options, ct);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string purpose, string handle, CT ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultReplayCache.Exists");

        return (await _cache.GetAsync(Prefix + purpose + handle, ct)) != null;
    }
}
