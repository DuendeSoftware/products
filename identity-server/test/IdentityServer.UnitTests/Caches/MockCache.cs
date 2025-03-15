// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Services;

namespace IdentityServer.UnitTests.Caches;

public class MockCache<T> : ICache<T>
    where T : class
{
    public MockCache(IClock clock)
    {
        _clock = clock;
    }

    public class CacheItem
    {
        public T Value { get; set; }
        public DateTimeOffset Expiration { get; set; }
    }

    public Dictionary<string, CacheItem> CacheItems = new Dictionary<string, CacheItem>();

    private readonly IClock _clock;

    private bool TryGetValue(string key, out T item)
    {
        if (CacheItems.TryGetValue(key, out var cacheItem))
        {
            if (cacheItem.Expiration <= _clock.UtcNow)
            {
                item = cacheItem.Value;
                return true;
            }
        }

        item = null;
        return false;
    }

    private void Add(string key, T item, TimeSpan duration)
    {
        var ci = new CacheItem
        {
            Value = item,
            Expiration = _clock.UtcNow.Add(duration),
        };
        CacheItems[key] = ci;
    }

    public Task<T> GetAsync(string key)
    {
        TryGetValue(key, out var item);
        return Task.FromResult(item);
    }

    public async Task<T> GetOrAddAsync(string key, TimeSpan duration, Func<Task<T>> get)
    {
        if (!TryGetValue(key, out var item))
        {
            item = await get();
            Add(key, item, duration);
        }

        return item;
    }

    public Task RemoveAsync(string key)
    {
        CacheItems.Remove(key);
        return Task.CompletedTask;
    }

    public Task SetAsync(string key, T item, TimeSpan expiration)
    {
        Add(key, item, expiration);
        return Task.CompletedTask;
    }
}
