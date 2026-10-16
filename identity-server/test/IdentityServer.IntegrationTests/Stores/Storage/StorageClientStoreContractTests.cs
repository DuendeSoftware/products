// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.Clients;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public class StorageClientStoreContractTests : ClientStoreContractTests
{
    private readonly string _dbName = $"StorageClientContract_{Guid.NewGuid():N}";
    private ServiceProvider _provider = null!;
    private readonly List<ServiceProvider> _isolatedProviders = [];

    public override async ValueTask InitializeAsync() => _provider = await StorageContractTestServices.CreateAsync(_dbName, b => b.AddConfigurationStorage());

    public override async ValueTask DisposeAsync()
    {
        try
        {
            DataCategoryNameRecorder.CurrentTestShouldHaveUsed([.. _isolatedProviders, _provider], DataCategoryName.Configuration);
        }
        finally
        {
            foreach (var provider in _isolatedProviders)
            {
                await provider.DisposeAsync();
            }
            await _provider.DisposeAsync();
            await base.DisposeAsync();
        }
    }

    protected override StoreHandle<IClientStore> CreateStore() => new StoreHandle<IClientStore>(_provider.GetRequiredService<IClientStore>());

    protected override async Task<StoreHandle<IClientStore>> CreateIsolatedStoreAsync()
    {
        var isolatedDbName = $"StorageClientContractIsolated_{Guid.NewGuid():N}";
        var provider = await StorageContractTestServices.CreateAsync(isolatedDbName, b => b.AddConfigurationStorage());
        _isolatedProviders.Add(provider);

        return new StoreHandle<IClientStore>(provider.GetRequiredService<IClientStore>());
    }

    // NOTE: Seeds through ClientRepository rather than IClientAdmin on purpose. IClientAdmin validates
    // clients like production does and rejects most contract fixtures (no allowed grant types), and it
    // hashes secret values, while the fixtures pass values that are already hashed. These tests check
    // what the store does with stored data, not admin validation.
    protected override async Task SeedClientAsync(Client client)
    {
        var repo = _provider.GetRequiredService<ClientRepository>();
        var dso = MapToDso(client);
        await repo.CreateAsync(UuidV7.New(), dso, TestContext.Current.CancellationToken);
    }

    protected override async Task SeedClientsAsync(IEnumerable<Client> clients)
    {
        foreach (var client in clients)
        {
            await SeedClientAsync(client);
        }
    }

    // NOTE: This mapping must be kept in sync with ClientDso.V1 as fields are added.
    // If tests start failing after a ClientDso.V1 change, check here first.
    private static ClientDso.V1 MapToDso(Client client) => new()
    {
        Id = Guid.NewGuid(),
        ClientId = client.ClientId,
        Enabled = client.Enabled,
        ProtocolType = client.ProtocolType,
        ClientName = client.ClientName,
        Description = client.Description,
        ClientUri = client.ClientUri,
        LogoUri = client.LogoUri,
        RequireClientSecret = client.RequireClientSecret,
        RequirePkce = client.RequirePkce,
        AllowPlainTextPkce = client.AllowPlainTextPkce,
        RequireRequestObject = client.RequireRequestObject,
        RequireDPoP = client.RequireDPoP,
        DPoPValidationMode = (int)client.DPoPValidationMode,
        DPoPClockSkewTicks = client.DPoPClockSkew.Ticks,
        RequireConsent = client.RequireConsent,
        AllowRememberConsent = client.AllowRememberConsent,
        ConsentLifetime = client.ConsentLifetime,
        AllowAccessTokensViaBrowser = client.AllowAccessTokensViaBrowser,
        AllowOfflineAccess = client.AllowOfflineAccess,
        AccessTokenType = (int)client.AccessTokenType,
        IncludeJwtId = client.IncludeJwtId,
        IdentityTokenLifetime = client.IdentityTokenLifetime,
        AccessTokenLifetime = client.AccessTokenLifetime,
        AuthorizationCodeLifetime = client.AuthorizationCodeLifetime,
        AbsoluteRefreshTokenLifetime = client.AbsoluteRefreshTokenLifetime,
        SlidingRefreshTokenLifetime = client.SlidingRefreshTokenLifetime,
        RefreshTokenUsage = (int)client.RefreshTokenUsage,
        RefreshTokenExpiration = (int)client.RefreshTokenExpiration,
        UpdateAccessTokenClaimsOnRefresh = client.UpdateAccessTokenClaimsOnRefresh,
        AlwaysIncludeUserClaimsInIdToken = client.AlwaysIncludeUserClaimsInIdToken,
        AlwaysSendClientClaims = client.AlwaysSendClientClaims,
        ClientClaimsPrefix = client.ClientClaimsPrefix,
        PairWiseSubjectSalt = client.PairWiseSubjectSalt,
        UserSsoLifetime = client.UserSsoLifetime,
        CoordinateLifetimeWithUserSession = client.CoordinateLifetimeWithUserSession,
        EnableLocalLogin = client.EnableLocalLogin,
        FrontChannelLogoutUri = client.FrontChannelLogoutUri,
        FrontChannelLogoutSessionRequired = client.FrontChannelLogoutSessionRequired,
        BackChannelLogoutUri = client.BackChannelLogoutUri,
        BackChannelLogoutSessionRequired = client.BackChannelLogoutSessionRequired,
        InitiateLoginUri = client.InitiateLoginUri,
        RequirePushedAuthorization = client.RequirePushedAuthorization,
        PushedAuthorizationLifetime = client.PushedAuthorizationLifetime,
        UserCodeType = client.UserCodeType,
        DeviceCodeLifetime = client.DeviceCodeLifetime,
        CibaLifetime = client.CibaLifetime,
        PollingInterval = client.PollingInterval,
        AllowedGrantTypes = client.AllowedGrantTypes.ToList(),
        AllowedScopes = client.AllowedScopes.ToList(),
        RedirectUris = client.RedirectUris.ToList(),
        PostLogoutRedirectUris = client.PostLogoutRedirectUris.ToList(),
        AllowedIdentityTokenSigningAlgorithms = client.AllowedIdentityTokenSigningAlgorithms.ToList(),
        IdentityProviderRestrictions = client.IdentityProviderRestrictions.ToList(),
        AllowedCorsOrigins = client.AllowedCorsOrigins.ToList(),
        ClientSecrets = client.ClientSecrets.Select(s => new ClientDso.SecretDso(
            Guid.NewGuid(), s.Value ?? "", s.Description, s.Expiration, s.Type ?? "SharedSecret", "Sha256")).ToList(),
        Claims = client.Claims.Select(c => new ClientDso.ClaimDso(c.Type, c.Value, c.ValueType)).ToList(),
        ExtendedAttributeValues = client.Properties.Count > 0
            ? client.Properties.Select(p => new AttributeValueDso.V1(p.Key, p.Value)).ToList()
            : null
    };
}
