// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models a SAML 2.0 Service Provider configuration.
/// </summary>
public class SamlServiceProvider : IConnectedApplication
{
    /// <summary>
    /// Gets or sets the entity identifier for the Service Provider.
    /// This is typically a URI that uniquely identifies the SP.
    /// </summary>
    public string EntityId { get; set; } = default!;

    /// <summary>
    /// Gets or sets the display name for the Service Provider.
    /// Used for logging and consent screens.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description of the Service Provider.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this Service Provider is enabled.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the clock skew tolerance for validating SAML messages.
    /// If null, the global default from <c>SamlOptions.DefaultClockSkew</c> is used.
    /// </summary>
    public TimeSpan? ClockSkew { get; set; }

    /// <summary>
    /// Gets or sets the maximum age for SAML authentication requests.
    /// If null, the global default from <c>SamlOptions.DefaultRequestMaxAge</c> is used.
    /// </summary>
    public TimeSpan? RequestMaxAge { get; set; }

    /// <summary>
    /// Gets or sets the lifetime for SAML assertions issued to this Service Provider.
    /// Controls how long the generated assertion is valid (the window between NotBefore and NotOnOrAfter).
    /// If null, the global default from <c>SamlOptions.DefaultAssertionLifetime</c> is used.
    /// </summary>
    public TimeSpan? AssertionLifetime { get; set; }

    /// <summary>
    /// Gets or sets the Assertion Consumer Service (ACS) URLs where SAML responses can be sent.
    /// At least one URL is required.
    /// </summary>
    public ICollection<IndexedEndpoint> AssertionConsumerServiceUrls { get; set; } = new HashSet<IndexedEndpoint>();

    /// <summary>
    /// Gets or sets the Single Logout Service endpoints where LogoutRequest and LogoutResponse messages should be sent.
    /// These are the endpoints at the SP that handle SAML Single Logout protocol messages.
    /// Multiple endpoints can be configured for different bindings. Currently only HTTP-Redirect is supported.
    /// </summary>
    public ICollection<SamlEndpointType> SingleLogoutServiceUrls { get; set; } = new HashSet<SamlEndpointType>();

    /// <summary>
    /// Gets the Single Logout Service endpoint for the specified binding, or null if none is configured.
    /// </summary>
    /// <param name="binding">The SAML binding to find an endpoint for.</param>
    /// <returns>The matching endpoint, or null.</returns>
    public SamlEndpointType? GetSingleLogoutServiceEndpoint(SamlBinding binding) =>
        SingleLogoutServiceUrls.FirstOrDefault(e => e.Binding == binding);

    /// <summary>
    /// Gets or sets whether the SP's AuthnRequests must be signed.
    /// When null, the global <c>SamlOptions.WantAuthnRequestsSigned</c> setting is used.
    /// </summary>
    public bool? RequireSignedAuthnRequests { get; set; }

    /// <summary>
    /// Gets or sets whether LogoutResponse messages from this SP must be signed.
    /// When <c>null</c> (default), the global <c>SamlOptions.RequireSignedLogoutResponses</c> setting is used.
    /// Set to <c>false</c> to allow unsigned LogoutResponses from legacy SPs that don't sign
    /// front-channel responses (e.g., via HTTP-Redirect iframes).
    /// </summary>
    public bool? RequireSignedLogoutResponses { get; set; }

    /// <summary>
    /// Gets or sets the X.509 certificates used by the SP, each annotated with its intended use
    /// (signing, encryption, or both).
    /// </summary>
    public ICollection<ServiceProviderCertificate>? Certificates { get; set; }

    /// <summary>
    /// Gets or sets whether IdP-initiated SSO is allowed for this service provider.
    /// When false, IdP-initiated SSO requests will be rejected.
    /// Defaults to <c>false</c> (secure by default).
    /// </summary>
    public bool AllowIdpInitiated { get; set; }

    /// <summary>
    /// Gets or sets the identity resource scopes that this service provider is allowed to access.
    /// This is the authorization ceiling — each scope must resolve to an enabled identity resource.
    /// The associated user claims from those resources determine which claim types may be included
    /// in SAML assertions. At least one scope must be configured.
    /// </summary>
    public ICollection<string> AllowedScopes { get; set; } = new HashSet<string>();

    /// <summary>
    /// Service provider-specific mappings from claim types to SAML attribute names.
    /// When non-empty, these mappings replace the global DefaultClaimMappings entirely for this service provider.
    ///
    /// Key: claim type (e.g., "department")
    /// Value: SAML attribute name (e.g., "businessUnit")
    ///
    /// If empty, global mappings are used.
    /// </summary>
    public IDictionary<string, string> ClaimMappings { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Service provider-specific mappings from OIDC acr/amr claim values to SAML AuthnContextClassRef URIs.
    /// When non-empty, these mappings replace the global DefaultAuthnContextMappings entirely for this service provider.
    ///
    /// Key: OIDC acr or amr value (e.g., "pwd", "mfa")
    /// Value: SAML AuthnContextClassRef URI
    ///
    /// If empty, global mappings are used.
    /// </summary>
    public IDictionary<string, string> AuthnContextMappings { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the claim types to include in SAML assertions for this SP.
    /// Each entry must be a claim type defined by one of the identity resources in
    /// <see cref="AllowedScopes"/>. When empty, all claim types from AllowedScopes are available.
    /// </summary>
    public List<string> RequestedClaimTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the default NameID format for this SP.
    /// If null, the unspecified format is used.
    /// </summary>
    public string? DefaultNameIdFormat { get; set; } = "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified";

    /// <summary>
    /// Overrides <c>SamlOptions.EmailNameIdClaimType</c> for this service provider.
    /// When null, the global default is used.
    /// </summary>
    public string? EmailNameIdClaimType { get; set; }

    /// <summary>
    /// Gets or sets the signing behavior for SAML messages sent to this SP.
    /// If null, the global default from <c>SamlOptions.DefaultSigningBehavior</c> is used.
    /// </summary>
    public SamlSigningBehavior? SigningBehavior { get; set; }

    /// <summary>
    /// Gets or sets the allowed signature algorithms for validating signatures from this SP.
    /// When null or empty, the global default algorithms are used.
    /// Values should be algorithm identifier URIs, e.g.
    /// "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256".
    /// </summary>
    public List<string>? AllowedSignatureAlgorithms { get; set; }

#pragma warning disable CA1033
    string IConnectedApplication.Identifier => EntityId;
    string? IConnectedApplication.DisplayName => DisplayName;
    string? IConnectedApplication.Description => Description;
    bool IConnectedApplication.Enabled => Enabled;
    string IConnectedApplication.ProtocolType => IdentityServerConstants.ProtocolTypes.Saml2p;
    bool IConnectedApplication.RequireConsent => false;
#pragma warning restore CA1033
}
