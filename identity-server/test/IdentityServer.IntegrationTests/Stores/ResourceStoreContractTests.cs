// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.IntegrationTests.Stores;

/// <summary>
/// Abstract contract tests for IResourceStore implementations.
/// Each derived class provides a specific store backend (EF, Storage, InMemory).
/// </summary>
public abstract class ResourceStoreContractTests : IAsyncLifetime
{
    protected readonly Ct _ct = TestContext.Current.CancellationToken;

    /// <summary>
    /// Creates a store instance that operates against the shared test database.
    /// Callers must dispose the returned <see cref="StoreHandle{T}"/> after use.
    /// </summary>
    protected abstract StoreHandle<IResourceStore> CreateStore();

    /// <summary>
    /// Seeds a single identity resource into the backing store.
    /// </summary>
    protected abstract Task SeedIdentityResourceAsync(IdentityResource resource);

    /// <summary>
    /// Seeds a single API resource into the backing store.
    /// </summary>
    protected abstract Task SeedApiResourceAsync(ApiResource resource);

    /// <summary>
    /// Seeds a single API scope into the backing store.
    /// </summary>
    protected abstract Task SeedApiScopeAsync(ApiScope scope);

    /// <summary>
    /// Lifecycle hook for derived classes that need async initialization.
    /// </summary>
    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Lifecycle hook for derived classes that need async cleanup.
    /// </summary>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected static IdentityResource CreateIdentityTestResource() => new()
    {
        Name = Guid.NewGuid().ToString(),
        DisplayName = Guid.NewGuid().ToString(),
        Description = Guid.NewGuid().ToString(),
        ShowInDiscoveryDocument = true,
        UserClaims =
        {
            JwtClaimTypes.Subject,
            JwtClaimTypes.Name,
        }
    };

    protected static ApiResource CreateApiResourceTestResource() => new()
    {
        Name = Guid.NewGuid().ToString(),
        ApiSecrets = new List<Secret> { new("secret".ToSha256()) },
        Scopes = { Guid.NewGuid().ToString() },
        UserClaims =
        {
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
        }
    };

    protected static ApiScope CreateApiScopeTestResource() => new()
    {
        Name = Guid.NewGuid().ToString(),
        UserClaims =
        {
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
        }
    };

