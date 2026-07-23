// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Stores.Storage.Clients;

internal class ClientExtensionPointValidator(IClientConfigurationValidator validator) : IConfigurationValidator<ClientConfiguration>
{
    public async Task<IReadOnlyList<AdminError>> ValidateAsync(ClientConfiguration configuration, Ct ct)
    {
        var isClient = MapToIsClient(configuration);
        var context = new ClientConfigurationValidationContext(isClient);
        await validator.ValidateAsync(context, ct);

        return context.IsValid ? [] : [AdminError.ValidationFailed(context.ErrorMessage!)];
    }

    private static Client MapToIsClient(ClientConfiguration configuration) =>
        new()
        {
            ClientId = configuration.ClientId,
            ProtocolType = IdentityServerConstants.ProtocolTypes.OpenIdConnect,
            Enabled = configuration.Enabled,

            // Display
            ClientName = configuration.ClientName,
            Description = configuration.Description,
            ClientUri = configuration.ClientUri,
            LogoUri = configuration.LogoUri,

            // Authentication
            RequireClientSecret = configuration.RequireClientSecret,
            RequirePkce = configuration.RequirePkce,
            AllowPlainTextPkce = configuration.AllowPlainTextPkce,
            RequireRequestObject = configuration.RequireRequestObject,
            RequireDPoP = configuration.RequireDPoP,
            DPoPValidationMode = configuration.DPoPValidationMode,
            DPoPClockSkew = configuration.DPoPClockSkew,

            // Consent
            RequireConsent = configuration.RequireConsent,
            AllowRememberConsent = configuration.AllowRememberConsent,
            ConsentLifetime = configuration.ConsentLifetime,

            // Tokens
            AllowAccessTokensViaBrowser = configuration.AllowAccessTokensViaBrowser,
            AllowOfflineAccess = configuration.AllowOfflineAccess,
            AccessTokenType = configuration.AccessTokenType,
            IncludeJwtId = configuration.IncludeJwtId,
            AlwaysIncludeUserClaimsInIdToken = configuration.AlwaysIncludeUserClaimsInIdToken,
            AlwaysSendClientClaims = configuration.AlwaysSendClientClaims,
            IdentityTokenLifetime = configuration.IdentityTokenLifetime,
            AccessTokenLifetime = configuration.AccessTokenLifetime,
            AuthorizationCodeLifetime = configuration.AuthorizationCodeLifetime,

            // Refresh
            AbsoluteRefreshTokenLifetime = configuration.AbsoluteRefreshTokenLifetime,
            SlidingRefreshTokenLifetime = configuration.SlidingRefreshTokenLifetime,
            RefreshTokenUsage = configuration.RefreshTokenUsage,
            RefreshTokenExpiration = configuration.RefreshTokenExpiration,
            UpdateAccessTokenClaimsOnRefresh = configuration.UpdateAccessTokenClaimsOnRefresh,

            // Claims metadata
            ClientClaimsPrefix = configuration.ClientClaimsPrefix,
            PairWiseSubjectSalt = configuration.PairWiseSubjectSalt,

            // Session
            UserSsoLifetime = configuration.UserSsoLifetime,
            CoordinateLifetimeWithUserSession = configuration.CoordinateLifetimeWithUserSession,

            // Login/Logout
            EnableLocalLogin = configuration.EnableLocalLogin,
            FrontChannelLogoutUri = configuration.FrontChannelLogoutUri,
            FrontChannelLogoutSessionRequired = configuration.FrontChannelLogoutSessionRequired,
            BackChannelLogoutUri = configuration.BackChannelLogoutUri,
            BackChannelLogoutSessionRequired = configuration.BackChannelLogoutSessionRequired,
            InitiateLoginUri = configuration.InitiateLoginUri,

            // PAR
            RequirePushedAuthorization = configuration.RequirePushedAuthorization,
            PushedAuthorizationLifetime = configuration.PushedAuthorizationLifetime,

            // Device/CIBA
            UserCodeType = configuration.UserCodeType,
            DeviceCodeLifetime = configuration.DeviceCodeLifetime,
            CibaLifetime = configuration.CibaLifetime,
            PollingInterval = configuration.PollingInterval,

            // Collections
            AllowedGrantTypes = configuration.AllowedGrantTypes?.ToList() ?? [],
            AllowedScopes = new HashSet<string>(configuration.AllowedScopes ?? []),
            RedirectUris = new HashSet<string>(configuration.RedirectUris ?? []),
            PostLogoutRedirectUris = new HashSet<string>(configuration.PostLogoutRedirectUris ?? []),
            AllowedIdentityTokenSigningAlgorithms = new HashSet<string>(configuration.AllowedIdentityTokenSigningAlgorithms ?? []),
            IdentityProviderRestrictions = new HashSet<string>(configuration.IdentityProviderRestrictions ?? []),
            AllowedCorsOrigins = new HashSet<string>(configuration.AllowedCorsOrigins ?? []),

            Properties = configuration.ExtendedProperties
                .OfType<AttributeValue<string>>()
                .ToDictionary(a => a.Code.Value, a => a.TypedValue),

            Claims = configuration.Claims?
                .Select(c => new ClientClaim(c.Type, c.Value, c.ValueType))
                .ToList() ?? [],

            ClientSecrets = configuration.ClientSecrets?
                .Select(secret => new Secret(
                    // We don't need a secret here. (actually, we don't even have the secret in the model)
                    value: "explicitly-not-set",
                    description: secret.Description ?? "",
                    expiration: secret.Expiration)
                {
                    Type = secret.Type,
                })
                .ToList() ?? [],
        };
}
