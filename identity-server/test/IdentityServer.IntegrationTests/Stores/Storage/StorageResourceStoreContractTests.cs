// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.IdentityServer.Admin.IdentityResources;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.ApiResources;
using Duende.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public class StorageResourceStoreContractTests : ResourceStoreContractTests
{
    private readonly string _dbName = $"StorageResourceContract_{Guid.NewGuid():N}";
    private ServiceProvider _provider = null!;

    public override async ValueTask InitializeAsync() => _provider = await StorageContractTestServices.CreateAsync(_dbName, b => b.AddConfigurationStorage());

    public override async ValueTask DisposeAsync()
    {
        try
        {
            DataCategoryNameRecorder.CurrentTestShouldHaveUsed([_provider], DataCategoryName.Configuration);
        }
        finally
        {
            await _provider.DisposeAsync();
            await base.DisposeAsync();
        }
    }

    protected override StoreHandle<IResourceStore> CreateStore() => new StoreHandle<IResourceStore>(_provider.GetRequiredService<IResourceStore>());

    protected override async Task SeedIdentityResourceAsync(IdentityResource resource)
    {
        var admin = _provider.GetRequiredService<IIdentityResourceAdmin>();
        var create = new CreateIdentityResource
        {
            Name = resource.Name,
            Enabled = resource.Enabled,
            DisplayName = resource.DisplayName,
            Description = resource.Description,
            ShowInDiscoveryDocument = resource.ShowInDiscoveryDocument,
            Required = resource.Required,
            Emphasize = resource.Emphasize,
            UserClaims = resource.UserClaims.ToList()
        };

        var result = await admin.CreateAsync(create, TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue($"Seeding identity resource '{resource.Name}' failed: {result}");
    }

    // NOTE: ApiResource seeding cannot go through IApiResourceAdmin: ApiResourceAdmin.CreateAsync
    // resolves every Scopes entry against ApiScopeRepository and rejects scope names that don't
    // already exist as a seeded ApiScope ("Scope '{name}' does not exist."). Most base
    // ResourceStoreContractTests cases build an ApiResource via CreateApiResourceTestResource(),
    // which assigns a random scope name without ever seeding a matching ApiScope first, so the
    // admin API cannot express this seed data. Kept on the internal repository.
    protected override async Task SeedApiResourceAsync(ApiResource resource)
    {
        var repo = _provider.GetRequiredService<ApiResourceRepository>();

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
        var admin = _provider.GetRequiredService<IApiScopeAdmin>();
        var create = new CreateApiScope
        {
            Name = scope.Name,
            Enabled = scope.Enabled,
            DisplayName = scope.DisplayName,
            Description = scope.Description,
            ShowInDiscoveryDocument = scope.ShowInDiscoveryDocument,
            Required = scope.Required,
            Emphasize = scope.Emphasize,
            UserClaims = scope.UserClaims.ToList()
        };

        var result = await admin.CreateAsync(create, TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue($"Seeding API scope '{scope.Name}' failed: {result}");
    }
}
