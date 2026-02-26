// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.AspNetCore.Authentication.JwtBearer.TestFramework;

internal class TestHybridCache : HybridCache
{
    private readonly Dictionary<string, object> _cache = new();
    private readonly List<(string key, object value, HybridCacheEntryOptions? options)> _setAsyncCalls = new();
    private readonly List<(string key, HybridCacheEntryOptions? options)> _getOrCreateAsyncCalls = new();

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(string key, TState state, Func<TState, Ct, ValueTask<T>> factory, HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null, Ct ct = new())
    {
        _getOrCreateAsyncCalls.Add((key, options));

        if (_cache.TryGetValue(key, out var cached))
        {
            return (T)cached;
        }

        return default(T)!;
    }

    public override ValueTask SetAsync<T>(string key, T value, HybridCacheEntryOptions? options = null, IEnumerable<string>? tags = null,
        Ct ct = new())
    {
        _setAsyncCalls.Add((key, value!, options));
        _cache[key] = value!;
        return ValueTask.CompletedTask;
    }

    public override ValueTask RemoveAsync(string key, Ct ct = new()) => throw new NotImplementedException();

    public override ValueTask RemoveByTagAsync(string tag, Ct ct = new()) => throw new NotImplementedException();

    public IReadOnlyList<(string key, object value, HybridCacheEntryOptions? options)> SetAsyncCalls => _setAsyncCalls;
    public IReadOnlyList<(string key, HybridCacheEntryOptions? options)> GetOrCreateAsyncCalls => _getOrCreateAsyncCalls;

    public void Clear()
    {
        _cache.Clear();
        _setAsyncCalls.Clear();
        _getOrCreateAsyncCalls.Clear();
    }
}
