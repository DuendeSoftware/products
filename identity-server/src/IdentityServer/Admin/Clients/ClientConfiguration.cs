// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Admin.Clients;

/// <summary>
/// Represents a client configuration returned by admin read operations.
/// </summary>
public sealed class ClientConfiguration
{
    // === Identity ===

    /// <summary>
    /// The OAuth <c>client_id</c>. Required. Primary business identifier.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Whether the client is enabled. Defaults to <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; init; } = true;

    // === Display ===

    /// <summary>
    /// A display-friendly name for the client (used in logging and consent screens).
    /// </summary>
    public string? ClientName { get; init; }

    /// <summary>
    /// A description of the client.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// URI to further information about the client (used on consent screens).
    /// </summary>
    public string? ClientUri { get; init; }

    /// <summary>
    /// URI to the client logo (used on consent screens).
    /// </summary>
    public string? LogoUri { get; init; }

    // === Authentication ===

    /// <summary>
    /// Whether a client secret is required to request tokens. Defaults to <see langword="true"/>.
    /// </summary>
    public bool RequireClientSecret { get; init; } = true;

    /// <summary>
    /// Whether PKCE is required for authorization code flows. Defaults to <see langword="true"/>.
    /// </summary>
    public bool RequirePkce { get; init; } = true;

    /// <summary>
    /// Whether plain-text PKCE code verifiers are allowed. Defaults to <see langword="false"/>.
    /// </summary>
    public bool AllowPlainTextPkce { get; init; }

    /// <summary>
    /// Whether the client must use request objects on authorize requests. Defaults to <see langword="false"/>.
    /// </summary>
    public bool RequireRequestObject { get; init; }

    /// <summary>
    /// Whether DPoP (Demonstrating Proof-of-Possession) is required. Defaults to <see langword="false"/>.
    /// </summary>
    public bool RequireDPoP { get; init; }

    /// <summary>
    /// The DPoP proof token expiration validation mode. Defaults to <see cref="DPoPTokenExpirationValidationMode.Iat"/>.
    /// </summary>
    public DPoPTokenExpirationValidationMode DPoPValidationMode { get; init; } = DPoPTokenExpirationValidationMode.Iat;

    /// <summary>
    /// Clock skew tolerance when validating the DPoP proof token <c>iat</c> claim. Defaults to 5 minutes.
    /// </summary>
    public TimeSpan DPoPClockSkew { get; init; } = TimeSpan.FromMinutes(5);

    // === Consent ===

    /// <summary>
    /// Whether a consent screen is required. Defaults to <see langword="false"/>.
    /// </summary>
    public bool RequireConsent { get; init; }

    /// <summary>
    /// Whether users can choose to remember consent decisions. Defaults to <see langword="true"/>.
    /// </summary>
    public bool AllowRememberConsent { get; init; } = true;

    /// <summary>
    /// Remembered consent lifetime in seconds. <see langword="null"/> means no expiration.
    /// </summary>
    public int? ConsentLifetime { get; init; }

    // === Token Settings ===

    /// <summary>
    /// Whether access tokens may be transmitted via the browser. Defaults to <see langword="false"/>.
    /// </summary>
    public bool AllowAccessTokensViaBrowser { get; init; }

    /// <summary>
    /// Whether offline access (refresh tokens) is allowed by requesting the <c>offline_access</c> scope. Defaults to <see langword="false"/>.
    /// </summary>
    public bool AllowOfflineAccess { get; init; }

    /// <summary>
    /// The access token type (JWT or reference token). Defaults to <see cref="AccessTokenType.Jwt"/>.
    /// </summary>
    public AccessTokenType AccessTokenType { get; init; } = AccessTokenType.Jwt;

    /// <summary>
    /// Whether JWT access tokens include a unique identifier via the <c>jti</c> claim. Defaults to <see langword="true"/>.
    /// </summary>
    public bool IncludeJwtId { get; init; } = true;

    /// <summary>
    /// Identity token lifetime in seconds. Defaults to 300 (5 minutes).
    /// </summary>
    public int IdentityTokenLifetime { get; init; } = 300;

    /// <summary>
    /// Access token lifetime in seconds. Defaults to 3600 (1 hour).
    /// </summary>
    public int AccessTokenLifetime { get; init; } = 3600;

    /// <summary>
    /// Authorization code lifetime in seconds. Defaults to 300 (5 minutes).
    /// </summary>
    public int AuthorizationCodeLifetime { get; init; } = 300;

    // === Refresh Token ===

    /// <summary>
    /// Maximum absolute refresh token lifetime in seconds. Defaults to 2592000 (30 days).
    /// </summary>
    public int AbsoluteRefreshTokenLifetime { get; init; } = 2592000;

