// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.ObjectModel;
using System.Security.Claims;
using Duende.IdentityServer.Models;
using SamlConstants = Duende.IdentityServer.Internal.Saml.SamlConstants;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Options for SAML 2.0 Identity Provider functionality.
/// </summary>
public class SamlOptions
{
    /// <summary>
    /// Gets or sets the metadata validity duration (optional).
    /// If set, metadata will include a validUntil attribute.
    /// Defaults to 7 days.
    /// </summary>
    public TimeSpan? MetadataValidityDuration { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets whether the IdP requires signed AuthnRequests.
    /// Defaults to false.
    /// </summary>
    public bool WantAuthnRequestsSigned { get; set; }

    /// <summary>
    /// Default attribute name format to use when SP doesn't specify.
    /// Common values:
    /// - "urn:oasis:names:tc:SAML:2.0:attrname-format:uri" (for OID format)
    /// - "urn:oasis:names:tc:SAML:2.0:attrname-format:basic" (for simple names)
    /// Default: Uri (most common)
    /// </summary>
    public string DefaultAttributeNameFormat { get; set; }
        = SamlConstants.AttributeNameFormats.Uri;

    /// <summary>
    /// Default claim type to use when resolving a persistent name identifier based on where
    /// the host application has populated the value. Persistent name identifiers will not be
    /// generated and are the responsibility of the host application to create.
    /// </summary>
    public string DefaultPersistentNameIdentifierClaimType { get; set; } = ClaimTypes.NameIdentifier;

    /// <summary>
    /// Default mappings from claim types to SAML attribute names.
    /// Key: claim type (e.g., "email", "name")
    /// Value: SAML attribute name (e.g., "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")
    ///
    /// Includes common OIDC to SAML attribute mappings by default.
    /// Service providers can override these mappings via SamlServiceProvider.ClaimMappings.
    ///
    /// If a claim type is not in this dictionary, the claim will be excluded from the SAML assertion.
    /// </summary>
    public ReadOnlyDictionary<string, string> DefaultClaimMappings { get; init; } =
        new(new Dictionary<string, string>
        {
            ["name"] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
            ["email"] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
            ["role"] = "http://schemas.xmlsoap.org/ws/2005/05/identity/role",
        });

    /// <summary>
    /// Gets or sets the supported NameID formats.
    /// Defaults to EmailAddress, Persistent, Transient, and Unspecified.
    /// </summary>
    public Collection<string> SupportedNameIdFormats { get; init; } =
    [
        SamlConstants.NameIdentifierFormats.EmailAddress,
        SamlConstants.NameIdentifierFormats.Persistent,
        SamlConstants.NameIdentifierFormats.Transient,
        SamlConstants.NameIdentifierFormats.Unspecified
    ];

    /// <summary>
    /// Gets or sets the default clock skew tolerance for SAML message validation.
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan DefaultClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the default maximum age for SAML authentication requests.
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan DefaultRequestMaxAge { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the default signing behavior for SAML messages.
    /// Defaults to <see cref="Models.SamlSigningBehavior.SignAssertion"/>.
    /// </summary>
    public SamlSigningBehavior DefaultSigningBehavior { get; set; } = SamlSigningBehavior.SignAssertion;

    /// <summary>
    /// Maximum length of the RelayState parameter, measured in bytes of its UTF-8 encoding.
    /// SAML spec recommends 80 bytes, but can be increased for SPs that support longer values.
    /// Default: 80 (UTF-8 bytes).
    /// </summary>
    public int MaxRelayStateLength { get; set; } = 80;

    /// <summary>
    /// Gets or sets the user interaction options for SAML endpoints.
    /// </summary>
    public SamlUserInteractionOptions UserInteraction { get; set; } = new();
}

/// <summary>
/// Options for SAML user interaction endpoint paths.
/// </summary>
public class SamlUserInteractionOptions
{
    /// <summary>
    /// Gets or sets the base route for all SAML endpoints.
    /// Default: "/saml".
    /// </summary>
    public string Route { get; set; } = SamlConstants.Urls.SamlRoute;

    /// <summary>
    /// Gets or sets the path for the SAML metadata endpoint.
    /// Default: "/metadata".
    /// </summary>
    public string Metadata { get; set; } = SamlConstants.Urls.Metadata;

    /// <summary>
    /// Gets or sets the path for the SAML sign-in endpoint.
    /// Default: "/signin".
    /// </summary>
    public string SignInPath { get; set; } = SamlConstants.Urls.SignIn;

    /// <summary>
    /// Gets or sets the path for the SAML sign-in callback endpoint.
    /// Default: "/signin_callback".
    /// </summary>
    public string SignInCallbackPath { get; set; } = SamlConstants.Urls.SigninCallback;

    /// <summary>
    /// Gets or sets the path for the IdP-initiated SSO endpoint.
    /// Default: "/idp-initiated".
    /// </summary>
    public string IdpInitiatedPath { get; set; } = SamlConstants.Urls.IdpInitiated;

    /// <summary>
    /// Gets or sets the path for the SAML single logout endpoint.
    /// Default: "/logout".
    /// </summary>
    public string SingleLogoutPath { get; set; } = SamlConstants.Urls.SingleLogout;

    /// <summary>
    /// Gets or sets the path for the SAML single logout callback endpoint.
    /// Default: "/logout_callback".
    /// </summary>
    public string SingleLogoutCallbackPath { get; set; } = SamlConstants.Urls.SingleLogoutCallback;
}
