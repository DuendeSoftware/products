// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography.X509Certificates;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models a SAML 2.0 Service Provider configuration.
/// </summary>
public class SamlServiceProvider
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
    public string DisplayName { get; set; } = default!;

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
    /// Gets or sets the Assertion Consumer Service (ACS) URLs where SAML responses can be sent.
    /// At least one URL is required.
    /// </summary>
    public ICollection<Uri> AssertionConsumerServiceUrls { get; set; } = new HashSet<Uri>();

    /// <summary>
    /// Gets or sets the SAML binding used for the Assertion Consumer Service.
    /// </summary>
    public SamlBinding AssertionConsumerServiceBinding { get; set; }

    /// <summary>
    /// Gets or sets the Single Logout Service endpoint where LogoutRequest and LogoutResponse messages should be sent.
    /// This is the endpoint at the SP that handles SAML Single Logout protocol messages.
    /// </summary>
    public SamlEndpointType? SingleLogoutServiceUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the SP's AuthnRequests must be signed.
    /// </summary>
    public bool RequireSignedAuthnRequests { get; set; }

    /// <summary>
    /// Gets or sets the X.509 certificates used by the SP to sign messages.
    /// </summary>
    public ICollection<X509Certificate2>? SigningCertificates { get; set; }

    /// <summary>
    /// Gets or sets the X.509 certificates used to encrypt SAML assertions for the SP.
    /// </summary>
    public ICollection<X509Certificate2>? EncryptionCertificates { get; set; }

    /// <summary>
    /// Gets or sets whether SAML assertions should be encrypted for this SP.
    /// </summary>
    public bool EncryptAssertions { get; set; }

    /// <summary>
    /// Gets or sets whether consent is required for this SP.
    /// </summary>
    public bool RequireConsent { get; set; }

    /// <summary>
    /// Gets or sets whether IdP-initiated SSO is allowed for this service provider.
    /// When false, IdP-initiated SSO requests will be rejected.
    /// Defaults to <c>false</c> (secure by default).
    /// </summary>
    public bool AllowIdpInitiated { get; set; }

    /// <summary>
    /// Service provider-specific mappings from claim types to SAML attribute names.
    /// These mappings override the global DefaultClaimMappings for this service provider.
    ///
    /// Key: claim type (e.g., "department")
    /// Value: SAML attribute name (e.g., "businessUnit")
    ///
    /// If empty, only global mappings are used.
    /// </summary>
    public IDictionary<string, string> ClaimMappings { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the default NameID format for this SP.
    /// If null, the unspecified format is used.
    /// </summary>
    public string? DefaultNameIdFormat { get; set; } = "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified";

    /// <summary>
    /// Gets or sets the claim type used to resolve a persistent name identifier for this SP.
    /// Overrides <c>SamlOptions.DefaultPersistentNameIdentifierClaimType</c>.
    /// </summary>
    public string? DefaultPersistentNameIdentifierClaimType { get; set; }

    /// <summary>
    /// Gets or sets the signing behavior for SAML messages sent to this SP.
    /// If null, the global default from <c>SamlOptions.DefaultSigningBehavior</c> is used.
    /// </summary>
    public SamlSigningBehavior? SigningBehavior { get; set; }
}
