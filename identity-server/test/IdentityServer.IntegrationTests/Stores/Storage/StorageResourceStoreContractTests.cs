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
using Duende.IdentityServer.Stores.Storage.ApiScopes;
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

    // Seeded through the repository rather than IApiResourceAdmin because the admin hashes secret
    // values, and the secrets on the given ApiResource are already hashed and must be stored as they are.
    protected override async Task SeedApiResourceAsync(ApiResource resource)
    {
        var ct = TestContext.Current.CancellationToken;
        var scopes = new List<(string Name, Guid Id)>();
        foreach (var scopeName in resource.Scopes)
        {
            scopes.Add((scopeName, await FindOrCreateApiScopeAsync(scopeName, ct)));
        }

        var id = UuidV7.New();
        var dso = new ApiResourceDso.V1
        {
            Id = id.Value,
            Name = resource.Name,
            Enabled = resource.Enabled,
            DisplayName = resource.DisplayName,
            Description = resource.Description,
            ShowInDiscoveryDocument = resource.ShowInDiscoveryDocument,
            RequireResourceIndicator = resource.RequireResourceIndicator,
            UserClaims = resource.UserClaims.ToList(),
            AllowedAccessTokenSigningAlgorithms = resource.AllowedAccessTokenSigningAlgorithms.ToList(),
            ApiSecrets = resource.ApiSecrets.Select(s => new ApiResourceDso.SecretDso(
                Guid.NewGuid(), s.Value ?? "", s.Description, s.Expiration, s.Type ?? "SharedSecret", "Sha256")).ToList()
        };
        var result = await _provider.GetRequiredService<ApiResourceRepository>().CreateAsync(id, dso, scopes, ct);
        result.Kind.ShouldBe(ApiResourceBatchOutcomeKind.Success);
    }

    private async Task<Guid> FindOrCreateApiScopeAsync(string name, Ct ct)
    {
        var existing = await _provider.GetRequiredService<ApiScopeRepository>().TryReadByNameAsync(name, ct);
        if (existing is not null)
        {
            return existing.Value.Dso.Id;
        }

        var result = await _provider.GetRequiredService<IApiScopeAdmin>().CreateAsync(new CreateApiScope { Name = name }, ct);
        result.IsSuccess.ShouldBeTrue($"Seeding API scope '{name}' failed: {result}");
        return result.Id!.Value;
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
