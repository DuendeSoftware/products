// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// The default <see cref="IHybridCacheFactory"/>, which returns the keyed <see cref="HybridCache"/>
/// unchanged. Replaced by Duende.Spaces when spaces are in use.
/// </summary>
internal sealed class DefaultHybridCacheFactory(IServiceProvider serviceProvider) : IHybridCacheFactory
{
    public HybridCache GetCache(string serviceKey) =>
        serviceProvider.GetRequiredKeyedService<HybridCache>(serviceKey);
}
