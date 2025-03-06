// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Json.Serialization;
using Duende.IdentityModel;

namespace Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;

/// <summary>
/// Represents a dynamic client registration request. The parameters that are
/// supported include a subset of the parameters defined by IANA
/// (https://www.iana.org/assignments/oauth-parameters/oauth-parameters.xhtml#client-metadata),
/// and custom properties needed by IdentityServer.
/// </summary>
public class DynamicClientRegistrationRequest
{
    /// <summary>
    /// List of redirection URI strings for use in redirect-based flows such as the authorization code flows.
    /// </summary>
    /// <remarks>
    /// Clients using flows with redirection must register their redirection URI values.
    /// </remarks>
    [JsonPropertyName(OidcConstants.ClientMetadata.RedirectUris)]
    public ICollection<Uri>? RedirectUris { get; set; }

    /// <summary>
    /// List of OAuth 2.0 grant type strings that the client can use at the token endpoint.
    /// </summary>
    /// <remarks>
    /// Valid values are "authorization_code", "client_credentials", "refresh_token".
    /// </remarks>
    [JsonPropertyName(OidcConstants.ClientMetadata.GrantTypes)]
    public ICollection<string> GrantTypes { get; set; } = new HashSet<string>();

    /// <summary>
    /// Human-readable string name of the client to be presented to the end-user during authorization.
    /// </summary>
    [JsonPropertyName(OidcConstants.ClientMetadata.ClientName)]
    public string? ClientName { get; set; }

    /// <summary>
    /// Logo for the client.
    /// </summary>
    /// <remarks>
    /// If present, the server should display this image to the end-user during approval.
    /// </remarks>
    [JsonPropertyName(OidcConstants.ClientMetadata.LogoUri)]
    public Uri? LogoUri { get; set; }

    /// <summary>
    /// Web page providing information about the client.
    /// </summary>
    [JsonPropertyName(OidcConstants.ClientMetadata.ClientUri)]
    public Uri? ClientUri { get; set; }

    /// <summary>
    /// URL to a JWK Set document which contains the client's public keys.
    /// </summary>
    /// <remarks>
    /// <remark>
    /// The default configuration endpoints do not use the jwks uri.
    /// It is included in this model to facilitate extensions to the
    /// configuration system.
    /// </remark>
    /// <remark>
    /// The <see cref="JwksUri"/> and <see cref="Jwks"/> parameters MUST NOT both be present in
    /// the same request or response.
    /// </remark>
    /// </remarks>
    [JsonPropertyName(OidcConstants.ClientMetadata.JwksUri)]
    public Uri? JwksUri { get; set; }

    /// <summary>
    /// JWK Set document which contains the client's public keys.
    /// </summary>
    /// <remark>
    /// The <see cref="JwksUri"/> and <see cref="Jwks"/> parameters MUST NOT both be present in
    /// the same request or response.
    /// </remark>
    [JsonPropertyName(OidcConstants.ClientMetadata.Jwks)]
    public KeySet? Jwks { get; set; }

    /// <summary>
    /// String containing a space-separated list of scope values that the client
    /// can use when requesting access tokens.
    /// </summary>
    /// <remarks>
    /// If omitted, the configuration API will register a client with the scopes
    /// set by the <see
    /// cref="Validation.DynamicClientRegistration.DynamicClientRegistrationValidator.SetDefaultScopes"
    /// /> method, which defaults to no scopes
    /// </remarks>
    [JsonPropertyName(OidcConstants.ClientMetadata.Scope)]
    public string? Scope { get; set; }

    /// <summary>
    /// List of post-logout redirection URIs for use in the end session
    /// endpoint.
    /// </summary>
    [JsonPropertyName(OidcConstants.ClientMetadata.PostLogoutRedirectUris)]
    public ICollection<Uri>? PostLogoutRedirectUris { get; set; }

    /// <summary>
    /// RP URL that will cause the RP to log itself out when rendered in an
    /// iframe by the OP.
    /// </summary>
    [JsonPropertyName(OidcConstants.ClientMetadata.FrontChannelLogoutUri)]
    public Uri? FrontChannelLogoutUri { get; set; }

    /// <summary>
    /// Boolean value specifying whether the RP requires that a sid (session ID)
    /// query parameter be included to identify the RP session with the OP when
    /// the frontchannel_logout_uri is used.
    /// </summary>
    [JsonPropertyName(OidcConstants.ClientMetadata.FrontChannelLogoutSessionRequired)]
    public bool? FrontChannelLogoutSessionRequired { get; set; }

