// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces.Internal;

/// <summary>
/// An <see cref="IHybridCacheFactory"/> that wraps the keyed <see cref="HybridCache"/> in a
/// <see cref="SpaceAwareHybridCache"/> so the ambient space flows into cache factory delegates.
/// </summary>
internal sealed class SpaceAwareHybridCacheFactory(
    IServiceProvider serviceProvider,
    ISpaceContextAccessor spaceContextAccessor) : IHybridCacheFactory
{
    public HybridCache GetCache(string serviceKey)
    {
        var inner = serviceProvider.GetRequiredKeyedService<HybridCache>(serviceKey);

        return inner as SpaceAwareHybridCache ?? new SpaceAwareHybridCache(inner, spaceContextAccessor);
    }
}
