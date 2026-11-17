// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

/// <summary>
/// Wraps a real <c>DefaultHybridCache</c> and can hold the factory for one key in flight,
/// so a test can run another caller while the first is still running.
/// </summary>
internal sealed class GatedHybridCache : HybridCache, IDisposable
{
    private readonly ServiceProvider _innerServices =
        new ServiceCollection().AddHybridCache().Services.BuildServiceProvider();
    private readonly HybridCache _inner;
    private readonly object _lock = new();
    private KeyGate? _gate;

    public GatedHybridCache() => _inner = _innerServices.GetRequiredService<HybridCache>();

    public KeyGate Gate(string key)
    {
        var gate = new KeyGate(key);
        lock (_lock)
        {
            _gate = gate;
        }
        return gate;
    }

    public void Dispose() => _innerServices.Dispose();

    public override ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, Ct, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        Ct cancellationToken = default)
    {
        KeyGate? gate;
        lock (_lock)
        {
            gate = _gate;
        }

        if (gate is null || gate.Key != key)
        {
            return _inner.GetOrCreateAsync(key, state, factory, options, tags, cancellationToken);
        }

        Func<TState, Ct, ValueTask<T>> gatedFactory = async (s, ct) =>
        {
            gate.FactoryRan();
            await gate.WaitForRelease(ct);
            return await factory(s, ct);
        };

        return _inner.GetOrCreateAsync(key, state, gatedFactory, options, tags, cancellationToken);
    }

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        Ct cancellationToken = default)
        => _inner.SetAsync(key, value, options, tags, cancellationToken);

    public override ValueTask RemoveAsync(string key, Ct cancellationToken = default)
        => _inner.RemoveAsync(key, cancellationToken);

    public override ValueTask RemoveAsync(IEnumerable<string> keys, Ct cancellationToken = default)
        => _inner.RemoveAsync(keys, cancellationToken);

    public override ValueTask RemoveByTagAsync(string tag, Ct cancellationToken = default)
        => _inner.RemoveByTagAsync(tag, cancellationToken);

    public override ValueTask RemoveByTagAsync(IEnumerable<string> tags, Ct cancellationToken = default)
        => _inner.RemoveByTagAsync(tags, cancellationToken);

    internal sealed class KeyGate(string key)
    {
        private readonly TaskCompletionSource _factoryStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Key { get; } = key;

        public Task FactoryStarted => _factoryStarted.Task;

        public void Release() => _release.TrySetResult();

        internal void FactoryRan() => _factoryStarted.TrySetResult();

        internal Task WaitForRelease(Ct ct) => _release.Task.WaitAsync(ct);
    }
}