    /// <summary>
    /// Sliding refresh token lifetime in seconds. Defaults to 1296000 (15 days).
    /// </summary>
    public int SlidingRefreshTokenLifetime { get; init; } = 1296000;

    /// <summary>
    /// Refresh token usage mode. Defaults to <see cref="TokenUsage.ReUse"/>.
    /// </summary>
    public TokenUsage RefreshTokenUsage { get; init; } = TokenUsage.ReUse;

    /// <summary>
    /// Refresh token expiration mode. Defaults to <see cref="TokenExpiration.Absolute"/>.
    /// </summary>
    public TokenExpiration RefreshTokenExpiration { get; init; } = TokenExpiration.Absolute;

    /// <summary>
    /// Whether access token claims are updated when a refresh token is used. Defaults to <see langword="false"/>.
    /// </summary>
    public bool UpdateAccessTokenClaimsOnRefresh { get; init; }

    // === Claims ===

    /// <summary>
    /// Whether user claims are always included in the identity token rather than requiring
    /// the client to call the userinfo endpoint. Defaults to <see langword="false"/>.
    /// </summary>
    public bool AlwaysIncludeUserClaimsInIdToken { get; init; }

    /// <summary>
    /// Whether client claims are always sent in access tokens (not just for client credentials flow).
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool AlwaysSendClientClaims { get; init; }

    /// <summary>
    /// Prefix applied to client claim types to avoid collisions with user claims.
    /// Defaults to <c>"client_"</c>.
    /// </summary>
    public string? ClientClaimsPrefix { get; init; } = "client_";

    /// <summary>
    /// Salt value used in pairwise subject identifier generation for users of this client.
    /// </summary>
    public string? PairWiseSubjectSalt { get; init; }

    // === Session ===

    /// <summary>
    /// Maximum duration in seconds since the user last authenticated.
    /// <see langword="null"/> means no restriction.
    /// </summary>
    public int? UserSsoLifetime { get; init; }

    /// <summary>
    /// Whether token lifetimes are coordinated with the user's session lifetime.
    /// Overrides the global <c>CoordinateClientLifetimesWithUserSession</c> setting when set.
    /// </summary>
    public bool? CoordinateLifetimeWithUserSession { get; init; }

    // === Login / Logout ===

    /// <summary>
    /// Whether local login is allowed for this client. Defaults to <see langword="true"/>.
    /// </summary>
    public bool EnableLocalLogin { get; init; } = true;

    /// <summary>
    /// Front-channel logout URI for HTTP front-channel based logout.
    /// </summary>
    public string? FrontChannelLogoutUri { get; init; }

    /// <summary>
    /// Whether the user session ID is sent to <see cref="FrontChannelLogoutUri"/>. Defaults to <see langword="true"/>.
    /// </summary>
    public bool FrontChannelLogoutSessionRequired { get; init; } = true;

    /// <summary>
    /// Back-channel logout URI for HTTP back-channel based logout.
    /// </summary>
    public string? BackChannelLogoutUri { get; init; }

    /// <summary>
    /// Whether the user session ID is sent to <see cref="BackChannelLogoutUri"/>. Defaults to <see langword="true"/>.
    /// </summary>
    public bool BackChannelLogoutSessionRequired { get; init; } = true;

    /// <summary>
    /// URI that can be used to initiate login from IdentityServer or a third party.
    /// </summary>
    public string? InitiateLoginUri { get; init; }

    // === PAR ===

    /// <summary>
    /// Whether pushed authorization requests (PAR) are required for this client. Defaults to <see langword="false"/>.
    /// </summary>
    public bool RequirePushedAuthorization { get; init; }

    /// <summary>
    /// PAR request lifetime in seconds. <see langword="null"/> means the global configuration value is used.
    /// </summary>
    public int? PushedAuthorizationLifetime { get; init; }

    // === Device Flow / CIBA ===

    /// <summary>
    /// The user code type for device flow. <see langword="null"/> falls back to the server default.
    /// </summary>
    public string? UserCodeType { get; init; }

    /// <summary>
    /// Device code lifetime in seconds. Defaults to 300 (5 minutes).
    /// </summary>
    public int DeviceCodeLifetime { get; init; } = 300;

    /// <summary>
    /// CIBA backchannel authentication request lifetime in seconds.
    /// <see langword="null"/> means the server default is used.
    /// </summary>
    public int? CibaLifetime { get; init; }

    /// <summary>
    /// Maximum polling interval in seconds for device flow and CIBA.
    /// <see langword="null"/> means the global default is used.
    /// </summary>
    public int? PollingInterval { get; init; }

    // === Collections ===

    /// <summary>
    /// Allowed grant types (e.g., <c>"authorization_code"</c>, <c>"client_credentials"</c>).
    /// </summary>
    public IReadOnlyList<string>? AllowedGrantTypes { get; init; }

