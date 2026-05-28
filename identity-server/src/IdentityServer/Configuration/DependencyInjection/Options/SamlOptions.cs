// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.ObjectModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Xml;
using SamlConstants = Duende.IdentityServer.Saml.SamlConstants;
using Samlp = Duende.IdentityServer.Saml.Samlp;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Options for SAML 2.0 Identity Provider functionality.
/// </summary>
public class SamlOptions
{
    /// <summary>
    /// The Entity Id of this SAML 2.0 Identity Provider. Defaults to null,
    /// which derives the entity ID from the OIDC issuer combined with <see cref="EntityIdPath"/>.
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// Path component appended to the OIDC issuer to form the SAML entity ID.
    /// Ignored if <see cref="EntityId"/> is set explicitly.
    /// Defaults to "/Saml2".
    /// </summary>
    public string EntityIdPath { get; set; } = SamlConstants.Defaults.Saml2Path;

    /// <summary>
    /// Gets or sets whether the IdP requires signed AuthnRequests.
    /// Defaults to true.
    /// </summary>
    public bool WantAuthnRequestsSigned { get; set; } = true;

    /// <summary>
    /// Default mappings from claim types to SAML attribute names.
    /// Key: claim type (e.g., "email", "name")
    /// Value: SAML attribute name (e.g., "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")
    ///
    /// Includes common OIDC to SAML attribute mappings by default.
    /// Service providers can override these mappings via SamlServiceProvider.ClaimMappings.
    ///
    /// If a claim type is not in this dictionary, it will be passed through using the claim type as the attribute name.
    /// </summary>
    public ReadOnlyDictionary<string, string> DefaultClaimMappings { get; init; } =
        new(new Dictionary<string, string>
        {
            ["name"] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
            ["email"] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
            ["role"] = "http://schemas.xmlsoap.org/ws/2005/05/identity/role",
        });

    /// <summary>
    /// Default mappings from OIDC acr/amr claim values to SAML AuthnContextClassRef URIs.
    /// Key: OIDC acr or amr value (e.g., "pwd", "mfa", "external")
    /// Value: SAML AuthnContextClassRef URI (e.g., "urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport")
    ///
    /// Used during AuthnStatement generation. The generator checks acr first, then amr claims
    /// against this map. If no match is found, falls back to Unspecified.
    /// Service providers can override via <see cref="SamlServiceProvider.AuthnContextMappings"/>.
    /// </summary>
    public ReadOnlyDictionary<string, string> DefaultAuthnContextMappings { get; init; } =
        new(new Dictionary<string, string>
        {
            ["pwd"] = SamlConstants.AuthnContextClasses.PasswordProtectedTransport,
            ["external"] = SamlConstants.AuthnContextClasses.Unspecified,
        });

    /// <summary>
    /// Gets or sets the supported NameID formats.
    /// Defaults to EmailAddress and Unspecified.
    /// </summary>
    public Collection<string> SupportedNameIdFormats { get; init; } =
    [
        SamlConstants.NameIdentifierFormats.EmailAddress,
        SamlConstants.NameIdentifierFormats.Unspecified
    ];

    /// <summary>
    /// Gets or sets the claim type used to source the value for email NameID format.
    /// Defaults to <c>"email"</c>. Per-SP override is available via
    /// <c>SamlServiceProvider.EmailNameIdClaimType</c>.
    /// </summary>
    public string EmailNameIdClaimType { get; set; } = "email";

    /// <summary>
    /// Gets or sets the default clock skew tolerance for SAML message validation.
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan DefaultClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether LogoutResponse messages must be signed or delivered over a trusted
    /// binding (TLS or higher). Defaults to <c>true</c> per SAML 2.0 Profiles §4.4.4.
    /// Individual service providers can override this via
    /// <see cref="SamlServiceProvider.RequireSignedLogoutResponses"/>.
    /// </summary>
    public bool RequireSignedLogoutResponses { get; set; } = true;