    /// <summary>
    /// RP URL that will cause the RP to log itself out when sent a Logout Token
    /// by the OP.
    /// </summary>
    [JsonPropertyName(OidcConstants.ClientMetadata.BackchannelLogoutUri)]
    public Uri? BackChannelLogoutUri { get; set; }

    /// <summary>
    /// Boolean value specifying whether the RP requires that a sid (session ID)
    /// Claim be included in the Logout Token to identify the RP session with
    /// the OP when the backchannel_logout_uri is used.
    /// </summary>
    [JsonPropertyName(OidcConstants.ClientMetadata.BackchannelLogoutSessionRequired)]
    public bool? BackChannelLogoutSessionRequired { get; set; }

    /// <summary>
    /// A software statement containing client metadata values about the client
    /// software as claims.  This is a string value containing the entire signed
    /// JWT.
    /// </summary>
    /// <remark>
    /// The default configuration endpoints do not use the software statement.
    /// It is included in this model to facilitate extensions to the
    /// configuration system.
    /// </remark>
    [JsonPropertyName(OidcConstants.ClientMetadata.SoftwareStatement)]
    public string? SoftwareStatement { get; set; }

    /// <summary>
    /// A unique identifier string (e.g., a Universally Unique Identifier
    /// (UUID)) assigned by the client developer or software publisher used by
    /// registration endpoints to identify the client software to be dynamically
    /// registered.  Unlike "client_id", which is issued by the authorization
    /// server and SHOULD vary between instances, the "software_id" SHOULD
    /// remain the same for all instances of the client software.  The
    /// "software_id" SHOULD remain the same across multiple updates or versions
    /// of the same piece of software.  The value of this field is not intended
    /// to be human readable and is usually opaque to the client and
    /// authorization server.
    /// </summary>
    /// <remark>
    /// The default configuration endpoints do not use the software id.
    /// It is included in this model to facilitate extensions to the
    /// configuration system.
    /// </remark>
    [JsonPropertyName(OidcConstants.ClientMetadata.SoftwareId)]
    public string? SoftwareId { get; set; }

    /// <summary>
    /// A version identifier string for the client software identified by
    /// "software_id".  The value of the "software_version" SHOULD change on any
    /// update to the client software identified by the same "software_id".  The
    /// value of this field is intended to be compared using string equality
    /// matching and no other comparison semantics are defined by this
    /// specification.  The value of this field is outside the scope of this
    /// specification, but it is not intended to be human readable and is
    /// usually opaque to the client and authorization server.  The definition
    /// of what constitutes an update to client software that would trigger a
    /// change to this value is specific to the software itself and is outside
    /// the scope of this specification.
    /// </summary>
    /// <remark> The default configuration endpoints do not use the software
    /// version. It is included in this model to facilitate extensions to the
    /// configuration system. </remark>
    [JsonPropertyName(OidcConstants.ClientMetadata.SoftwareVersion)]
    public string? SoftwareVersion { get; set; }

    /// <summary>
    /// Boolean value specifying whether authorization requests must be
    /// protected as signed request objects and provided through either the
    /// request or request_uri parameters.
    /// </summary>
    [JsonPropertyName(OidcConstants.ClientMetadata.RequireSignedRequestObject)]
    public bool? RequireSignedRequestObject { get; set; }

    /// <summary>
    /// Requested Client Authentication method for the Token Endpoint. The
    /// supported options are client_secret_post, client_secret_basic,
    /// client_secret_jwt, private_key_jwt.
    /// </summary>
    [JsonPropertyName(OidcConstants.ClientMetadata.TokenEndpointAuthenticationMethod)]
    public string? TokenEndpointAuthenticationMethod { get; set; }

    /// <summary>
    /// Default maximum authentication age. 
    /// </summary>
    /// <remarks>
    /// This is stored as the UserSsoLifetime property of the client.
    /// </remarks>
    [JsonPropertyName(OidcConstants.ClientMetadata.DefaultMaxAge)]
    public int? DefaultMaxAge { get; set; }