    /// <summary>
    /// Allowed scopes the client may request.
    /// </summary>
    public IReadOnlyList<string>? AllowedScopes { get; init; }

    /// <summary>
    /// Allowed redirect URIs for token and authorization code delivery.
    /// </summary>
    public IReadOnlyList<string>? RedirectUris { get; init; }

    /// <summary>
    /// Allowed post-logout redirect URIs.
    /// </summary>
    public IReadOnlyList<string>? PostLogoutRedirectUris { get; init; }

    /// <summary>
    /// Allowed signing algorithms for identity tokens.
    /// If empty, the server default signing algorithm is used.
    /// </summary>
    public IReadOnlyList<string>? AllowedIdentityTokenSigningAlgorithms { get; init; }

    /// <summary>
    /// External identity provider restrictions.
    /// If empty, all configured identity providers are allowed.
    /// </summary>
    public IReadOnlyList<string>? IdentityProviderRestrictions { get; init; }

    /// <summary>
    /// Allowed CORS origins for JavaScript clients.
    /// </summary>
    public IReadOnlyList<string>? AllowedCorsOrigins { get; init; }

    /// <summary>
    /// Client claims to be included in tokens.
    /// </summary>
    public IReadOnlyList<ClientClaimConfiguration>? Claims { get; init; }

    /// <summary>
    /// Client secrets — metadata only. The secret value is never exposed.
    /// To add a new secret, use <c>CreateSecretAsync</c> (accepts plaintext, hashes before storage).
    /// To change a secret value, delete the existing secret and create a new one.
    /// </summary>
    public IReadOnlyList<ClientSecretConfiguration>? ClientSecrets { get; init; }

    /// <summary>
    /// Extended attributes for this client, validated against a configured schema at the store boundary.
    /// Use this to attach arbitrary typed metadata to a client.
    /// </summary>
    public IReadOnlyCollection<AttributeValue> ExtendedProperties { get; init; } = [];

    /// <summary>
    /// Data version for optimistic concurrency. <see langword="null"/> for new clients.
    /// </summary>
    public DataVersion? Version { get; init; }

    /// <summary>
    /// Creates an update model from this configuration.
    /// </summary>
    public UpdateClient ToUpdate() => new()
    {
        ClientId = ClientId,
        Enabled = Enabled,
        ClientName = ClientName,
        Description = Description,
        ClientUri = ClientUri,
        LogoUri = LogoUri,
        RequireClientSecret = RequireClientSecret,
        RequirePkce = RequirePkce,
        AllowPlainTextPkce = AllowPlainTextPkce,
        RequireRequestObject = RequireRequestObject,
        RequireDPoP = RequireDPoP,
        DPoPValidationMode = DPoPValidationMode,
        DPoPClockSkew = DPoPClockSkew,
        RequireConsent = RequireConsent,
        AllowRememberConsent = AllowRememberConsent,
        ConsentLifetime = ConsentLifetime,
        AllowAccessTokensViaBrowser = AllowAccessTokensViaBrowser,
        AllowOfflineAccess = AllowOfflineAccess,
        AccessTokenType = AccessTokenType,
        IncludeJwtId = IncludeJwtId,
        IdentityTokenLifetime = IdentityTokenLifetime,
        AccessTokenLifetime = AccessTokenLifetime,
        AuthorizationCodeLifetime = AuthorizationCodeLifetime,
        AbsoluteRefreshTokenLifetime = AbsoluteRefreshTokenLifetime,
        SlidingRefreshTokenLifetime = SlidingRefreshTokenLifetime,
        RefreshTokenUsage = RefreshTokenUsage,
        RefreshTokenExpiration = RefreshTokenExpiration,
        UpdateAccessTokenClaimsOnRefresh = UpdateAccessTokenClaimsOnRefresh,
        AlwaysIncludeUserClaimsInIdToken = AlwaysIncludeUserClaimsInIdToken,
        AlwaysSendClientClaims = AlwaysSendClientClaims,
        ClientClaimsPrefix = ClientClaimsPrefix,
        PairWiseSubjectSalt = PairWiseSubjectSalt,
        UserSsoLifetime = UserSsoLifetime,
        CoordinateLifetimeWithUserSession = CoordinateLifetimeWithUserSession,
        EnableLocalLogin = EnableLocalLogin,
        FrontChannelLogoutUri = FrontChannelLogoutUri,
        FrontChannelLogoutSessionRequired = FrontChannelLogoutSessionRequired,
        BackChannelLogoutUri = BackChannelLogoutUri,
        BackChannelLogoutSessionRequired = BackChannelLogoutSessionRequired,
        InitiateLoginUri = InitiateLoginUri,
        RequirePushedAuthorization = RequirePushedAuthorization,
        PushedAuthorizationLifetime = PushedAuthorizationLifetime,
        UserCodeType = UserCodeType,
        DeviceCodeLifetime = DeviceCodeLifetime,
        CibaLifetime = CibaLifetime,
        PollingInterval = PollingInterval,
        AllowedGrantTypes = Copy(AllowedGrantTypes),
        AllowedScopes = Copy(AllowedScopes),
        RedirectUris = Copy(RedirectUris),
        PostLogoutRedirectUris = Copy(PostLogoutRedirectUris),
        AllowedIdentityTokenSigningAlgorithms = Copy(AllowedIdentityTokenSigningAlgorithms),
        IdentityProviderRestrictions = Copy(IdentityProviderRestrictions),
        AllowedCorsOrigins = Copy(AllowedCorsOrigins),
        Claims = Claims?.Select(c => new ClientClaimConfiguration
        {
            Type = c.Type,
            Value = c.Value,
            ValueType = c.ValueType
        }).ToList(),
        ExtendedProperties = CopyExtendedProperties()
    };

