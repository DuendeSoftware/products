// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

using System;
using System.Collections.Generic;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.EntityFramework.Entities;

public class Client
{
    public int Id { get; set; }
    public bool Enabled { get; set; } = true;
    public string ClientId { get; set; }
    public string ProtocolType { get; set; } = "oidc";
    public List<ClientSecret> ClientSecrets { get; set; }
    public bool RequireClientSecret { get; set; } = true;
    public string ClientName { get; set; }
    public string Description { get; set; }
    public string ClientUri { get; set; }
    public string LogoUri { get; set; }
    public bool RequireConsent { get; set; } = false;
    public bool AllowRememberConsent { get; set; } = true;
    public bool AlwaysIncludeUserClaimsInIdToken { get; set; }
    public List<ClientGrantType> AllowedGrantTypes { get; set; }
    public bool RequirePkce { get; set; } = true;
    public bool AllowPlainTextPkce { get; set; }
    public bool RequireRequestObject { get; set; }
    public bool AllowAccessTokensViaBrowser { get; set; }
    public bool RequireDPoP { get; set; }
    public DPoPTokenExpirationValidationMode DPoPValidationMode { get; set; }
    public TimeSpan DPoPClockSkew { get; set; } = TimeSpan.FromMinutes(5);
    public List<ClientRedirectUri> RedirectUris { get; set; }
    public List<ClientPostLogoutRedirectUri> PostLogoutRedirectUris { get; set; }
    public string FrontChannelLogoutUri { get; set; }
    public bool FrontChannelLogoutSessionRequired { get; set; } = true;
    public string BackChannelLogoutUri { get; set; }
    public bool BackChannelLogoutSessionRequired { get; set; } = true;
    public bool AllowOfflineAccess { get; set; }
    public List<ClientScope> AllowedScopes { get; set; }
    public int IdentityTokenLifetime { get; set; } = 300;
    public string AllowedIdentityTokenSigningAlgorithms { get; set; }
    public int AccessTokenLifetime { get; set; } = 3600;
    public int AuthorizationCodeLifetime { get; set; } = 300;
    public int? ConsentLifetime { get; set; } = null;
    public int AbsoluteRefreshTokenLifetime { get; set; } = 2592000;
    public int SlidingRefreshTokenLifetime { get; set; } = 1296000;
    public int RefreshTokenUsage { get; set; } = (int)TokenUsage.OneTimeOnly;
    public bool UpdateAccessTokenClaimsOnRefresh { get; set; }
    public int RefreshTokenExpiration { get; set; } = (int)TokenExpiration.Absolute;
    public int AccessTokenType { get; set; } = 0; // AccessTokenType.Jwt;
    public bool EnableLocalLogin { get; set; } = true;
    public List<ClientIdPRestriction> IdentityProviderRestrictions { get; set; }
    public bool IncludeJwtId { get; set; }
    public List<ClientClaim> Claims { get; set; }
    public bool AlwaysSendClientClaims { get; set; }
    public string ClientClaimsPrefix { get; set; } = "client_";
    public string PairWiseSubjectSalt { get; set; }
    public List<ClientCorsOrigin> AllowedCorsOrigins { get; set; }
    public string InitiateLoginUri { get; set; }
    public List<ClientProperty> Properties { get; set; }
    public int? UserSsoLifetime { get; set; }
    public string UserCodeType { get; set; }
    public int DeviceCodeLifetime { get; set; } = 300;

    public int? CibaLifetime { get; set; }
    public int? PollingInterval { get; set; }

    public bool? CoordinateLifetimeWithUserSession { get; set; }

    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime? Updated { get; set; }
    public DateTime? LastAccessed { get; set; }
    public bool NonEditable { get; set; }
    public int? PushedAuthorizationLifetime { get; set; }
    public bool RequirePushedAuthorization { get; set; }
}