// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.Spaces;

/// <summary>
/// Resolves the <see cref="HybridCache"/> registered under a given service key, applying any
/// behavior required by the host (e.g. space awareness when Duende.Spaces is in use).
/// </summary>
/// <remarks>
/// Consumers should depend on this factory rather than injecting a keyed <see cref="HybridCache"/>
/// directly, so that the host can decorate the cache without rewriting service registrations.
/// </remarks>
public interface IHybridCacheFactory
{
    /// <summary>
    /// Gets the <see cref="HybridCache"/> registered under <paramref name="serviceKey"/>.
    /// </summary>
    /// <param name="serviceKey">The key the <see cref="HybridCache"/> is registered under.</param>
    /// <returns>The cache to use.</returns>
    HybridCache GetCache(string serviceKey);
}