    /// <summary>
    /// Creates a create model from this configuration.
    /// </summary>
    public CreateClient ToCreate()
    {
        var update = ToUpdate();
        return new CreateClient
        {
            ClientId = update.ClientId,
            Enabled = update.Enabled,
            ClientName = update.ClientName,
            Description = update.Description,
            ClientUri = update.ClientUri,
            LogoUri = update.LogoUri,
            RequireClientSecret = update.RequireClientSecret,
            RequirePkce = update.RequirePkce,
            AllowPlainTextPkce = update.AllowPlainTextPkce,
            RequireRequestObject = update.RequireRequestObject,
            RequireDPoP = update.RequireDPoP,
            DPoPValidationMode = update.DPoPValidationMode,
            DPoPClockSkew = update.DPoPClockSkew,
            RequireConsent = update.RequireConsent,
            AllowRememberConsent = update.AllowRememberConsent,
            ConsentLifetime = update.ConsentLifetime,
            AllowAccessTokensViaBrowser = update.AllowAccessTokensViaBrowser,
            AllowOfflineAccess = update.AllowOfflineAccess,
            AccessTokenType = update.AccessTokenType,
            IncludeJwtId = update.IncludeJwtId,
            IdentityTokenLifetime = update.IdentityTokenLifetime,
            AccessTokenLifetime = update.AccessTokenLifetime,
            AuthorizationCodeLifetime = update.AuthorizationCodeLifetime,
            AbsoluteRefreshTokenLifetime = update.AbsoluteRefreshTokenLifetime,
            SlidingRefreshTokenLifetime = update.SlidingRefreshTokenLifetime,
            RefreshTokenUsage = update.RefreshTokenUsage,
            RefreshTokenExpiration = update.RefreshTokenExpiration,
            UpdateAccessTokenClaimsOnRefresh = update.UpdateAccessTokenClaimsOnRefresh,
            AlwaysIncludeUserClaimsInIdToken = update.AlwaysIncludeUserClaimsInIdToken,
            AlwaysSendClientClaims = update.AlwaysSendClientClaims,
            ClientClaimsPrefix = update.ClientClaimsPrefix,
            PairWiseSubjectSalt = update.PairWiseSubjectSalt,
            UserSsoLifetime = update.UserSsoLifetime,
            CoordinateLifetimeWithUserSession = update.CoordinateLifetimeWithUserSession,
            EnableLocalLogin = update.EnableLocalLogin,
            FrontChannelLogoutUri = update.FrontChannelLogoutUri,
            FrontChannelLogoutSessionRequired = update.FrontChannelLogoutSessionRequired,
            BackChannelLogoutUri = update.BackChannelLogoutUri,
            BackChannelLogoutSessionRequired = update.BackChannelLogoutSessionRequired,
            InitiateLoginUri = update.InitiateLoginUri,
            RequirePushedAuthorization = update.RequirePushedAuthorization,
            PushedAuthorizationLifetime = update.PushedAuthorizationLifetime,
            UserCodeType = update.UserCodeType,
            DeviceCodeLifetime = update.DeviceCodeLifetime,
            CibaLifetime = update.CibaLifetime,
            PollingInterval = update.PollingInterval,
            AllowedGrantTypes = update.AllowedGrantTypes,
            AllowedScopes = update.AllowedScopes,
            RedirectUris = update.RedirectUris,
            PostLogoutRedirectUris = update.PostLogoutRedirectUris,
            AllowedIdentityTokenSigningAlgorithms = update.AllowedIdentityTokenSigningAlgorithms,
            IdentityProviderRestrictions = update.IdentityProviderRestrictions,
            AllowedCorsOrigins = update.AllowedCorsOrigins,
            Claims = update.Claims,
            ExtendedProperties = update.ExtendedProperties
        };
    }

    private static List<string>? Copy(IReadOnlyList<string>? values) => values is null ? null : [.. values];

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
