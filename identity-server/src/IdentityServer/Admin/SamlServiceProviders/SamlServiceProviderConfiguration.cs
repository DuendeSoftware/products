// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Admin.SamlServiceProviders;

/// <summary>
/// Represents a SAML Service Provider configuration returned by admin read operations.
/// Immutable read model -- to modify, use <see cref="ToUpdate"/> to obtain a mutable
/// <see cref="UpdateSamlServiceProvider"/> and pass it to <see cref="ISamlServiceProviderAdmin.UpdateAsync"/>.
/// </summary>
public sealed class SamlServiceProviderConfiguration
{
    /// <summary>
    /// The SAML entity identifier for this Service Provider. Required. Primary business identifier.
    /// </summary>
    public required string EntityId { get; init; }

    /// <summary>
    /// Whether the Service Provider is enabled. Defaults to <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// A display-friendly name for the Service Provider (used in logging and consent screens).
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// A description of the Service Provider.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Clock skew tolerance for validating SAML messages.
    /// If <see langword="null"/>, the global default from <c>SamlOptions.DefaultClockSkew</c> is used.
    /// </summary>
    public TimeSpan? ClockSkew { get; init; }

    /// <summary>
    /// Maximum age for SAML authentication requests.
    /// If <see langword="null"/>, the global default from <c>SamlOptions.DefaultRequestMaxAge</c> is used.
    /// </summary>
    public TimeSpan? RequestMaxAge { get; init; }

    /// <summary>
    /// Lifetime for SAML assertions issued to this Service Provider.
    /// If <see langword="null"/>, the global default from <c>SamlOptions.DefaultAssertionLifetime</c> is used.
    /// </summary>
    public TimeSpan? AssertionLifetime { get; init; }

    /// <summary>
    /// Assertion Consumer Service (ACS) URLs where SAML responses can be sent.
    /// </summary>
    public IReadOnlyList<SamlIndexedEndpointConfiguration> AssertionConsumerServiceUrls { get; init; } = [];

    /// <summary>
    /// Single Logout Service endpoints where LogoutRequest and LogoutResponse messages should be sent.
    /// </summary>
    public IReadOnlyList<SamlEndpointConfiguration> SingleLogoutServiceUrls { get; init; } = [];

    /// <summary>
    /// Whether the SP's AuthnRequests must be signed.
    /// When <see langword="null"/>, the global <c>SamlOptions.WantAuthnRequestsSigned</c> setting is used.
    /// </summary>
    public bool? RequireSignedAuthnRequests { get; init; }

    /// <summary>
    /// Whether LogoutResponse messages from this SP must be signed.
    /// When <see langword="null"/>, the global <c>SamlOptions.RequireSignedLogoutResponses</c> setting is used.
    /// </summary>
    public bool? RequireSignedLogoutResponses { get; init; }

    /// <summary>
    /// X.509 certificates used by the SP. Unlike client secrets, these are public key material
    /// and full data is exposed on reads.
    /// </summary>
    public IReadOnlyList<SamlCertificateConfiguration> Certificates { get; init; } = [];

    /// <summary>
    /// Whether IdP-initiated SSO is allowed for this Service Provider.
    /// Defaults to <see langword="false"/> (secure by default).
    /// </summary>
    public bool AllowIdpInitiated { get; init; }

    /// <summary>
    /// Identity resource scopes that this Service Provider is allowed to access.
    /// </summary>
    public IReadOnlyList<string> AllowedScopes { get; init; } = [];

