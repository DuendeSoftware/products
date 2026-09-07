// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.SamlServiceProviders;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public class StorageSamlServiceProviderStoreContractTests : SamlServiceProviderStoreContractTests
{
    private readonly string _dbName = $"StorageSamlServiceProviderContract_{Guid.NewGuid():N}";
    private ServiceProvider _provider = null!;
    private readonly List<ServiceProvider> _isolatedProviders = [];

    public override async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={_dbName};Mode=Memory;Cache=Shared"));

        services.AddDsoRegistration<SamlServiceProviderDso.V1>();
        services.AddScoped<SamlServiceProviderRepository>();
        services.AddScoped<ISamlServiceProviderStore, SamlServiceProviderStore>();

        _provider = services.BuildServiceProvider();

        var schema = _provider.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(_ct);
    }

    public override async ValueTask DisposeAsync()
    {
        foreach (var provider in _isolatedProviders)
        {
            await provider.DisposeAsync();
        }
        await _provider.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override StoreHandle<ISamlServiceProviderStore> CreateStore()
    {
        var scope = _provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ISamlServiceProviderStore>();
        return StoreHandle.WithDisposable(store, scope);
    }

    protected override async Task<StoreHandle<ISamlServiceProviderStore>> CreateIsolatedStoreAsync()
    {
        var isolatedDbName = $"StorageSamlServiceProviderContractIsolated_{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={isolatedDbName};Mode=Memory;Cache=Shared"));

        services.AddDsoRegistration<SamlServiceProviderDso.V1>();
        services.AddScoped<SamlServiceProviderRepository>();
        services.AddScoped<ISamlServiceProviderStore, SamlServiceProviderStore>();

        var provider = services.BuildServiceProvider();
        _isolatedProviders.Add(provider);

        var schema = provider.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(_ct);

        var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ISamlServiceProviderStore>();
        return StoreHandle.WithDisposable(store, scope);
    }

    protected override async Task SeedSamlServiceProviderAsync(SamlServiceProvider sp)
    {
        await using var scope = _provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<SamlServiceProviderRepository>();

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
            AllowedScopes = (sp.AllowedScopes ?? []).ToList(),
            ClaimMappings = new Dictionary<string, string>(sp.ClaimMappings),
            AuthnContextMappings = new Dictionary<string, string>(sp.AuthnContextMappings ?? new Dictionary<string, string>()),
            RequestedClaimTypes = sp.RequestedClaimTypes ?? [],
            DefaultNameIdFormat = sp.DefaultNameIdFormat,
            EmailNameIdClaimType = sp.EmailNameIdClaimType,
            SigningBehavior = sp.SigningBehavior.HasValue ? (int)sp.SigningBehavior.Value : null,
            AllowedSignatureAlgorithms = sp.AllowedSignatureAlgorithms ?? [],
        };

        await repository.CreateAsync(id, dso, _ct);
    }
}
