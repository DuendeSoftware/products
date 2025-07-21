// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.Bff.Tests.TestInfra;

internal class TestHybridCache : HybridCache
{
    private ConcurrentDictionary<string, ValueTask<object>> _cache = new();
    public override async ValueTask<T> GetOrCreateAsync<TState, T>(string key, TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory, HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null, CancellationToken cancellationToken = new CancellationToken()) => (T)await _cache.GetOrAdd(key, async _ => (await factory(state, cancellationToken))!);

    public override ValueTask SetAsync<T>(string key, T value, HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        _cache[key] = new ValueTask<object>(value!);
        return ValueTask.CompletedTask;
    }

    public override ValueTask
        RemoveAsync(string key, CancellationToken cancellationToken = new CancellationToken())
    {
        _waitUntilRemoveAsyncCalled.Set();
        _cache.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }

    ManualResetEventSlim _waitUntilRemoveByTagAsyncCalled = new ManualResetEventSlim();
    ManualResetEventSlim _waitUntilRemoveAsyncCalled = new ManualResetEventSlim();

    public override ValueTask RemoveByTagAsync(string tag,
        CancellationToken cancellationToken = new CancellationToken())
    {
        _waitUntilRemoveByTagAsyncCalled.Set();
        _cache.Clear();
        return new ValueTask();
    }

    public void WaitUntilRemoveByTagAsyncCalled(TimeSpan until)
    {
        _waitUntilRemoveByTagAsyncCalled.Wait(until);
        if (!_waitUntilRemoveByTagAsyncCalled.IsSet)
        {
            throw new TimeoutException();
        }
    }

    public void WaitUntilRemoveAsyncCalled(TimeSpan until)
    {
        _waitUntilRemoveAsyncCalled.Wait(until);
        if (!_waitUntilRemoveAsyncCalled.IsSet)
        {
            throw new TimeoutException();
        }
    }

}
