// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.Clients;
using Duende.IdentityServer.Validation;
using Duende.Storage;
using Duende.IdentityServer.DataExtensions.Internal.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public class StorageClientStoreContractTests : ClientStoreContractTests
{
    private readonly string _dbName = $"StorageClientContract_{Guid.NewGuid():N}";
    private ServiceProvider _provider = null!;
    private readonly List<ServiceProvider> _isolatedProviders = [];

    public override async ValueTask InitializeAsync() => _provider = await StorageContractTestServices.CreateAsync(_dbName, b => b.AddConfigurationStorage().AddClientConfigurationValidator<NopClientConfigurationValidator>());

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
        var provider = await StorageContractTestServices.CreateAsync(isolatedDbName, b => b.AddConfigurationStorage().AddClientConfigurationValidator<NopClientConfigurationValidator>());
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
        var id = UuidV7.New();
        var dso = MapToDso(client, id.Value);
        var scopes = await ResolveScopeLinksAsync(client.AllowedScopes, TestContext.Current.CancellationToken);
        await repo.CreateAsync(id, dso, scopes, TestContext.Current.CancellationToken);
    }

    // Contract fixtures name scopes that don't need to exist for IClientStore's own behavior (it reads
    // stored data, not admin-validated input), but allowed scopes are now Storage links, so each name must
    // resolve to an API scope entity. Creates one per unique name, reusing it across seed calls.
    private async Task<List<ScopeLink>> ResolveScopeLinksAsync(ICollection<string> scopeNames, Ct ct)
    {
        var apiScopeRepository = _provider.GetRequiredService<Duende.IdentityServer.Stores.Storage.ApiScopes.ApiScopeRepository>();
        var links = new List<ScopeLink>(scopeNames.Count);

        foreach (var name in scopeNames.Distinct(StringComparer.Ordinal))
        {
            var existing = await apiScopeRepository.TryReadByNameAsync(name, ct);
            Guid scopeId;
            if (existing is not null)
            {
                scopeId = existing.Value.Dso.Id;
            }
            else
            {
                var id = UuidV7.New();
                scopeId = id.Value;
                await apiScopeRepository.CreateAsync(id, new Duende.IdentityServer.Stores.Storage.ApiScopeDso.V1
                {
                    Id = scopeId,
                    Name = name,
                    Enabled = true,
                    ShowInDiscoveryDocument = true,
                    Required = false,
                    Emphasize = false,
                    UserClaims = []
                }, ct);
            }

            links.Add(new ScopeLink(name, scopeId, ScopeLinkKind.ApiScope));
        }

        return links;
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
    private static ClientDso.V1 MapToDso(Client client, Guid id) => new()
    {
        Id = id,
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
