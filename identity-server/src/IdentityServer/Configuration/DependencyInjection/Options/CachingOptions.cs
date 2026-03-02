// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Caching options.
/// </summary>
public class CachingOptions
{
    private static readonly TimeSpan Default = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets the client store expiration.
    /// </summary>
    /// <value>
    /// The client store expiration.
    /// </value>
    public TimeSpan ClientStoreExpiration { get; set; } = Default;

    /// <summary>
    /// Gets or sets the scope store expiration.
    /// </summary>
    /// <value>
    /// The scope store expiration.
    /// </value>
    public TimeSpan ResourceStoreExpiration { get; set; } = Default;

    /// <summary>
    /// Gets or sets the CORS origin expiration.
    /// </summary>
    public TimeSpan CorsExpiration { get; set; } = Default;

    /// <summary>
    /// Duration identity provider store cache duration
    /// </summary>
    public TimeSpan IdentityProviderCacheDuration { get; set; } = TimeSpan.FromMinutes(60);


    /// <summary>
    /// The timeout for concurrency locking in the default cache.
    /// </summary>
    public TimeSpan CacheLockTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets the SAML service provider store expiration.
    /// </summary>
    public TimeSpan SamlServiceProviderStoreExpiration { get; set; } = Default;
}
