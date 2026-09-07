// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.ApiResources;
using Duende.IdentityServer.Stores.Storage.ApiScopes;
using Duende.IdentityServer.Stores.Storage.IdentityResources;
using Duende.IdentityServer.Stores.Storage.Resources;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public class StorageResourceStoreContractTests : ResourceStoreContractTests
{
    private readonly string _dbName = $"StorageResourceContract_{Guid.NewGuid():N}";
    private ServiceProvider _provider = null!;

    public override async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={_dbName};Mode=Memory;Cache=Shared"));

        services.AddDsoRegistration<ApiResourceDso.V1>();
        services.AddDsoRegistration<ApiScopeDso.V1>();
        services.AddDsoRegistration<IdentityResourceDso.V1>();
        services.AddSingleton<IPoolContextAccessor, PoolContextAccessor>();
        services.AddSingleton<IStorageFactory, DefaultStorageFactory>();
        services.AddScoped<ApiResourceRepository>();
        services.AddScoped<ApiScopeRepository>();
        services.AddScoped<IdentityResourceRepository>();
        services.AddScoped<IResourceStore, StorageResourceStore>();

        _provider = services.BuildServiceProvider();

        var schema = _provider.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override StoreHandle<IResourceStore> CreateStore()
    {
        var scope = _provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IResourceStore>();
        return StoreHandle.WithDisposable(store, scope);
    }

    protected override async Task SeedIdentityResourceAsync(IdentityResource resource)
    {
        using var scope = _provider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IdentityResourceRepository>();
        var dso = new IdentityResourceDso.V1
        {
            // dso.Id is an internal identifier within the DSO payload; the UuidV7 passed to
            // CreateAsync is the storage-layer primary key used for addressing the record.
            Id = Guid.NewGuid(),
            Name = resource.Name,
            Enabled = resource.Enabled,
            DisplayName = resource.DisplayName,
            Description = resource.Description,
            ShowInDiscoveryDocument = resource.ShowInDiscoveryDocument,
            Required = resource.Required,
            Emphasize = resource.Emphasize,
            UserClaims = resource.UserClaims.ToList(),
            ExtendedAttributeValues = resource.Properties
                .Select(kvp => new AttributeValueDso.V1(kvp.Key, kvp.Value))
                .ToList()
        };
        await repo.CreateAsync(UuidV7.New(), dso, TestContext.Current.CancellationToken);
    }

    protected override async Task SeedApiResourceAsync(ApiResource resource)
    {
        using var scope = _provider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ApiResourceRepository>();

        // Build scope references — StorageResourceStore.FindApiResourcesByScopeNameAsync queries via
        // ApiResourceRepository.FindByScopeNamesAsync which matches on the scope Name search field
        // (not the reference ID). We use random Guids for scope reference IDs since only the name matters.
        var scopeRefs = resource.Scopes.Select(s => new ApiScopeReferenceDso.V1(Guid.NewGuid(), s)).ToList();

        var dso = new ApiResourceDso.V1
        {
            Id = Guid.NewGuid(),
            Name = resource.Name,
            Enabled = resource.Enabled,
            DisplayName = resource.DisplayName,
            Description = resource.Description,
            ShowInDiscoveryDocument = resource.ShowInDiscoveryDocument,
            RequireResourceIndicator = resource.RequireResourceIndicator,
            UserClaims = resource.UserClaims.ToList(),
            Scopes = scopeRefs,
            AllowedAccessTokenSigningAlgorithms = resource.AllowedAccessTokenSigningAlgorithms.ToList(),
            ApiSecrets = resource.ApiSecrets.Select(s => new ApiResourceDso.SecretDso(
                Guid.NewGuid(), s.Value ?? "", s.Description, s.Expiration, s.Type ?? "SharedSecret", "Sha256")).ToList()
        };
        await repo.CreateAsync(UuidV7.New(), dso, TestContext.Current.CancellationToken);
    }

    protected override async Task SeedApiScopeAsync(ApiScope scope)
    {
        using var scopeProvider = _provider.CreateScope();
        var repo = scopeProvider.ServiceProvider.GetRequiredService<ApiScopeRepository>();
        var dso = new ApiScopeDso.V1
        {
            Id = Guid.NewGuid(),
            Name = scope.Name,
            Enabled = scope.Enabled,
            DisplayName = scope.DisplayName,
            Description = scope.Description,
            ShowInDiscoveryDocument = scope.ShowInDiscoveryDocument,
            Required = scope.Required,
            Emphasize = scope.Emphasize,
            UserClaims = scope.UserClaims.ToList()
        };
        await repo.CreateAsync(UuidV7.New(), dso, TestContext.Current.CancellationToken);
    }
}
