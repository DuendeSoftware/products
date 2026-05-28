// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Cache duration settings for client, resource, CORS, and identity provider store lookups.
/// These settings only apply when the respective caching has been enabled during service registration.
/// </summary>
public class CachingOptions
{
    private static readonly TimeSpan Default = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets how long client configuration loaded from the client store is cached.
    /// </summary>
    /// <remarks>
    /// Defaults to 15 minutes. Only effective when client store caching is enabled
    /// (e.g., via <c>AddClientStoreCache</c>).
    /// </remarks>
    public TimeSpan ClientStoreExpiration { get; set; } = Default;

    /// <summary>
    /// Gets or sets how long identity and API resource configuration loaded from the resource store is cached.
    /// </summary>
    /// <remarks>
    /// Defaults to 15 minutes. Only effective when resource store caching is enabled
    /// (e.g., via <c>AddResourceStoreCache</c>).
    /// </remarks>
    public TimeSpan ResourceStoreExpiration { get; set; } = Default;

    /// <summary>
    /// Gets or sets how long CORS origin configuration loaded from the CORS policy service is cached.
    /// </summary>
    /// <remarks>
    /// Defaults to 15 minutes. Only effective when CORS caching is enabled
    /// (e.g., via <c>AddCorsPolicyCache</c>).
    /// </remarks>
    public TimeSpan CorsExpiration { get; set; } = Default;

    /// <summary>
    /// Gets or sets how long identity provider configuration loaded from the identity provider store is cached.
    /// </summary>
    /// <remarks>
    /// Defaults to 60 minutes. Only effective when identity provider store caching is enabled.
    /// </remarks>
    public TimeSpan IdentityProviderCacheDuration { get; set; } = TimeSpan.FromMinutes(60);

    /// <summary>
    /// Gets or sets how long SAML service provider configuration loaded from the service provider store is cached.
    /// </summary>
    /// <remarks>
    /// Defaults to 15 minutes. Only effective when SAML service provider store caching is enabled
    /// (e.g., via <c>AddSamlServiceProviderStoreCache</c>).
    /// </remarks>
    public TimeSpan SamlServiceProviderStoreExpiration { get; set; } = Default;


    /// <summary>
    /// Gets or sets the timeout for concurrency locking in the default cache.
    /// </summary>
    /// <remarks>
    /// Defaults to 60 seconds. This property is no longer used by configuration-store caching
    /// (HybridCache provides built-in stampede protection). It is still used internally by
    /// KeyManager for key-management locking.
    /// </remarks>
    [Obsolete("CacheLockTimeout is no longer used by configuration-store caching (HybridCache provides built-in stampede protection). It is still used internally by KeyManager for key-management locking.")]
    public TimeSpan CacheLockTimeout { get; set; } = TimeSpan.FromSeconds(60);
}
