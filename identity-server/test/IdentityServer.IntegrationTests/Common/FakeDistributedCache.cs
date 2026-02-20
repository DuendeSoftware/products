// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Microsoft.Extensions.Caching.Distributed;

namespace Duende.IdentityServer.IntegrationTests.Common;

internal class FakeDistributedCache(TimeProvider timeProvider) : IDistributedCache
{
    private readonly Dictionary<string, CacheEntry> _items = new();

    private record CacheEntry(byte[] Value, DateTimeOffset? AbsoluteExpiration);

    public byte[]? Get(string key)
    {
        if (!_items.TryGetValue(key, out var entry))
        {
            return null;
        }

        if (!entry.AbsoluteExpiration.HasValue || timeProvider.GetUtcNow() <= entry.AbsoluteExpiration.Value)
        {
            return entry.Value;
        }

        _items.Remove(key);
        return null;
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        var absoluteExpiration = options.AbsoluteExpirationRelativeToNow.HasValue
            ? timeProvider.GetUtcNow().Add(options.AbsoluteExpirationRelativeToNow.Value)
            : options.AbsoluteExpiration;

        _items[key] = new CacheEntry(value, absoluteExpiration);
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void Remove(string key) => _items.Remove(key);

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    public void Refresh(string key)
    {
        // not currently needed
    }

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
}
