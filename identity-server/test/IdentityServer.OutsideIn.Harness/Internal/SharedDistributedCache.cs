// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal;

/// <summary>
/// Forwards to an in-process <see cref="MemoryDistributedCache" />, standing in for a real distributed cache (design
/// doc D34). HybridCache 10.0's <c>DefaultHybridCache</c> special-cases a plain <see cref="MemoryDistributedCache" />
/// backend paired with a plain <see cref="Microsoft.Extensions.Caching.Memory.MemoryCache" /> L1 and silently drops
/// the L2 tier (see DefaultHybridCache.cs in dotnet/extensions), on the theory that an L2 identical in type to L1
/// adds nothing. That's wrong here: the harness's L1 is per-node, but this instance is one shared backing store for
/// every node in the topology, which is the whole point of <see cref="CachingMode.Distributed" />. Wrapping it in a
/// type HybridCache doesn't recognize keeps the L2 tier alive.
/// </summary>
internal sealed class SharedDistributedCache(MemoryDistributedCache inner) : IDistributedCache
{
    public byte[]? Get(string key) => inner.Get(key);

    public Task<byte[]?> GetAsync(string key, Ct token = default) => inner.GetAsync(key, token);

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => inner.Set(key, value, options);

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, Ct token = default) =>
        inner.SetAsync(key, value, options, token);

    public void Refresh(string key) => inner.Refresh(key);

    public Task RefreshAsync(string key, Ct token = default) => inner.RefreshAsync(key, token);

    public void Remove(string key) => inner.Remove(key);

    public Task RemoveAsync(string key, Ct token = default) => inner.RemoveAsync(key, token);
}
