// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Runtime.CompilerServices;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.Models;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.Clients;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ClientStore(
    ClientRepository repository,
    ILogger<ClientStore> logger) : IClientStore
{
    private const int PageSize = 200;

    /// <inheritdoc/>
    public async Task<Client?> FindClientByIdAsync(string clientId, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ClientStore.FindClientById");
        activity?.SetTag(Tracing.Properties.ClientId, clientId);

        var result = await repository.TryReadByClientIdAsync(clientId, ct);
        var model = result is null ? null : MapToClient(result.Value.Dso);

        logger.ClientFound(LogLevel.Debug, clientId, model != null);

        return model;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Client> GetAllClientsAsync([EnumeratorCancellation] Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ClientStore.GetAllClients");

        var pageNumber = 1;
        var clientCount = 0;

        while (true)
        {
            var range = DataRange.FromPage(pageNumber, PageSize);
            var request = QueryRequest.Create<ClientFilter, ClientSortField>(range);
            var result = await repository.QueryAsync(request, ct);

            foreach (var dso in result.Items)
            {
                clientCount++;
                yield return MapToClient(dso);
            }

            if (!result.HasMoreData)
            {
                break;
            }

            pageNumber++;
        }

        logger.ClientsRetrieved(LogLevel.Debug, clientCount);
    }

    private static Client MapToClient(ClientDso.V1 dso) => new()
    {
        // Identity
        Enabled = dso.Enabled,
        ClientId = dso.ClientId,
        ProtocolType = dso.ProtocolType,

        // Display
        ClientName = dso.ClientName,
        Description = dso.Description,
        ClientUri = dso.ClientUri,
        LogoUri = dso.LogoUri,

        // Authentication
        RequireClientSecret = dso.RequireClientSecret,
        RequirePkce = dso.RequirePkce,
        AllowPlainTextPkce = dso.AllowPlainTextPkce,
        RequireRequestObject = dso.RequireRequestObject,
        RequireDPoP = dso.RequireDPoP,
        DPoPValidationMode = (DPoPTokenExpirationValidationMode)dso.DPoPValidationMode,
        DPoPClockSkew = TimeSpan.FromTicks(dso.DPoPClockSkewTicks),

        // Consent
        RequireConsent = dso.RequireConsent,
        AllowRememberConsent = dso.AllowRememberConsent,
        ConsentLifetime = dso.ConsentLifetime,

        // Tokens
        AllowAccessTokensViaBrowser = dso.AllowAccessTokensViaBrowser,
        AllowOfflineAccess = dso.AllowOfflineAccess,
        AccessTokenType = (AccessTokenType)dso.AccessTokenType,
        IncludeJwtId = dso.IncludeJwtId,
        AlwaysIncludeUserClaimsInIdToken = dso.AlwaysIncludeUserClaimsInIdToken,
        AlwaysSendClientClaims = dso.AlwaysSendClientClaims,
        IdentityTokenLifetime = dso.IdentityTokenLifetime,
        AccessTokenLifetime = dso.AccessTokenLifetime,
        AuthorizationCodeLifetime = dso.AuthorizationCodeLifetime,

        // Refresh tokens
        AbsoluteRefreshTokenLifetime = dso.AbsoluteRefreshTokenLifetime,
        SlidingRefreshTokenLifetime = dso.SlidingRefreshTokenLifetime,
        RefreshTokenUsage = (TokenUsage)dso.RefreshTokenUsage,
        RefreshTokenExpiration = (TokenExpiration)dso.RefreshTokenExpiration,
        UpdateAccessTokenClaimsOnRefresh = dso.UpdateAccessTokenClaimsOnRefresh,

        // Claims metadata
        ClientClaimsPrefix = dso.ClientClaimsPrefix,
        PairWiseSubjectSalt = dso.PairWiseSubjectSalt,

        // Session
        UserSsoLifetime = dso.UserSsoLifetime,
        CoordinateLifetimeWithUserSession = dso.CoordinateLifetimeWithUserSession,

        // Login/Logout
        EnableLocalLogin = dso.EnableLocalLogin,
        FrontChannelLogoutUri = dso.FrontChannelLogoutUri,
        FrontChannelLogoutSessionRequired = dso.FrontChannelLogoutSessionRequired,
        BackChannelLogoutUri = dso.BackChannelLogoutUri,
        BackChannelLogoutSessionRequired = dso.BackChannelLogoutSessionRequired,
        InitiateLoginUri = dso.InitiateLoginUri,

        // PAR
        RequirePushedAuthorization = dso.RequirePushedAuthorization,
        PushedAuthorizationLifetime = dso.PushedAuthorizationLifetime,

        // Device/CIBA
        UserCodeType = dso.UserCodeType,
        DeviceCodeLifetime = dso.DeviceCodeLifetime,
        CibaLifetime = dso.CibaLifetime,
        PollingInterval = dso.PollingInterval,

        // Collections
        AllowedGrantTypes = new List<string>(dso.AllowedGrantTypes),
        AllowedScopes = new List<string>(dso.AllowedScopes),
        RedirectUris = new List<string>(dso.RedirectUris),
        PostLogoutRedirectUris = new List<string>(dso.PostLogoutRedirectUris),
        AllowedIdentityTokenSigningAlgorithms = new HashSet<string>(dso.AllowedIdentityTokenSigningAlgorithms),
        IdentityProviderRestrictions = new List<string>(dso.IdentityProviderRestrictions),
        AllowedCorsOrigins = new List<string>(dso.AllowedCorsOrigins),
        Properties = EavPropertyMapper.ExtractStringProperties(dso.ExtendedAttributeValues),

        // Secrets
        ClientSecrets = dso.ClientSecrets.Select(s => new Secret
        {
            Value = s.Value,
            Description = s.Description,
            Expiration = s.Expiration,
            Type = s.Type
        }).ToList(),

        // Claims
        Claims = dso.Claims.Select(c => new ClientClaim(c.Type, c.Value, c.ValueType)).ToList()
    };
}
