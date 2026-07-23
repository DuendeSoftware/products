// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Admin.Clients;

/// <summary>
/// Represents a client configuration used to update an existing client.
/// To add a new secret, use <see cref="IClientAdmin.CreateSecretAsync"/>.
/// To change a secret value, delete the existing secret and create a new one.
/// </summary>
public sealed class UpdateClient
{
    public required string ClientId { get; set; }
    public bool Enabled { get; set; } = true;
    public string? ClientName { get; set; }
    public string? Description { get; set; }
    public string? ClientUri { get; set; }
    public string? LogoUri { get; set; }
    public bool RequireClientSecret { get; set; } = true;
    public bool RequirePkce { get; set; } = true;
    public bool AllowPlainTextPkce { get; set; }
    public bool RequireRequestObject { get; set; }
    public bool RequireDPoP { get; set; }
    public DPoPTokenExpirationValidationMode DPoPValidationMode { get; set; } = DPoPTokenExpirationValidationMode.Iat;
    public TimeSpan DPoPClockSkew { get; set; } = TimeSpan.FromMinutes(5);
    public bool RequireConsent { get; set; }
    public bool AllowRememberConsent { get; set; } = true;
    public int? ConsentLifetime { get; set; }
    public bool AllowAccessTokensViaBrowser { get; set; }
    public bool AllowOfflineAccess { get; set; }
    public AccessTokenType AccessTokenType { get; set; } = AccessTokenType.Jwt;
    public bool IncludeJwtId { get; set; } = true;
    public int IdentityTokenLifetime { get; set; } = 300;
    public int AccessTokenLifetime { get; set; } = 3600;
    public int AuthorizationCodeLifetime { get; set; } = 300;
    public int AbsoluteRefreshTokenLifetime { get; set; } = 2592000;
    public int SlidingRefreshTokenLifetime { get; set; } = 1296000;
    public TokenUsage RefreshTokenUsage { get; set; } = TokenUsage.ReUse;
    public TokenExpiration RefreshTokenExpiration { get; set; } = TokenExpiration.Absolute;
    public bool UpdateAccessTokenClaimsOnRefresh { get; set; }
    public bool AlwaysIncludeUserClaimsInIdToken { get; set; }
    public bool AlwaysSendClientClaims { get; set; }
    public string? ClientClaimsPrefix { get; set; } = "client_";
    public string? PairWiseSubjectSalt { get; set; }
    public int? UserSsoLifetime { get; set; }
    public bool? CoordinateLifetimeWithUserSession { get; set; }
    public bool EnableLocalLogin { get; set; } = true;
    public string? FrontChannelLogoutUri { get; set; }
    public bool FrontChannelLogoutSessionRequired { get; set; } = true;
    public string? BackChannelLogoutUri { get; set; }
    public bool BackChannelLogoutSessionRequired { get; set; } = true;
    public string? InitiateLoginUri { get; set; }
    public bool RequirePushedAuthorization { get; set; }
    public int? PushedAuthorizationLifetime { get; set; }
    public string? UserCodeType { get; set; }
    public int DeviceCodeLifetime { get; set; } = 300;
    public int? CibaLifetime { get; set; }
    public int? PollingInterval { get; set; }
    public List<string>? AllowedGrantTypes { get; set; }
    public List<string>? AllowedScopes { get; set; }
    public List<string>? RedirectUris { get; set; }
    public List<string>? PostLogoutRedirectUris { get; set; }
    public List<string>? AllowedIdentityTokenSigningAlgorithms { get; set; }
    public List<string>? IdentityProviderRestrictions { get; set; }
    public List<string>? AllowedCorsOrigins { get; set; }
    public List<ClientClaimConfiguration>? Claims { get; set; }
    public AttributeValueCollection ExtendedProperties { get; init; } = new();
}
