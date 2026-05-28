// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable


namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Settings for the OpenID Connect discovery document endpoint, including flags to control
/// which sections are included and support for custom entries.
/// </summary>
/// <remarks>
/// To take full control over the rendering of the discovery and JWKS documents, implement
/// <c>IDiscoveryResponseGenerator</c> or derive from the default implementation.
/// </remarks>
public class DiscoveryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether endpoint URLs (e.g., <c>authorization_endpoint</c>, <c>token_endpoint</c>) are included in
    /// the discovery document.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool ShowEndpoints { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the <c>jwks_uri</c> is included in the discovery document and the JWKS endpoint is enabled.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool ShowKeySet { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether identity resources (OpenID scopes) are included in the <c>scopes_supported</c> array of the
    /// discovery document.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool ShowIdentityScopes { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether API scopes are included in the <c>scopes_supported</c> array of the discovery document.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool ShowApiScopes { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the <c>claims_supported</c> array is included in the discovery document.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool ShowClaims { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the <c>response_types_supported</c> array is included in the discovery document.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool ShowResponseTypes { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the <c>response_modes_supported</c> array is included in the discovery document.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool ShowResponseModes { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the <c>grant_types_supported</c> array is included in the discovery document.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool ShowGrantTypes { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether extension (custom) grant types are included in the <c>grant_types_supported</c> array of the
    /// discovery document.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool ShowExtensionGrantTypes { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the <c>token_endpoint_auth_methods_supported</c> array is included in the discovery document.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool ShowTokenEndpointAuthenticationMethods { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the supported authentication methods for the revocation endpoint are included in the
    /// discovery document.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool ShowRevocationEndpointAuthenticationMethods { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the supported authentication methods for the introspection endpoint are included in the
    /// discovery document.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool ShowIntrospectionEndpointAuthenticationMethods { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether relative paths that begin with <c>~/</c> in <see cref="CustomEntries"/> are expanded into
    /// absolute URLs beneath the IdentityServer base address.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>. For example, if IdentityServer is hosted at
    /// <c>https://localhost:5001</c>, a custom entry value of <c>~/custom</c> is expanded to
    /// <c>https://localhost:5001/custom</c>.
    /// </remarks>
    public bool ExpandRelativePathsInCustomEntries { get; set; } = true;

    /// <summary>
    /// Gets or sets the options for how the dynamic client registration endpoint is advertised in the discovery
    /// document.
    /// </summary>
    public DynamicClientRegistrationDiscoveryOptions DynamicClientRegistration { get; set; } = new DynamicClientRegistrationDiscoveryOptions();

    /// <summary>
    /// Gets or sets the <c>max-age</c> value (in seconds) of the <c>Cache-Control</c> response header
    /// on the discovery document. Set to <c>0</c> to emit <c>no-cache</c> headers.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>null</c>, which means no <c>Cache-Control</c> header is set. Configuring
    /// this gives clients a hint about how often they should refresh their cached copy of the
    /// discovery document.
    /// </remarks>
    public int? ResponseCacheInterval { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the discovery document is cached in the distributed cache.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. When enabled, the discovery document is cached using the registered
    /// <c>IDistributedCache</c> for the duration specified by <see cref="DiscoveryDocumentCacheDuration"/>.
    /// </remarks>
    public bool EnableDiscoveryDocumentCache { get; set; }

    /// <summary>
    /// Gets or sets the duration for which the discovery document is cached in the distributed cache.
    /// </summary>
    /// <remarks>
    /// Defaults to 1 minute. Only applies when <see cref="EnableDiscoveryDocumentCache"/> is <c>true</c>.
    /// </remarks>
    public TimeSpan DiscoveryDocumentCacheDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the additional key-value entries to include in the discovery document.
    /// </summary>
    /// <remarks>
    /// Values that are strings beginning with <c>~/</c> are expanded to absolute URLs when
    /// <see cref="ExpandRelativePathsInCustomEntries"/> is <c>true</c>.
    /// </remarks>
    public Dictionary<string, object> CustomEntries { get; set; } = new Dictionary<string, object>();
}