    [Fact]
    public async Task FindApiResourcesByNameAsync_WhenResourceExists_ExpectResourceAndCollectionsReturned()
    {
        var resource = CreateApiResourceTestResource();

        await SeedApiResourceAsync(resource);

        await using var handle = CreateStore();
        var foundResource = (await handle.Store.FindApiResourcesByNameAsync(new[] { resource.Name }, _ct)).SingleOrDefault();

        foundResource.ShouldNotBeNull();
        foundResource.Name.ShouldBe(resource.Name);
        foundResource.UserClaims.ShouldNotBeNull();
        foundResource.UserClaims.ShouldNotBeEmpty();
        foundResource.ApiSecrets.ShouldNotBeNull();
        foundResource.ApiSecrets.ShouldNotBeEmpty();
        foundResource.Scopes.ShouldNotBeNull();
        foundResource.Scopes.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task FindApiResourcesByNameAsync_WhenResourcesExist_ExpectOnlyResourcesRequestedReturned()
    {
        var resource = CreateApiResourceTestResource();

        await SeedApiResourceAsync(resource);
        await SeedApiResourceAsync(CreateApiResourceTestResource());

        await using var handle = CreateStore();
        var foundResource = (await handle.Store.FindApiResourcesByNameAsync(new[] { resource.Name }, _ct)).SingleOrDefault();

        foundResource.ShouldNotBeNull();
        foundResource.Name.ShouldBe(resource.Name);
        foundResource.UserClaims.ShouldNotBeNull();
        foundResource.UserClaims.ShouldNotBeEmpty();
        foundResource.ApiSecrets.ShouldNotBeNull();
        foundResource.ApiSecrets.ShouldNotBeEmpty();
        foundResource.Scopes.ShouldNotBeNull();
        foundResource.Scopes.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task FindApiResourcesByScopeNameAsync_WhenResourcesExist_ExpectResourcesReturned()
    {
        var testApiResource = CreateApiResourceTestResource();
        var testApiScope = CreateApiScopeTestResource();
        testApiResource.Scopes.Add(testApiScope.Name);

        await SeedApiScopeAsync(testApiScope);
        await SeedApiResourceAsync(testApiResource);

        await using var handle = CreateStore();
        var resources = await handle.Store.FindApiResourcesByScopeNameAsync(new List<string>
        {
            testApiScope.Name
        }, _ct);

        resources.ShouldNotBeNull();
        resources.ShouldNotBeEmpty();
        resources.SingleOrDefault(x => x.Name == testApiResource.Name).ShouldNotBeNull();
    }

    [Fact]
    public async Task FindApiResourcesByScopeNameAsync_WhenResourcesExist_ExpectOnlyResourcesRequestedReturned()
    {
        var testIdentityResource = CreateIdentityTestResource();
        var testApiResource = CreateApiResourceTestResource();
        var testApiScope = CreateApiScopeTestResource();
        testApiResource.Scopes.Add(testApiScope.Name);

        await SeedIdentityResourceAsync(testIdentityResource);
        await SeedApiScopeAsync(testApiScope);
        await SeedApiResourceAsync(testApiResource);
        await SeedIdentityResourceAsync(CreateIdentityTestResource());
        await SeedApiScopeAsync(CreateApiScopeTestResource());
        await SeedApiResourceAsync(CreateApiResourceTestResource());

        await using var handle = CreateStore();
        var resources = await handle.Store.FindApiResourcesByScopeNameAsync(new[] { testApiScope.Name }, _ct);

        resources.ShouldNotBeNull();
        resources.ShouldNotBeEmpty();
        resources.SingleOrDefault(x => x.Name == testApiResource.Name).ShouldNotBeNull();
    }

    [Fact]
    public async Task FindIdentityResourcesByScopeNameAsync_WhenResourceExists_ExpectResourceAndCollectionsReturned()
    {
        var resource = CreateIdentityTestResource();

        await SeedIdentityResourceAsync(resource);

        await using var handle = CreateStore();
        var resources = (await handle.Store.FindIdentityResourcesByScopeNameAsync(new List<string>
        {
            resource.Name
        }, _ct)).ToList();

        resources.ShouldNotBeNull();
        resources.ShouldNotBeEmpty();
        var foundScope = resources.Single();

        foundScope.Name.ShouldBe(resource.Name);
        foundScope.UserClaims.ShouldNotBeNull();
        foundScope.UserClaims.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task FindIdentityResourcesByScopeNameAsync_WhenResourcesExist_ExpectOnlyRequestedReturned()
    {
        var resource = CreateIdentityTestResource();

        await SeedIdentityResourceAsync(resource);
        await SeedIdentityResourceAsync(CreateIdentityTestResource());

        await using var handle = CreateStore();
        var resources = (await handle.Store.FindIdentityResourcesByScopeNameAsync(new List<string>
        {
            resource.Name
        }, _ct)).ToList();

        resources.ShouldNotBeNull();
        resources.ShouldNotBeEmpty();
        resources.SingleOrDefault(x => x.Name == resource.Name).ShouldNotBeNull();
    }

    [Fact]
    public async Task FindApiScopesByNameAsync_WhenResourceExists_ExpectResourceAndCollectionsReturned()
    {
        var resource = CreateApiScopeTestResource();

        await SeedApiScopeAsync(resource);

        await using var handle = CreateStore();
        var resources = (await handle.Store.FindApiScopesByNameAsync(new List<string>
        {
            resource.Name
        }, _ct)).ToList();

        resources.ShouldNotBeNull();
        resources.ShouldNotBeEmpty();
        var foundScope = resources.Single();

        foundScope.Name.ShouldBe(resource.Name);
        foundScope.UserClaims.ShouldNotBeNull();
        foundScope.UserClaims.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task FindApiScopesByNameAsync_WhenResourcesExist_ExpectOnlyRequestedReturned()
    {
        var resource = CreateApiScopeTestResource();

        await SeedApiScopeAsync(resource);
        await SeedApiScopeAsync(CreateApiScopeTestResource());

        await using var handle = CreateStore();
        var resources = (await handle.Store.FindApiScopesByNameAsync(new List<string>
        {
            resource.Name
        }, _ct)).ToList();

        resources.ShouldNotBeNull();
        resources.ShouldNotBeEmpty();
        resources.SingleOrDefault(x => x.Name == resource.Name).ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAllResources_WhenAllResourcesRequested_ExpectAllResourcesIncludingHidden()
    {
        var visibleIdentityResource = CreateIdentityTestResource();
        var visibleApiResource = CreateApiResourceTestResource();
        var visibleApiScope = CreateApiScopeTestResource();
        var hiddenIdentityResource = new IdentityResource { Name = Guid.NewGuid().ToString(), ShowInDiscoveryDocument = false };
        var hiddenApiResource = new ApiResource
        {
            Name = Guid.NewGuid().ToString(),
            Scopes = { Guid.NewGuid().ToString() },
            ShowInDiscoveryDocument = false
        };
        var hiddenApiScope = new ApiScope
        {
            Name = Guid.NewGuid().ToString(),
            ShowInDiscoveryDocument = false
        };

        await SeedIdentityResourceAsync(visibleIdentityResource);
        await SeedApiResourceAsync(visibleApiResource);
        await SeedApiScopeAsync(visibleApiScope);
        await SeedIdentityResourceAsync(hiddenIdentityResource);
        await SeedApiResourceAsync(hiddenApiResource);
        await SeedApiScopeAsync(hiddenApiScope);

        await using var handle = CreateStore();
        var resources = await handle.Store.GetAllResourcesAsync(_ct);

        resources.ShouldNotBeNull();
        resources.IdentityResources.ShouldNotBeEmpty();
        resources.ApiResources.ShouldNotBeEmpty();
        resources.ApiScopes.ShouldNotBeEmpty();

        resources.IdentityResources.ShouldContain(x => x.Name == visibleIdentityResource.Name);
        resources.IdentityResources.ShouldContain(x => x.Name == hiddenIdentityResource.Name);

        resources.ApiResources.ShouldContain(x => x.Name == visibleApiResource.Name);
        resources.ApiResources.ShouldContain(x => x.Name == hiddenApiResource.Name);

        resources.ApiScopes.ShouldContain(x => x.Name == visibleApiScope.Name);
        resources.ApiScopes.ShouldContain(x => x.Name == hiddenApiScope.Name);
    }
}
