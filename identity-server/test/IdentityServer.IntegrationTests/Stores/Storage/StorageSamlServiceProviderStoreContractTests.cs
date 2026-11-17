// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.SamlServiceProviders;
using Duende.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public class StorageSamlServiceProviderStoreContractTests : SamlServiceProviderStoreContractTests
{
    private readonly string _dbName = $"StorageSamlServiceProviderContract_{Guid.NewGuid():N}";
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

    protected override StoreHandle<ISamlServiceProviderStore> CreateStore() => new StoreHandle<ISamlServiceProviderStore>(_provider.GetRequiredService<ISamlServiceProviderStore>());

    protected override async Task<StoreHandle<ISamlServiceProviderStore>> CreateIsolatedStoreAsync()
    {
        var isolatedDbName = $"StorageSamlServiceProviderContractIsolated_{Guid.NewGuid():N}";
        var provider = await StorageContractTestServices.CreateAsync(isolatedDbName, b => b.AddConfigurationStorage());
        _isolatedProviders.Add(provider);

        return new StoreHandle<ISamlServiceProviderStore>(provider.GetRequiredService<ISamlServiceProviderStore>());
    }

    // NOTE: Seeds through SamlServiceProviderRepository rather than ISamlServiceProviderAdmin on purpose.
    // The admin requires an HTTP-POST assertion consumer service URL and at least one allowed scope, which
    // the contract fixtures don't set. These tests check what the store does with stored data, not admin validation.
    protected override async Task SeedSamlServiceProviderAsync(SamlServiceProvider sp)
    {
        var repository = _provider.GetRequiredService<SamlServiceProviderRepository>();

        var id = UuidV7.New();
        var dso = new SamlServiceProviderDso.V1
        {
            Id = id.Value,
            EntityId = sp.EntityId,
            Enabled = sp.Enabled,
            DisplayName = sp.DisplayName,
            Description = sp.Description,
            ClockSkewTicks = sp.ClockSkew?.Ticks,
            RequestMaxAgeTicks = sp.RequestMaxAge?.Ticks,
            AssertionLifetimeTicks = sp.AssertionLifetime?.Ticks,
            AssertionConsumerServiceUrls = (sp.AssertionConsumerServiceUrls ?? [])
                .Select(a => new SamlServiceProviderDso.IndexedEndpointDso(a.Location, (int)a.Binding, a.Index, a.IsDefault))
                .ToList(),
            SingleLogoutServiceUrls = (sp.SingleLogoutServiceUrls ?? [])
                .Select(s => new SamlServiceProviderDso.EndpointDso(s.Location, (int)s.Binding))
                .ToList(),
            RequireSignedAuthnRequests = sp.RequireSignedAuthnRequests,
            RequireSignedLogoutResponses = sp.RequireSignedLogoutResponses,
            Certificates = (sp.Certificates ?? [])
                .Select(c => new SamlServiceProviderDso.CertificateDso(Guid.NewGuid(), Convert.ToBase64String(c.Certificate.RawData), (int)c.Use))
                .ToList(),
            AllowIdpInitiated = sp.AllowIdpInitiated,
            ClaimMappings = new Dictionary<string, string>(sp.ClaimMappings),
            AuthnContextMappings = new Dictionary<string, string>(sp.AuthnContextMappings ?? new Dictionary<string, string>()),
            RequestedClaimTypes = sp.RequestedClaimTypes ?? [],
            DefaultNameIdFormat = sp.DefaultNameIdFormat,
            EmailNameIdClaimType = sp.EmailNameIdClaimType,
            SigningBehavior = sp.SigningBehavior.HasValue ? (int)sp.SigningBehavior.Value : null,
            AllowedSignatureAlgorithms = sp.AllowedSignatureAlgorithms ?? [],
        };

        var scopes = await ResolveScopeLinksAsync(sp.AllowedScopes, _ct);
        await repository.CreateAsync(id, dso, scopes, _ct);
    }

    // Contract fixtures name scopes that don't need to exist for ISamlServiceProviderStore's own behavior
    // (it reads stored data, not admin-validated input), but allowed scopes are now Storage links to
    // identity resources, so each name must resolve to an identity resource entity. Creates one per unique
    // name, reusing it across seed calls.
    private async Task<List<SamlAllowedScopeLink>> ResolveScopeLinksAsync(ICollection<string> scopeNames, Ct ct)
    {
        var identityResourceRepository = _provider.GetRequiredService<Duende.IdentityServer.Stores.Storage.IdentityResources.IdentityResourceRepository>();
        var links = new List<SamlAllowedScopeLink>(scopeNames.Count);

        foreach (var name in scopeNames.Distinct(StringComparer.Ordinal))
        {
            var existing = await identityResourceRepository.TryReadByNameAsync(name, ct);
            Guid resourceId;
            if (existing is not null)
            {
                resourceId = existing.Value.Dso.Id;
            }
            else
            {
                var id = UuidV7.New();
                resourceId = id.Value;
                await identityResourceRepository.CreateAsync(id, new Duende.IdentityServer.Stores.Storage.IdentityResourceDso.V1
                {
                    Id = resourceId,
                    Name = name,
                    Enabled = true,
                    ShowInDiscoveryDocument = true,
                    Required = false,
                    Emphasize = false,
                    UserClaims = []
                }, ct);
            }

            links.Add(new SamlAllowedScopeLink(name, resourceId));
        }

        return links;
    }
}
