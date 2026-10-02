// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer;

/// <summary>
/// Keys used to resolve keyed services from dependency injection.
/// </summary>
public static class ServiceProviderKeys
{
    /// <summary>
    /// Service key for the <see cref="Microsoft.Extensions.Caching.Hybrid.HybridCache"/> instance
    /// used by configuration store caching decorators. Resolve it via
    /// <see cref="Duende.Spaces.IHybridCacheFactory"/> rather than injecting the keyed cache directly,
    /// so it picks up host behavior such as space awareness.
    /// </summary>
    public const string ConfigurationStoreCache = nameof(ConfigurationStoreCache);

    /// <summary>
    /// Service key for the <see cref="Microsoft.Extensions.Caching.Hybrid.HybridCache"/> instance
    /// used by operational store caching. Reserved for future use.
    /// </summary>
    public const string OperationalStoreCache = nameof(OperationalStoreCache);
}
