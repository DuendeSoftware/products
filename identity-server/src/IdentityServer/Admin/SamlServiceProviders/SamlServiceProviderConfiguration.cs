// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;

namespace Duende.IdentityServer.Admin.SamlServiceProviders;

/// <summary>
/// Represents a SAML Service Provider configuration for admin CRUD operations.
/// Mutable class — callers can <c>Get</c>, modify properties, and pass back to <c>UpdateAsync</c>.
/// </summary>
public class SamlServiceProviderConfiguration
{
    /// <summary>
    /// The SAML entity identifier for this Service Provider. Required. Primary business identifier.
    /// </summary>
    public required string EntityId { get; set; }

    /// <summary>
    /// Whether the Service Provider is enabled. Defaults to <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// A display-friendly name for the Service Provider (used in logging and consent screens).
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// A description of the Service Provider.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Clock skew tolerance for validating SAML messages.
    /// If <see langword="null"/>, the global default from <c>SamlOptions.DefaultClockSkew</c> is used.
    /// </summary>
    public TimeSpan? ClockSkew { get; set; }

    /// <summary>
    /// Maximum age for SAML authentication requests.
    /// If <see langword="null"/>, the global default from <c>SamlOptions.DefaultRequestMaxAge</c> is used.
    /// </summary>
    public TimeSpan? RequestMaxAge { get; set; }

    /// <summary>
    /// Lifetime for SAML assertions issued to this Service Provider.
    /// If <see langword="null"/>, the global default from <c>SamlOptions.DefaultAssertionLifetime</c> is used.
    /// </summary>
    public TimeSpan? AssertionLifetime { get; set; }

    /// <summary>
    /// Assertion Consumer Service (ACS) URLs where SAML responses can be sent.
    /// </summary>
    public List<SamlIndexedEndpointConfiguration>? AssertionConsumerServiceUrls { get; set; }

    /// <summary>
    /// Single Logout Service endpoints where LogoutRequest and LogoutResponse messages should be sent.
    /// </summary>
    public List<SamlEndpointConfiguration>? SingleLogoutServiceUrls { get; set; }

    /// <summary>
    /// Whether the SP's AuthnRequests must be signed.
    /// When <see langword="null"/>, the global <c>SamlOptions.WantAuthnRequestsSigned</c> setting is used.
    /// </summary>
    public bool? RequireSignedAuthnRequests { get; set; }

    /// <summary>
    /// Whether LogoutResponse messages from this SP must be signed.
    /// When <see langword="null"/>, the global <c>SamlOptions.RequireSignedLogoutResponses</c> setting is used.
    /// </summary>
    public bool? RequireSignedLogoutResponses { get; set; }

    /// <summary>
    /// X.509 certificates used by the SP. Unlike client secrets, these are public key material
    /// and full data is exposed on reads.
    /// </summary>
    public List<SamlCertificateConfiguration>? Certificates { get; set; }

    /// <summary>
    /// Whether IdP-initiated SSO is allowed for this Service Provider.
    /// Defaults to <see langword="false"/> (secure by default).
    /// </summary>
    public bool AllowIdpInitiated { get; set; }

    /// <summary>
    /// Identity resource scopes that this Service Provider is allowed to access.
    /// </summary>
    public List<string>? AllowedScopes { get; set; }

    /// <summary>
    /// Service provider-specific mappings from claim types to SAML attribute names.
    /// When non-empty, these replace the global DefaultClaimMappings for this SP.
    /// </summary>
    public Dictionary<string, string>? ClaimMappings { get; set; }

    /// <summary>
    /// Service provider-specific mappings from OIDC acr/amr values to SAML AuthnContextClassRef URIs.
    /// When non-empty, these replace the global DefaultAuthnContextMappings for this SP.
    /// </summary>
    public Dictionary<string, string>? AuthnContextMappings { get; set; }

    /// <summary>
    /// Claim types to include in SAML assertions for this SP.
    /// When empty, all claim types from AllowedScopes are available.
    /// </summary>
    public List<string>? RequestedClaimTypes { get; set; }

    /// <summary>
    /// Default NameID format for this SP. If <see langword="null"/>, the unspecified format is used.
    /// </summary>
    public string? DefaultNameIdFormat { get; set; } = SamlConstants.NameIdentifierFormats.Unspecified;

    /// <summary>
    /// Overrides <c>SamlOptions.EmailNameIdClaimType</c> for this Service Provider.
    /// When <see langword="null"/>, the global default is used.
    /// </summary>
    public string? EmailNameIdClaimType { get; set; }

    /// <summary>
    /// Signing behavior for SAML messages sent to this SP.
    /// If <see langword="null"/>, the global default from <c>SamlOptions.DefaultSigningBehavior</c> is used.
    /// </summary>
    public SamlSigningBehavior? SigningBehavior { get; set; }

    /// <summary>
    /// Allowed signature algorithms for validating signatures from this SP.
    /// When <see langword="null"/> or empty, the global default algorithms are used.
    /// </summary>
    public List<string>? AllowedSignatureAlgorithms { get; set; }

    /// <summary>
    /// Data version for optimistic concurrency. <see langword="null"/> for new service providers.
    /// </summary>
    public DataVersion? Version { get; set; }
}
