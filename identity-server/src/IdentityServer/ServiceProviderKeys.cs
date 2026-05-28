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
    /// used by configuration store caching decorators
    /// (<c>CachingClientStore</c>, <c>CachingResourceStore</c>, <c>CachingCorsPolicyService</c>,
    /// <c>CachingIdentityProviderStore</c>).
    /// </summary>
    public const string ConfigurationStoreCache = nameof(ConfigurationStoreCache);

    /// <summary>
    /// Service key for the <see cref="Microsoft.Extensions.Caching.Hybrid.HybridCache"/> instance
    /// used by operational store caching. Reserved for future use.
    /// </summary>
    public const string OperationalStoreCache = nameof(OperationalStoreCache);
}