    /// <summary>
    /// Service provider-specific mappings from claim types to SAML attribute names.
    /// When non-empty, these replace the global DefaultClaimMappings for this SP.
    /// </summary>
    public IReadOnlyDictionary<string, string> ClaimMappings { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Service provider-specific mappings from OIDC acr/amr values to SAML AuthnContextClassRef URIs.
    /// When non-empty, these replace the global DefaultAuthnContextMappings for this SP.
    /// </summary>
    public IReadOnlyDictionary<string, string> AuthnContextMappings { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Claim types to include in SAML assertions for this SP.
    /// When empty, all claim types from AllowedScopes are available.
    /// </summary>
    public IReadOnlyList<string> RequestedClaimTypes { get; init; } = [];

    /// <summary>
    /// Default NameID format for this SP. If <see langword="null"/>, the unspecified format is used.
    /// </summary>
    public string? DefaultNameIdFormat { get; init; } = SamlConstants.NameIdentifierFormats.Unspecified;

    /// <summary>
    /// Overrides <c>SamlOptions.EmailNameIdClaimType</c> for this Service Provider.
    /// When <see langword="null"/>, the global default is used.
    /// </summary>
    public string? EmailNameIdClaimType { get; init; }

    /// <summary>
    /// Signing behavior for SAML messages sent to this SP.
    /// If <see langword="null"/>, the global default from <c>SamlOptions.DefaultSigningBehavior</c> is used.
    /// </summary>
    public SamlSigningBehavior? SigningBehavior { get; init; }

    /// <summary>
    /// Allowed signature algorithms for validating signatures from this SP.
    /// When <see langword="null"/> or empty, the global default algorithms are used.
    /// </summary>
    public IReadOnlyList<string> AllowedSignatureAlgorithms { get; init; } = [];

    /// <summary>
    /// Extended properties validated against the SAML service provider schema.
    /// Register a schema with <see cref="Duende.IdentityServer.Stores.Storage.SchemaIdExtensions"/> using the
    /// <c>SchemaId.SamlServiceProvider</c> ID to enable custom metadata on SAML SPs.
    /// </summary>
    public IReadOnlyCollection<AttributeValue> ExtendedProperties { get; init; } = [];

    /// <summary>
    /// Creates an update model from this configuration.
    /// </summary>
    public UpdateSamlServiceProvider ToUpdate() => new()
    {
        EntityId = EntityId,
        Enabled = Enabled,
        DisplayName = DisplayName,
        Description = Description,
        ClockSkew = ClockSkew,
        RequestMaxAge = RequestMaxAge,
        AssertionLifetime = AssertionLifetime,
        AssertionConsumerServiceUrls = CopyIndexedEndpoints(),
        SingleLogoutServiceUrls = CopyEndpoints(),
        RequireSignedAuthnRequests = RequireSignedAuthnRequests,
        RequireSignedLogoutResponses = RequireSignedLogoutResponses,
        Certificates = CopyCertificates(),
        AllowIdpInitiated = AllowIdpInitiated,
        AllowedScopes = Copy(AllowedScopes),
        ClaimMappings = CopyDictionary(ClaimMappings),
        AuthnContextMappings = CopyDictionary(AuthnContextMappings),
        RequestedClaimTypes = Copy(RequestedClaimTypes),
        DefaultNameIdFormat = DefaultNameIdFormat,
        EmailNameIdClaimType = EmailNameIdClaimType,
        SigningBehavior = SigningBehavior,
        AllowedSignatureAlgorithms = Copy(AllowedSignatureAlgorithms),
        ExtendedProperties = CopyExtendedProperties()
    };

    /// <summary>
    /// Creates a create model from this configuration.
    /// </summary>
    public CreateSamlServiceProvider ToCreate()
    {
        var update = ToUpdate();
        return new CreateSamlServiceProvider
        {
            EntityId = update.EntityId,
            Enabled = update.Enabled,
            DisplayName = update.DisplayName,
            Description = update.Description,
            ClockSkew = update.ClockSkew,
            RequestMaxAge = update.RequestMaxAge,
            AssertionLifetime = update.AssertionLifetime,
            AssertionConsumerServiceUrls = update.AssertionConsumerServiceUrls,
            SingleLogoutServiceUrls = update.SingleLogoutServiceUrls,
            RequireSignedAuthnRequests = update.RequireSignedAuthnRequests,
            RequireSignedLogoutResponses = update.RequireSignedLogoutResponses,
            Certificates = update.Certificates,
            AllowIdpInitiated = update.AllowIdpInitiated,
            AllowedScopes = update.AllowedScopes,
            ClaimMappings = update.ClaimMappings,
            AuthnContextMappings = update.AuthnContextMappings,
            RequestedClaimTypes = update.RequestedClaimTypes,
            DefaultNameIdFormat = update.DefaultNameIdFormat,
            EmailNameIdClaimType = update.EmailNameIdClaimType,
            SigningBehavior = update.SigningBehavior,
            AllowedSignatureAlgorithms = update.AllowedSignatureAlgorithms,
            ExtendedProperties = update.ExtendedProperties
        };
    }

    private static List<string> Copy(IReadOnlyList<string> values) => [.. values];

    private static Dictionary<string, string> CopyDictionary(IReadOnlyDictionary<string, string> values) => new(values);

    private List<SamlIndexedEndpointConfiguration> CopyIndexedEndpoints() =>
        [.. AssertionConsumerServiceUrls.Select(e => new SamlIndexedEndpointConfiguration
        {
            Location = e.Location,
            Binding = e.Binding,
            Index = e.Index,
            IsDefault = e.IsDefault
        })];

    private List<SamlEndpointConfiguration> CopyEndpoints() =>
        [.. SingleLogoutServiceUrls.Select(e => new SamlEndpointConfiguration
        {
            Location = e.Location,
            Binding = e.Binding
        })];

    private List<SamlCertificateInput> CopyCertificates() => [.. Certificates.Select(c => c.ToInput())];

    private AttributeValueCollection CopyExtendedProperties()
    {
        var copy = new AttributeValueCollection();
        foreach (var attribute in ExtendedProperties)
        {
            copy.Set(attribute);
        }

        return copy;
    }
}