    /// <summary>
    /// Gets or sets the default maximum age for SAML authentication requests.
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan DefaultRequestMaxAge { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the default lifetime for SAML assertions. This controls how long
    /// the generated assertion is valid (the window between NotBefore and NotOnOrAfter).
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan DefaultAssertionLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the lifetime of SAML signin state entries. This controls how long
    /// the signin state is retained while the user completes authentication.
    /// Defaults to 15 minutes.
    /// </summary>
    public TimeSpan SigninStateLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets the lifetime of SAML logout session entries. This controls how long
    /// the logout session tracking state is retained while front-channel logout iframes
    /// complete and SP responses are collected.
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan LogoutSessionLifetime { get; set; } = TimeSpan.FromMinutes(5);

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
    /// Maximum size of an inbound SAML message, in characters. For typical SAML
    /// content (ASCII XML, base64 payloads) this is approximately equal to bytes.
    /// Default: 1,048,576.
    /// </summary>
    public int MaxMessageSize { get; set; } = 1_048_576;

    /// <summary>
    /// Gets or sets the endpoint options for SAML.
    /// </summary>
    public SamlEndpointOptions Endpoints { get; set; } = new();

    /// <summary>
    /// Gets or sets the metadata generation options.
    /// </summary>
    public SamlMetadataOptions Metadata { get; } = new();

    /// <summary>
    /// Optional callback invoked when XML read errors occur while parsing an
    /// <see cref="Samlp.AuthnRequest"/>. The callback can inspect and remove errors from
    /// <see cref="ReadErrorInspectorContext{T}.Errors"/> to suppress exceptions. The callback
    /// also has access to the partially-parsed <c>context.Data</c> and the raw XML via
    /// <c>context.XmlSource</c>, allowing it to fix values (e.g., parse a quirky date format
    /// and populate the corresponding property directly).
    /// Consumers needing per-SP behavior can branch on <c>context.Data.Issuer</c>.
    /// </summary>
    /// <remarks>
    /// <c>context.Data.Issuer</c> is unvalidated at callback time — it has not yet been
    /// verified against the configured service provider store. Do not make security-sensitive
    /// decisions based solely on the issuer value without additional verification.
    /// </remarks>
    public Action<ReadErrorInspectorContext<Samlp.AuthnRequest>>? AuthnRequestErrorInspector { get; set; }

    /// <summary>
    /// Optional callback invoked when XML read errors occur while parsing a
    /// <see cref="Samlp.LogoutRequest"/>. The callback can inspect and remove errors from
    /// <see cref="ReadErrorInspectorContext{T}.Errors"/> to suppress exceptions. The callback
    /// also has access to the partially-parsed <c>context.Data</c> and the raw XML via
    /// <c>context.XmlSource</c>, allowing it to fix values (e.g., parse a quirky date format
    /// and populate the corresponding property directly).
    /// Consumers needing per-SP behavior can branch on <c>context.Data.Issuer</c>.
    /// </summary>
    /// <remarks>
    /// <c>context.Data.Issuer</c> is unvalidated at callback time — it has not yet been
    /// verified against the configured service provider store. Do not make security-sensitive
    /// decisions based solely on the issuer value without additional verification.
    /// </remarks>
    public Action<ReadErrorInspectorContext<Samlp.LogoutRequest>>? LogoutRequestErrorInspector { get; set; }

    /// <summary>
    /// Optional callback invoked when XML read errors occur while parsing a
    /// <see cref="Samlp.LogoutResponse"/>. The callback can inspect and remove errors from
    /// <see cref="ReadErrorInspectorContext{T}.Errors"/> to suppress exceptions. The callback
    /// also has access to the partially-parsed <c>context.Data</c> and the raw XML via
    /// <c>context.XmlSource</c>, allowing it to fix values (e.g., parse a quirky date format
    /// and populate the corresponding property directly).
    /// Consumers needing per-SP behavior can branch on <c>context.Data.Issuer</c>.
    /// </summary>
    /// <remarks>
    /// <c>context.Data.Issuer</c> is unvalidated at callback time — it has not yet been
    /// verified against the configured service provider store. Do not make security-sensitive
    /// decisions based solely on the issuer value without additional verification.
    /// </remarks>
    public Action<ReadErrorInspectorContext<Samlp.LogoutResponse>>? LogoutResponseErrorInspector { get; set; }
}

/// <summary>
/// Options for SAML endpoint paths and bindings.
/// </summary>
public sealed class SamlEndpointOptions
{
    /// <summary>
    /// Gets or sets the path for the SAML 2.0 Single Sign-On Service endpoint.
    /// Default: "/Saml2/SSO".
    /// </summary>
    public string SingleSignOnServicePath { get; set; } = SamlConstants.Defaults.SingleSignOnServicePath;

    /// <summary>
    /// Bindings supported for the Single Sign-On Service endpoint.
    /// Set to empty to disable the endpoint.
    /// Defaults to HTTP-Redirect and HTTP-POST.
    /// </summary>
    public ICollection<string> SingleSignOnServiceBindings { get; set; } =
    [
        SamlConstants.Bindings.HttpRedirect,
        SamlConstants.Bindings.HttpPost
    ];

    /// <summary>
    /// Gets or sets the path for the SAML 2.0 Single Sign-On callback endpoint.
    /// Default: "/Saml2/SSO/Callback".
    /// </summary>
    public string SingleSignOnCallbackPath { get; set; } = SamlConstants.Defaults.SingleSignOnCallbackPath;

    /// <summary>
    /// Bindings supported for SingleLogoutService. Set to empty to
    /// disable endpoint.
    /// </summary>
    public ICollection<string> SingleLogoutServiceBindings { get; set; } =
    [
        SamlConstants.Bindings.HttpRedirect,
        SamlConstants.Bindings.HttpPost
    ];

    /// <summary>
    /// Gets or sets the path for the SAML single logout endpoint.
    /// Default: "/Saml2/SLO".
    /// </summary>
    public string SingleLogoutServicePath { get; set; } = SamlConstants.Defaults.SingleLogoutServicePath;

    /// <summary>
    /// Gets or sets the path for the SAML single logout callback endpoint.
    /// Default: "/Saml2/SLO/Callback".
    /// </summary>
    public string SingleLogoutCallbackPath { get; set; } = SamlConstants.Defaults.SingleLogoutCallbackPath;

    /// <summary>
    /// Gets or sets the query string parameter name used to pass the SAML
    /// sign-in state identifier through the return URL.
    /// Default: "samlStateId".
    /// </summary>
    public string StateIdParameterName { get; set; } = "samlStateId";
}

/// <summary>
/// Options for SAML metadata generation.
/// </summary>
public sealed class SamlMetadataOptions
{
    /// <summary>
    /// Gets or sets the cache duration for generated metadata.
    /// Defaults to 12 hours.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(12);

    /// <summary>
    /// Gets or sets the absolute expiry duration for generated metadata.
    /// Defaults to 5 days.
    /// </summary>
    public TimeSpan ExpiryDuration { get; set; } = TimeSpan.FromDays(5);
}
