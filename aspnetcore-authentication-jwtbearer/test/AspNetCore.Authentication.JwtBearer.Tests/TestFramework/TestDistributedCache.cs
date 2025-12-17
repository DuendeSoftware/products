// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;

namespace Duende.AspNetCore.Authentication.JwtBearer.TestFramework;

internal class TestDistributedCache : IDistributedCache
{
    private readonly Dictionary<string, byte[]> _cache = new();

    public List<string> GetCalls { get; } = new();
    public List<string> GetAsyncCalls { get; } = new();
    public List<string> RefreshCalls { get; } = new();
    public List<string> RefreshAsyncCalls { get; } = new();
    public List<string> RemoveCalls { get; } = new();
    public List<string> RemoveAsyncCalls { get; } = new();
    public List<string> SetCalls { get; } = new();
    public List<string> SetAsyncCalls { get; } = new();

    public byte[]? Get(string key)
    {
        GetCalls.Add(key);
        _cache.TryGetValue(key, out var value);
        return value;
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken token = new CancellationToken())
    {
        GetAsyncCalls.Add(key);
        _cache.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public void Refresh(string key) => RefreshCalls.Add(key); // Refresh is a no-op for an in-memory cache

    public Task RefreshAsync(string key, CancellationToken token = new CancellationToken())
    {
        RefreshAsyncCalls.Add(key);
        // Refresh is a no-op for an in-memory cache
        return Task.CompletedTask;
    }

    public void Remove(string key)
    {
        RemoveCalls.Add(key);
        _cache.Remove(key);
    }

    public Task RemoveAsync(string key, CancellationToken token = new CancellationToken())
    {
        RemoveAsyncCalls.Add(key);
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        SetCalls.Add(key);
        _cache[key] = value;
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        CancellationToken token = new CancellationToken())
    {
        SetAsyncCalls.Add(key);
        _cache[key] = value;
        return Task.CompletedTask;
    }
}
