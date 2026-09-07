// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.IntegrationTests.Stores.InMemory;

/// <summary>
/// InMemory contract tests for IResourceStore.
/// Note: xUnit v3 creates a new instance per test method, so the backing lists
/// start empty for each test — providing natural test isolation.
/// </summary>
public class InMemoryResourceStoreContractTests : ResourceStoreContractTests
{
    private readonly List<IdentityResource> _identityResources = [];
    private readonly List<ApiResource> _apiResources = [];
    private readonly List<ApiScope> _apiScopes = [];

    protected override StoreHandle<IResourceStore> CreateStore() =>
        new(new InMemoryResourcesStore(_identityResources, _apiResources, _apiScopes));

    protected override Task SeedIdentityResourceAsync(IdentityResource resource)
    {
        _identityResources.Add(resource);
        return Task.CompletedTask;
    }

    protected override Task SeedApiResourceAsync(ApiResource resource)
    {
        _apiResources.Add(resource);
        return Task.CompletedTask;
    }

    protected override Task SeedApiScopeAsync(ApiScope scope)
    {
        _apiScopes.Add(scope);
        return Task.CompletedTask;
    }
}
