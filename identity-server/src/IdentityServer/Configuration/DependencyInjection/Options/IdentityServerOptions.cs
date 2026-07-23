// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores.Serialization;
using Duende.IdentityServer.Validation;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// The central configuration container for Duende IdentityServer. All fundamental settings are
/// grouped into sub-option objects accessible as properties of this class.
/// </summary>
/// <remarks>
/// Options are typically configured at startup via the <c>AddIdentityServer</c> overload that
/// accepts a configuration delegate:
/// <code>
/// builder.Services.AddIdentityServer(options =>
/// {
///     options.IssuerUri = "https://identity.example.com";
/// });
/// </code>
/// </remarks>
public class IdentityServerOptions
{
    /// <summary>
    /// Gets or sets the URI that identifies this IdentityServer instance. Used as the <c>issuer</c> claim in
    /// the discovery document, JWT access tokens, and introspection responses.
    /// </summary>
    /// <remarks>
    /// It is recommended to leave this unset. When not configured, the issuer is inferred from
    /// the URL of each incoming request, which better conforms to the OpenID Connect specification
    /// requirement that the issuer value be identical to the URL used to retrieve the discovery
    /// document. Setting a fixed issuer is useful when IdentityServer is accessed internally
    /// (e.g., inside a Kubernetes cluster) on a different address than the public-facing URL.
    /// When set, clients must be configured with the OpenID Connect metadata address explicitly
    /// to avoid using the authority-derived address.
    /// </remarks>
    public string? IssuerUri { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the inferred <see cref="IssuerUri"/> is normalized to lowercase.
    /// </summary>
    /// <remarks>
    /// When <c>true</c> (the default), the issuer URI derived from the request is converted to
    /// lowercase. Set to <c>false</c> to preserve the original casing of the request URL.
    /// </remarks>
    public bool LowerCaseIssuerUri { get; set; } = true;

    /// <summary>
    /// Gets or sets the value written to the <c>typ</c> header of JWT access tokens.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>at+jwt</c> as specified by
    /// <see href="https://datatracker.ietf.org/doc/html/rfc9068">RFC 9068</see>.
    /// Set to <c>null</c> or an empty string to omit the <c>typ</c> header entirely.
    /// </remarks>
    public string AccessTokenJwtType { get; set; } = "at+jwt";

    /// <summary>
    /// Gets or sets the value written to the <c>typ</c> header of back-channel logout tokens.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>logout+jwt</c> as specified by the
    /// <see href="https://openid.net/specs/openid-connect-backchannel-1_0.html#logouttoken">OpenID Connect Back-Channel Logout 1.0</see> specification.
    /// </remarks>
    public string LogoutTokenJwtType { get; set; } = "logout+jwt";

    /// <summary>
    /// Gets or sets a value indicating whether a static <c>aud</c> claim in all access tokens with the format <c>{issuer}/resources</c> is emitted.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. Enable this flag only when you need to produce access tokens
    /// that are backwards-compatible with older IdentityServer deployments that emitted a static
    /// audience. When <see cref="ApiResource"/>s are also configured, both the static audience
    /// and the API resource audiences will be present in the token.
    /// </remarks>
    public bool EmitStaticAudienceClaim { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether scope claims in JWTs and introspection responses are emitted as a
    /// space-delimited string rather than a JSON array.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c> for backwards compatibility. Setting this to <c>true</c> conforms
    /// to <see href="https://datatracker.ietf.org/doc/html/rfc9068">RFC 9068</see>, which
    /// specifies a space-delimited string for the <c>scope</c> claim in JWT access tokens.
    /// </remarks>
    public bool EmitScopesAsSpaceDelimitedStringInJwt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the <c>iss</c> response parameter on authorize endpoint responses is emitted.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>. Specified by
    /// <see href="https://datatracker.ietf.org/doc/rfc9207/">RFC 9207</see>, which defines the
    /// <c>iss</c> parameter to help clients identify the authorization server that issued the
    /// response and protect against mix-up attacks.
    /// </remarks>
    public bool EmitIssuerIdentificationResponseParameter { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the <c>s_hash</c> claim in identity tokens is emitted.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. The <c>s_hash</c> claim is a hash of the <c>state</c> parameter,
    /// defined by the
    /// <see href="https://openid.net/specs/openid-financial-api-part-2-1_0.html">OpenID Financial-grade API Security Profile</see>.
    /// Enable this when targeting FAPI-compliant clients.
    /// </remarks>
    public bool EmitStateHash { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether strict validation of JWT-secured authorization requests (JAR) per
    /// <see href="https://datatracker.ietf.org/doc/rfc9101/">RFC 9101</see> is enforced.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. When enabled, JAR request JWTs must carry a <c>typ</c> header
    /// value of <c>oauth-authz-req+jwt</c>, and the HTTP request must include a
    /// <c>Content-Type</c> of <c>application/oauth-authz-req+jwt</c>. Enabling this may break
    /// older OIDC-conformant request objects that do not set these headers.
    /// </remarks>
    public bool StrictJarValidation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user's <c>tenant</c> claim is compared against the <c>tenant</c> value in
    /// <c>acr_values</c> to decide whether to show the login page.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. When enabled, if the authenticated user's <c>tenant</c> claim
    /// does not match the requested tenant in <c>acr_values</c>, the user is redirected to the
    /// login page.
    /// </remarks>
    public bool ValidateTenantOnAuthorization { get; set; }

    /// <summary>
    /// Gets or sets the configuration for which protocol endpoints are enabled or disabled.
    /// </summary>
    public EndpointsOptions Endpoints { get; set; } = new EndpointsOptions();

    /// <summary>
    /// Gets or sets the configuration for the OpenID Connect discovery document endpoint.
    /// </summary>
    public DiscoveryOptions Discovery { get; set; } = new DiscoveryOptions();

    /// <summary>
    /// Gets or sets the configuration for login, logout, and cookie behavior for interactive users.
    /// </summary>
    public AuthenticationOptions Authentication { get; set; } = new AuthenticationOptions();

    /// <summary>
    /// Gets or sets the configuration for which diagnostic events are raised to the registered event sink.
    /// </summary>
    public EventsOptions Events { get; set; } = new EventsOptions();

    /// <summary>
    /// Gets or sets the maximum allowed lengths for protocol request parameters such as client ID, scope, and redirect URI.
    /// </summary>
    public InputLengthRestrictions InputLengthRestrictions { get; set; } = new InputLengthRestrictions();

    /// <summary>
    /// Gets or sets the configuration for user-facing UI pages, including URLs and query parameter names.
    /// </summary>
    public UserInteractionOptions UserInteraction { get; set; } = new UserInteractionOptions();

    /// <summary>
    /// Gets or sets the cache durations for client, resource, CORS, and identity provider store lookups.
    /// </summary>
    /// <remarks>
    /// These settings only take effect when the respective caching has been enabled during
    /// service registration (e.g., <c>AddClientStoreCache</c>).
    /// </remarks>
    public CachingOptions Caching { get; set; } = new CachingOptions();

    /// <summary>
    /// Gets or sets the CORS policy settings for IdentityServer's protocol endpoints.
    /// </summary>
    public CorsOptions Cors { get; set; } = new CorsOptions();

    /// <summary>
    /// Gets or sets the Content Security Policy (CSP) header settings for IdentityServer's UI pages.
    /// </summary>
    public CspOptions Csp { get; set; } = new CspOptions();

    /// <summary>
    /// Gets or sets the settings that control redirect URI validation behavior.
    /// </summary>
    public ValidationOptions Validation { get; set; } = new ValidationOptions();

    /// <summary>
    /// Gets or sets the OAuth 2.0 Device Authorization Grant (device flow) settings.
    /// </summary>
    public DeviceFlowOptions DeviceFlow { get; set; } = new DeviceFlowOptions();

    /// <summary>
    /// Gets or sets the Client-Initiated Backchannel Authentication (CIBA) settings.
    /// </summary>
    public CibaOptions Ciba { get; set; } = new CibaOptions();

    /// <summary>
    /// Gets or sets the settings for filtering sensitive values from logs and suppressing noisy exceptions.
    /// </summary>
    public LoggingOptions Logging { get; set; } = new LoggingOptions();

    /// <summary>
    /// Gets or sets the Mutual TLS (mTLS) settings for certificate-bound tokens and client authentication.
    /// </summary>
    public MutualTlsOptions MutualTls { get; set; } = new MutualTlsOptions();

    /// <summary>
    /// Gets or sets the automatic signing key management settings, including rotation intervals and storage options.
    /// </summary>
    public KeyManagementOptions KeyManagement { get; set; } = new KeyManagementOptions();

    /// <summary>
    /// Gets or sets the settings for persisted grants, including data protection and one-time refresh token behavior.
    /// </summary>
    public PersistentGrantOptions PersistentGrants { get; set; } = new PersistentGrantOptions();

    /// <summary>
    /// Gets or sets the Demonstration of Proof-of-Possession (DPoP) settings for sender-constrained tokens.
    /// </summary>
    public DPoPOptions DPoP { get; set; } = new DPoPOptions();

    /// <summary>
    /// Gets or sets the Duende IdentityServer license key. When not set, IdentityServer runs in trial/starter mode.
    /// </summary>
    public string? LicenseKey { get; set; }

    /// <summary>
    /// Gets or sets the settings for the dynamic external identity provider feature.
    /// </summary>
    public DynamicProviderOptions DynamicProviders { get; set; } = new DynamicProviderOptions();

    /// <summary>
    /// Gets or sets the settings for server-side session storage and periodic cleanup of expired sessions.
    /// </summary>
    public ServerSideSessionOptions ServerSideSessions { get; set; } = new ServerSideSessionOptions();

    /// <summary>
    /// Gets or sets the settings for the background job that periodically purges expired
    /// entities from the storage layer.
    /// </summary>
    public StoragePurgeOptions StoragePurge { get; set; } = new StoragePurgeOptions();

    /// <summary>
    /// Gets or sets the Pushed Authorization Request (PAR) settings, including whether PAR is globally required.
    /// </summary>
    public PushedAuthorizationOptions PushedAuthorization { get; set; } = new PushedAuthorizationOptions();

    /// <summary>
    /// Gets or sets the allowed clock skew applied when validating JWT lifetimes throughout IdentityServer.
    /// </summary>
    /// <remarks>
    /// Defaults to five minutes. This setting applies to JWT access tokens validated at the
    /// UserInfo, introspection, and local API endpoints; private_key_jwt client authentication
    /// assertions; JAR request objects; and custom uses of <see cref="TokenValidator"/>.
    /// It does not apply to DPoP proof tokens, which use <see cref="DPoPOptions.ServerClockSkew"/>.
    /// </remarks>
    public TimeSpan JwtValidationClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets a value indicating whether strict audience validation is enforced for
    /// <c>private_key_jwt</c> client assertions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <see langword="true"/>, the audience (<c>aud</c>) claim must be the issuer identifier
    /// as the sole value (per draft-ietf-oauth-rfc7523bis), and the <c>typ</c> header must be
    /// <c>client-authentication+jwt</c>.
    /// </para>
    /// <para>
    /// When <see langword="false"/>, legacy audience values (token endpoint URL, CIBA endpoint, PAR
    /// endpoint) are accepted, and the <c>typ</c> header is not required. However, if a token
    /// includes <c>typ: client-authentication+jwt</c>, strict validation is still applied for that
    /// token regardless of this setting.
    /// </para>
    /// <para>
    /// Defaults to <see langword="false"/>. Set to <see langword="true"/> to enforce strict
    /// validation per draft-ietf-oauth-rfc7523bis.
    /// </para>
    /// </remarks>
    public bool StrictClientAssertionAudienceValidation { get; set; }

    /// <summary>
    /// <para>
    /// Gets or sets the allowed signature algorithms for JWT secured authorization requests (JAR). The "alg" header of JAR
    /// request objects is validated against this collection, and the
    /// request_object_signing_alg_values_supported discovery property is populated with these values.
    /// </para>
    /// <para>
    /// Defaults to [RS256, RS384, RS512, PS256, PS384, PS512, ES256, ES384, ES512], which allows
    /// the RSA, Probabilistic RSA, or ECDSA signing algorithms with 256, 384, or 512-bit SHA hashing.
    /// </para>
    /// <para>
    /// If set to an empty collection, all algorithms are allowed, but the request_object_signing_alg_values_supported
    /// will not be set. Explicitly listing the expected values is recommended.
    ///</para>
    /// </summary>
    public ICollection<string> SupportedRequestObjectSigningAlgorithms { get; set; } =
    [
        SecurityAlgorithms.RsaSha256,
        SecurityAlgorithms.RsaSha384,
        SecurityAlgorithms.RsaSha512,

        SecurityAlgorithms.RsaSsaPssSha256,
        SecurityAlgorithms.RsaSsaPssSha384,
        SecurityAlgorithms.RsaSsaPssSha512,

        SecurityAlgorithms.EcdsaSha256,
        SecurityAlgorithms.EcdsaSha384,
        SecurityAlgorithms.EcdsaSha512,
    ];

    /// <summary>
    /// <para>
    /// Gets or sets the allowed signature algorithms for client authentication using client assertions (the
    /// private_key_jwt parameter). The "alg" header of client assertions is validated against this collection, and the
    /// token_endpoint_auth_signing_alg_values_supported discovery property is populated with these values.
    /// </para>
    /// <para>
    /// Defaults to [RS256, RS384, RS512, PS256, PS384, PS512, ES256, ES384, ES512], which allows
    /// the RSA, Probabilistic RSA, or ECDSA signing algorithms with 256, 384, or 512-bit SHA hashing.
    /// </para>
    /// <para>
    /// If set to an empty collection, all algorithms are allowed, but the
    /// token_endpoint_auth_signing_alg_values_supported will not be set. Explicitly listing the expected values is
    /// recommended.
    ///</para>
    /// </summary>
    public ICollection<string> SupportedClientAssertionSigningAlgorithms { get; set; } = [
        SecurityAlgorithms.RsaSha256,
        SecurityAlgorithms.RsaSha384,
        SecurityAlgorithms.RsaSha512,

        SecurityAlgorithms.RsaSsaPssSha256,
        SecurityAlgorithms.RsaSsaPssSha384,
        SecurityAlgorithms.RsaSsaPssSha512,

        SecurityAlgorithms.EcdsaSha256,
        SecurityAlgorithms.EcdsaSha384,
        SecurityAlgorithms.EcdsaSha512,
    ];

    /// <summary>
    /// Gets or sets the options that control the diagnostic data that is logged by IdentityServer.
    /// </summary>
    public DiagnosticOptions Diagnostics { get; set; } = new DiagnosticOptions();

    /// <summary>
    /// Gets or sets the SAML 2.0 Identity Provider options.
    /// </summary>
    public SamlOptions Saml { get; set; } = new SamlOptions();
}