    /// <summary>
    /// URI using the https scheme that a third party can use to initiate a
    /// login by the relying party.
    /// </summary>
    /// <remarks>
    /// The URI must accept requests via both GET and POST. The client must
    /// understand the <c>login_hint</c> and iss parameters and should support
    /// the <c>target_link_uri</c> parameter.
    /// </remarks>
    [JsonPropertyName(OidcConstants.ClientMetadata.InitiateLoginUri)]
    public Uri? InitiateLoginUri { get; set; }

    /// <summary>
    /// The lifetime of identity tokens, in seconds.
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("identity_token_lifetime")]
    public int? IdentityTokenLifetime { get; set; }

    /// <summary>
    /// The lifetime of access tokens, in seconds.
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("access_token_lifetime")]
    public int? AccessTokenLifetime { get; set; }

    /// <summary>
    /// The lifetime of authorization codes, in seconds.
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("authorization_code_lifetime")]
    public int? AuthorizationCodeLifetime { get; set; }

    /// <summary>
    /// The absolute lifetime of refresh tokens, in seconds.
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("absolute_refresh_token_lifetime")]
    public int? AbsoluteRefreshTokenLifetime { get; set; }

    /// <summary>
    /// The sliding lifetime of refresh tokens, in seconds.
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("sliding_refresh_token_lifetime")]
    public int? SlidingRefreshTokenLifetime { get; set; }

    /// <summary>
    /// The type of expiration for refresh tokens. Either "sliding" or
    /// "absolute".
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("refresh_token_expiration")]
    public string? RefreshTokenExpiration { get; set; }

    /// <summary>
    /// The usage type for refresh tokens. Either "OneTimeOnly" or "ReUse".
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("refresh_token_usage")]
    public string? RefreshTokenUsage { get; set; }

    /// <summary>
    /// Boolean value specifying whether access token claims are updated during 
    /// token refresh.
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("update_access_token_claims_on_refresh")]
    public bool? UpdateAccessTokenClaimsOnRefresh { get; set; }

    /// <summary>
    /// Boolean value specifying whether consent is required in user-centric
    /// flows initiated by this client.
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("require_consent")]
    public bool? RequireConsent { get; set; }

    /// <summary>
    /// Boolean value specifying whether a user's consent can be remembered in
    /// flows initiated by this client.
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("allow_remember_consent")]
    public bool? AllowRememberConsent { get; set; }

    /// <summary>
    /// The lifetime of consent, in seconds.
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("consent_lifetime")]
    public int? ConsentLifetime { get; set; }

    /// <summary>
    /// The type of access tokens that this client will create. Either "Jwt" or
    /// "Reference".
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("access_token_type")]
    public string? AccessTokenType { get; set; }

    /// <summary>
    /// List of allowed CORS origins for JavaScript clients.
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("allowed_cors_origins")]
    public HashSet<string>? AllowedCorsOrigins { get; set; }

    /// <summary>
    /// Boolean value specifying if a client secret is needed to request tokens
    /// at the token endpoint.
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("require_client_secret")]
    public bool? RequireClientSecret { get; set; }

    /// <summary>
    /// Boolean value specifying if local logins are enabled when this client
    /// uses interactive flows.
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("enable_local_login")]
    public bool? EnableLocalLogin { get; set; }

    /// <summary>
    /// List of external IdPs that can be used with this client. If list is
    /// empty all IdPs are allowed. Defaults to empty.
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("identity_provider_restrictions")]
    public HashSet<string>? IdentityProviderRestrictions { get; set; }

    /// <summary>
    /// When enabled, the client's token lifetimes (e.g. refresh tokens) will be
    /// tied to the user's session lifetime. This means when the user logs out,
    /// any revokable tokens will be removed. If using server-side sessions,
    /// expired sessions will also remove any revokable tokens, and backchannel
    /// logout will be triggered. This client's setting overrides the global
    /// CoordinateTokensWithUserSession configuration setting.
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("coordinate_lifetime_with_user_session")]
    public bool? CoordinateLifetimeWithUserSession { get; set; }

    /// <summary>
    /// List of signing algorithms to use when signing identity tokens. If not set, will use the server default signing algorithm.
    /// This property is an extension to the Dynamic Client Registration Protocol.
    /// </summary>
    [JsonPropertyName("allowed_identity_token_signing_algorithms")]
    public ICollection<string>? AllowedIdentityTokenSigningAlgorithms { get; set; }

    /// <summary>
    /// Custom client metadata fields to include in the serialization.
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, object> Extensions { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
}
