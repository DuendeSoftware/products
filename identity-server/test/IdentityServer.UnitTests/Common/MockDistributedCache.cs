// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.Extensions.Caching.Distributed;

namespace UnitTests.Common;

public class MockDistributedCache : IDistributedCache
{
    public Dictionary<string, (byte[] Bytes, DistributedCacheEntryOptions Options)> Items { get; set; } = new Dictionary<string, (byte[] Bytes, DistributedCacheEntryOptions Options)>();

    public byte[] Get(string key)
    {
        if (Items.TryGetValue(key, out var item))
        {
            return item.Bytes;
        }
        return null;
    }

    public Task<byte[]> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

    public void Refresh(string key)
    {
    }

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

    public void Remove(string key) => Items.Remove(key);

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => Items[key] = (value, options);

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }
}
