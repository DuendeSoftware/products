// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections;
using System.Diagnostics;
using Duende.IdentityModel;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models an OpenID Connect or OAuth2 client
/// </summary>
[DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
public class Client : IConnectedApplication
{
    // setting grant types should be atomic
    private ICollection<string> _allowedGrantTypes = new GrantTypeValidatingHashSet();

    private string DebuggerDisplay => ClientId ?? $"{{{typeof(Client)}}}";

    /// <summary>
    /// Gets or sets a value indicating whether the client is enabled (defaults to <c>true</c>).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the unique ID of the client.
    /// </summary>
    public string ClientId { get; set; } = default!;

    /// <summary>
    /// Gets or sets the protocol type. Defaults to <c>oidc</c> (OpenID Connect).
    /// </summary>
    /// <value>
    /// The protocol type.
    /// </value>
    public string ProtocolType { get; set; } = IdentityServerConstants.ProtocolTypes.OpenIdConnect;

    /// <summary>
    /// Gets or sets the collection of client secrets — credentials used to authenticate the client at the token endpoint.
    /// Only relevant for flows that require a secret (e.g. authorization code, client credentials).
    /// </summary>
    public ICollection<Secret> ClientSecrets { get; set; } = new HashSet<Secret>();

    /// <summary>
    /// Gets or sets a value indicating whether a client secret is required to request tokens at the token endpoint (defaults to <c>true</c>).
    /// </summary>
    public bool RequireClientSecret { get; set; } = true;

    /// <summary>
    /// Gets or sets the client display name (used for logging and consent screen).
    /// </summary>
    public string? ClientName { get; set; }

    /// <summary>
    /// Gets or sets the description of the client.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the URI to further information about client (used on consent screen).
    /// </summary>
    public string? ClientUri { get; set; }

    /// <summary>
    /// Gets or sets the URI to client logo (used on consent screen).
    /// </summary>
    public string? LogoUri { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a consent screen is required (defaults to <c>false</c>).
    /// </summary>
    public bool RequireConsent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user can choose to store consent decisions (defaults to <c>true</c>).
    /// </summary>
    public bool AllowRememberConsent { get; set; } = true;

    /// <summary>
    /// Gets or sets the collection of allowed grant types the client may use (e.g. authorization_code, client_credentials).
    /// Use the <see cref="OidcConstants.GrantTypes"/> class for common combinations.
    /// Invalid combinations (e.g. mixing implicit with authorization_code) are rejected.
    /// </summary>
    public ICollection<string> AllowedGrantTypes
    {
        get => _allowedGrantTypes;
        set
        {
            ValidateGrantTypes(value);
            _allowedGrantTypes = new GrantTypeValidatingHashSet(value);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether a proof key is required for authorization code based token requests (defaults to <c>true</c>).
    /// </summary>
    public bool RequirePkce { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a proof key can be sent using plain method (not recommended and defaults to <c>false</c>).
    /// </summary>
    public bool AllowPlainTextPkce { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the client must use a request object on authorize requests (defaults to <c>false</c>).
    /// </summary>
    public bool RequireRequestObject { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether access tokens are transmitted via the browser for this client (defaults to <c>false</c>).
    /// This can prevent accidental leakage of access tokens when multiple response types are allowed.
    /// </summary>
    /// <value>
    /// <c>true</c> if access tokens can be transmitted via the browser; otherwise, <c>false</c>.
    /// </value>
    public bool AllowAccessTokensViaBrowser { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a DPoP (Demonstrating Proof-of-Possession) token is required to be used by this client (defaults to <c>false</c>).
    /// </summary>
    public bool RequireDPoP { get; set; }

    /// <summary>
    /// Gets or sets the validation mode for the DPoP proof token expiration.
    /// This supports both the client generated 'iat' value and/or the server generated 'nonce' value.
    /// Defaults to only using the 'iat' value.
    /// </summary>
    public DPoPTokenExpirationValidationMode DPoPValidationMode { get; set; } = DPoPTokenExpirationValidationMode.Iat;

    /// <summary>
    /// Gets or sets the clock skew used in validating the client's DPoP proof token 'iat' claim value. Defaults to 5 minutes.
    /// </summary>
    public TimeSpan DPoPClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the collection of allowed URIs to return tokens or authorization codes to.
    /// </summary>
    public ICollection<string> RedirectUris { get; set; } = new HashSet<string>();

    /// <summary>
    /// Gets or sets the collection of allowed URIs to redirect to after logout.
    /// </summary>
    public ICollection<string> PostLogoutRedirectUris { get; set; } = new HashSet<string>();

    /// <summary>
    /// Gets or sets the logout URI at client for HTTP front-channel based logout.
    /// </summary>
    public string? FrontChannelLogoutUri { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user's session id should be sent to the FrontChannelLogoutUri. Defaults to <c>true</c>.
    /// </summary>
    public bool FrontChannelLogoutSessionRequired { get; set; } = true;

    /// <summary>
    /// Gets or sets the logout URI at client for HTTP back-channel based logout.
    /// </summary>
    public string? BackChannelLogoutUri { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user's session id should be sent to the BackChannelLogoutUri. Defaults to <c>true</c>.
    /// </summary>
    public bool BackChannelLogoutSessionRequired { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether this client can request refresh tokens by requesting the <c>offline_access</c> scope. Defaults to <c>false</c>.
    /// </summary>
    public bool AllowOfflineAccess { get; set; }

    /// <summary>
    /// Gets or sets the collection of API scopes that the client is allowed to request. If empty, the client cannot access any scope.
    /// By default, a client has no access to any resources — add the corresponding scope names to grant access.
    /// </summary>
    public ICollection<string> AllowedScopes { get; set; } = new HashSet<string>();

    /// <summary>
    /// Gets or sets a value indicating whether user claims should always be added to the id token instead of requiring the client to use the userinfo endpoint
    /// when requesting both an id token and access token. Defaults to <c>false</c>.
    /// </summary>
    public bool AlwaysIncludeUserClaimsInIdToken { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of identity token in seconds (defaults to 300 seconds / 5 minutes).
    /// </summary>
    public int IdentityTokenLifetime { get; set; } = 300;

    /// <summary>
    /// Gets or sets the collection of allowed signing algorithms for the identity token. If empty, the server default signing algorithm is used.
    /// </summary>
    public ICollection<string> AllowedIdentityTokenSigningAlgorithms { get; set; } = new HashSet<string>();

    /// <summary>
    /// Gets or sets the lifetime of access token in seconds (defaults to 3600 seconds / 1 hour).
    /// </summary>
    public int AccessTokenLifetime { get; set; } = 3600;

    /// <summary>
    /// Gets or sets the lifetime of authorization code in seconds (defaults to 300 seconds / 5 minutes).
    /// </summary>
    public int AuthorizationCodeLifetime { get; set; } = 300;

    /// <summary>
    /// Gets or sets the maximum lifetime of a refresh token in seconds. Defaults to 2592000 seconds / 30 days.
    /// Setting this to 0 when <see cref="RefreshTokenExpiration"/> is <see cref="TokenExpiration.Sliding"/>
    /// means refresh tokens only expire after <see cref="SlidingRefreshTokenLifetime"/> has passed.
    /// </summary>
    public int AbsoluteRefreshTokenLifetime { get; set; } = 2592000;

    /// <summary>
    /// Gets or sets the sliding lifetime of a refresh token in seconds. Defaults to 1296000 seconds / 15 days.
    /// </summary>
    public int SlidingRefreshTokenLifetime { get; set; } = 1296000;

    /// <summary>
    /// Gets or sets the lifetime of a user consent in seconds. Defaults to null (no expiration).
    /// </summary>
    public int? ConsentLifetime { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of pushed authorization requests for this client. If this lifetime is set, it takes precedence over
    /// the global configuration in PushedAuthorizationOptions. Defaults to null, which means the global
    /// configuration will be used.
    /// </summary>
    public int? PushedAuthorizationLifetime { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether pushed authorization requests are required for this client. There is also a global
    /// configuration flag to require pushed authorization in <c>PushedAuthorizationOptions</c>. Pushed authorization is
    /// required for a client if either the global configuration flag is enabled or if this flag is set for that client.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool RequirePushedAuthorization { get; set; }

    /// <summary>
    /// Gets or sets the token usage behavior, controlling whether tokens are rotated when they are used. Defaults to
    /// reusable tokens.
    /// <para>
    /// ReUse: the refresh token handle will stay the same when refreshing
    /// tokens
    /// </para>
    /// <para>
    /// OneTime: the refresh token handle will be updated when refreshing tokens
    /// </para>
    /// </summary>
    public TokenUsage RefreshTokenUsage { get; set; } = TokenUsage.ReUse;

    /// <summary>
    /// Gets or sets a value indicating whether the access token (and its claims) should be updated on a refresh token request.
    /// Defaults to <c>false</c>.
    /// </summary>
    /// <value>
    /// <c>true</c> if the token should be updated; otherwise, <c>false</c>.
    /// </value>
    public bool UpdateAccessTokenClaimsOnRefresh { get; set; }

    /// <summary>
    /// Gets or sets the expiration behavior of refresh tokens.
    /// <para>
    /// <see cref="TokenExpiration.Absolute"/>: the refresh token expires at a fixed point in time
    /// (specified by <see cref="AbsoluteRefreshTokenLifetime"/>). This is the default.
    /// </para>
    /// <para>
    /// <see cref="TokenExpiration.Sliding"/>: when refreshing the token, the lifetime of the refresh token is renewed
    /// (by the amount specified in <see cref="SlidingRefreshTokenLifetime"/>). The lifetime will not exceed
    /// <see cref="AbsoluteRefreshTokenLifetime"/>.
    /// </para>
    /// </summary>
    public TokenExpiration RefreshTokenExpiration { get; set; } = TokenExpiration.Absolute;

    /// <summary>
    /// Gets or sets a value indicating whether the access token is a reference token or a self contained JWT token (defaults to Jwt).
    /// </summary>
    public AccessTokenType AccessTokenType { get; set; } = AccessTokenType.Jwt;

    /// <summary>
    /// Gets or sets a value indicating whether the local login is allowed for this client. Defaults to <c>true</c>.
    /// </summary>
    /// <value>
    ///   <c>true</c> if local logins are enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableLocalLogin { get; set; } = true;

    /// <summary>
    /// Gets or sets the collection of external IdPs that can be used with this client (if list is empty all IdPs are allowed). Defaults to empty.
    /// </summary>
    public ICollection<string> IdentityProviderRestrictions { get; set; } = new HashSet<string>();

    /// <summary>
    /// Gets or sets a value indicating whether JWT access tokens should include an embedded unique ID
    /// via the <c>jti</c> claim. Defaults to <c>true</c>.
    /// </summary>
    /// <value>
    /// <c>true</c> to add an id; otherwise, <c>false</c>.
    /// </value>
    public bool IncludeJwtId { get; set; } = true;

    /// <summary>
    /// Gets or sets the collection of claims for the client (will be included in the access token).
    /// </summary>
    /// <value>
    /// The claims.
    /// </value>
    public ICollection<ClientClaim> Claims { get; set; } = new HashSet<ClientClaim>();

    /// <summary>
    /// Gets or sets a value indicating whether client claims should be always included in the access tokens - or only for client credentials flow.
    /// Defaults to <c>false</c>
    /// </summary>
    /// <value>
    /// <c>true</c> if claims should always be sent; otherwise, <c>false</c>.
    /// </value>
    public bool AlwaysSendClientClaims { get; set; }

    /// <summary>
    /// Gets or sets a prefix for client claim types to avoid accidental collisions with user claims.
    /// Defaults to <c>client_</c>.
    /// </summary>
    /// <value>
    /// Any non empty string if claims should be prefixed with the value; otherwise, <c>null</c>.
    /// </value>
    public string? ClientClaimsPrefix { get; set; } = "client_";

    /// <summary>
    /// Gets or sets a salt value used in pair-wise subjectId generation for users of this client.
    /// </summary>
    public string? PairWiseSubjectSalt { get; set; }

    /// <summary>
    /// Gets or sets the maximum duration (in seconds) since the last time the user authenticated.
    /// Defaults to <c>null</c> (no restriction). Use this to control when users are required to re-enter
    /// credentials instead of being silently authenticated via an existing session.
    /// </summary>
    public int? UserSsoLifetime { get; set; }

    /// <summary>
    /// Gets or sets the type of the device flow user code. If not set, falls back to the server default.
    /// </summary>
    /// <value>
    /// The type of the device flow user code.
    /// </value>
    public string? UserCodeType { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of device codes in seconds (defaults to 300 seconds / 5 minutes).
    /// </summary>
    /// <value>
    /// The device code lifetime.
    /// </value>
    public int DeviceCodeLifetime { get; set; } = 300;

    /// <summary>
    /// Gets or sets the backchannel authentication request lifetime in seconds for CIBA flows.
    /// Defaults to <c>null</c>, which means the server default is used.
    /// </summary>
    public int? CibaLifetime { get; set; }

    /// <summary>
    /// Gets or sets the maximum polling interval for this client in the CIBA
    /// and Device Code flows. If this client polls more frequently than the
    /// polling interval during those flows, it will receive a <c>slow_down</c> error
    /// response. Defaults to <c>null</c>, which uses the global default
    /// (<c>IdentityServerOptions.Ciba.DefaultPollingInterval</c> or <c>IdentityServerOptions.DeviceFlow.Interval</c>).
    /// </summary>
    public int? PollingInterval { get; set; }


    /// <summary>
    /// Gets or sets a value indicating whether the client's token lifetimes (e.g. refresh tokens) will be tied to the user's session lifetime.
    /// This means when the user logs out, any revokable tokens will be removed.
    /// If using server-side sessions, expired sessions will also remove any revokable tokens, and backchannel logout will be triggered.
    /// This client's setting overrides the global CoordinateClientLifetimesWithUserSession configuration setting.
    /// </summary>
    public bool? CoordinateLifetimeWithUserSession { get; set; }


    /// <summary>
    /// Gets or sets the allowed CORS origins for JavaScript clients.
    /// Used by the default CORS policy service implementations (in-memory and EF) to build a CORS policy.
    /// </summary>
    /// <value>
    /// The allowed CORS origins.
    /// </value>
    public ICollection<string> AllowedCorsOrigins { get; set; } = new HashSet<string>();

    /// <summary>
    /// Gets or sets a URI that can be used to initiate login from the
    /// IdentityServer host or a third party. Commonly used to create a client application portal within the
    /// IdentityServer host. Defaults to <c>null</c>.
    /// See https://openid.net/specs/openid-connect-core-1_0.html#ThirdPartyInitiatedLogin
    /// </summary>
    public string? InitiateLoginUri { get; set; }

    /// <summary>
    /// Gets or sets the custom properties for the client. Use this dictionary to hold any custom client-specific values.
    /// </summary>
    /// <value>
    /// The properties.
    /// </value>
    public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

#pragma warning disable CA1033
    string IConnectedApplication.Identifier => ClientId;
    string? IConnectedApplication.DisplayName => ClientName;
    string? IConnectedApplication.Description => Description;
    bool IConnectedApplication.Enabled => Enabled;
    string IConnectedApplication.ProtocolType => ProtocolType;
    bool IConnectedApplication.RequireConsent => RequireConsent;
#pragma warning restore CA1033

    /// <summary>
    /// Validates the grant types.
    /// </summary>
    /// <param name="grantTypes">The grant types.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Grant types list is empty
    /// or
    /// Grant types cannot contain spaces
    /// or
    /// Grant types list contains duplicate values
    /// </exception>
    public static void ValidateGrantTypes(IEnumerable<string> grantTypes)
    {
        ArgumentNullException.ThrowIfNull(grantTypes);

        // spaces are not allowed in grant types
        foreach (var type in grantTypes)
        {
            if (type.Contains(' ', StringComparison.InvariantCulture))
            {
                throw new InvalidOperationException("Grant types cannot contain spaces");
            }
        }

        // single grant type, seems to be fine
        if (grantTypes.Count() == 1)
        {
            return;
        }

        // don't allow duplicate grant types
        if (grantTypes.Count() != grantTypes.Distinct().Count())
        {
            throw new InvalidOperationException("Grant types list contains duplicate values");
        }

        // would allow response_type downgrade attack from code to token
        DisallowGrantTypeCombination(GrantType.Implicit, GrantType.AuthorizationCode, grantTypes);
        DisallowGrantTypeCombination(GrantType.Implicit, GrantType.Hybrid, grantTypes);

        DisallowGrantTypeCombination(GrantType.AuthorizationCode, GrantType.Hybrid, grantTypes);
    }

    private static void DisallowGrantTypeCombination(string value1, string value2, IEnumerable<string> grantTypes)
    {
        if (grantTypes.Contains(value1, StringComparer.Ordinal) &&
            grantTypes.Contains(value2, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Grant types list cannot contain both {value1} and {value2}");
        }
    }

    internal sealed class GrantTypeValidatingHashSet : ICollection<string>
    {
        private readonly ICollection<string> _inner;

        public GrantTypeValidatingHashSet() => _inner = new HashSet<string>();

        public GrantTypeValidatingHashSet(IEnumerable<string> values) => _inner = new HashSet<string>(values);

        private HashSet<string> Clone() => new HashSet<string>(this);

        private HashSet<string> CloneWith(params string[] values)
        {
            var clone = Clone();
            foreach (var item in values)
            {
                clone.Add(item);
            }

            return clone;
        }

        public int Count => _inner.Count;

        public bool IsReadOnly => _inner.IsReadOnly;

        public void Add(string item)
        {
            ValidateGrantTypes(CloneWith(item));
            _inner.Add(item);
        }

        public void Clear() => _inner.Clear();

        public bool Contains(string item) => _inner.Contains(item);

        public void CopyTo(string[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);

        public IEnumerator<string> GetEnumerator() => _inner.GetEnumerator();

        public bool Remove(string item) => _inner.Remove(item);

        IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }
}

/// <summary>
/// Models how the client's DPoP token expiation should be validated.
/// </summary>
[Flags]
public enum DPoPTokenExpirationValidationMode
{
    // TODO: do we allow this custom/none?

    /// <summary>
    /// No built-in expiration validation.
    /// </summary>
    Custom = 0b_0000_0000,  // 0
    /// <summary>
    /// Validate the iat value
    /// </summary>
    Iat = 0b_0000_0001,  // 1
    /// <summary>
    /// Validate the nonce value
    /// </summary>
    Nonce = 0b_0000_0010,  // 2

    /// <summary>
    /// Validate both the iat and nonce values
    /// </summary>
    IatAndNonce = Iat | Nonce
}
