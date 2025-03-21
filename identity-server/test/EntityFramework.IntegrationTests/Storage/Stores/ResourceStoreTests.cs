// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.EntityFrameworkCore;

namespace EntityFramework.IntegrationTests.Storage.Stores;

public class ScopeStoreTests : IntegrationTest<ScopeStoreTests, ConfigurationDbContext, ConfigurationStoreOptions>
{
    public ScopeStoreTests(DatabaseProviderFixture<ConfigurationDbContext> fixture) : base(fixture)
    {
        foreach (var options in TestDatabaseProviders)
        {
            using var context = new ConfigurationDbContext(options);
            context.Database.EnsureCreated();
        }
    }

    private static IdentityResource CreateIdentityTestResource()
    {
        return new IdentityResource()
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
    }

    private static ApiResource CreateApiResourceTestResource()
    {
        return new ApiResource()
        {
            Name = Guid.NewGuid().ToString(),
            ApiSecrets = new List<Secret> { new Secret("secret".ToSha256()) },
            Scopes = { Guid.NewGuid().ToString() },
            UserClaims =
            {
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
            }
        };
    }

    private static ApiScope CreateApiScopeTestResource()
    {
        return new ApiScope()
        {
            Name = Guid.NewGuid().ToString(),
            UserClaims =
            {
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
            }
        };
    }


    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindApiResourcesByNameAsync_WhenResourceExists_ExpectResourceAndCollectionsReturned(DbContextOptions<ConfigurationDbContext> options)
    {
        var resource = CreateApiResourceTestResource();

        await using (var context = new ConfigurationDbContext(options))
        {
            context.ApiResources.Add(resource.ToEntity());
            await context.SaveChangesAsync();
        }

        ApiResource foundResource;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ResourceStore(context, FakeLogger<ResourceStore>.Create(), new NoneCancellationTokenProvider());
            foundResource = (await store.FindApiResourcesByNameAsync(new[] { resource.Name })).SingleOrDefault();
        }

        foundResource.ShouldNotBeNull();
        foundResource.Name.ShouldBe(resource.Name);
        foundResource.UserClaims.ShouldNotBeNull();
        foundResource.UserClaims.ShouldNotBeEmpty();
        foundResource.ApiSecrets.ShouldNotBeNull();
        foundResource.ApiSecrets.ShouldNotBeEmpty();
        foundResource.Scopes.ShouldNotBeNull();
        foundResource.Scopes.ShouldNotBeEmpty();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindApiResourcesByNameAsync_WhenResourcesExist_ExpectOnlyResourcesRequestedReturned(DbContextOptions<ConfigurationDbContext> options)
    {
        var resource = CreateApiResourceTestResource();

        await using (var context = new ConfigurationDbContext(options))
        {
            context.ApiResources.Add(resource.ToEntity());
            context.ApiResources.Add(CreateApiResourceTestResource().ToEntity());
            await context.SaveChangesAsync();
        }

        ApiResource foundResource;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ResourceStore(context, FakeLogger<ResourceStore>.Create(), new NoneCancellationTokenProvider());
            foundResource = (await store.FindApiResourcesByNameAsync(new[] { resource.Name })).SingleOrDefault();
        }

        foundResource.ShouldNotBeNull();
        foundResource.Name.ShouldBe(resource.Name);

        foundResource.UserClaims.ShouldNotBeNull();
        foundResource.UserClaims.ShouldNotBeEmpty();
        foundResource.ApiSecrets.ShouldNotBeNull();
        foundResource.ApiSecrets.ShouldNotBeEmpty();
        foundResource.Scopes.ShouldNotBeNull();
        foundResource.Scopes.ShouldNotBeEmpty();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindApiResourcesByScopeNameAsync_WhenResourcesExist_ExpectResourcesReturned(DbContextOptions<ConfigurationDbContext> options)
    {
        var testApiResource = CreateApiResourceTestResource();
        var testApiScope = CreateApiScopeTestResource();
        testApiResource.Scopes.Add(testApiScope.Name);

        await using (var context = new ConfigurationDbContext(options))
        {
            context.ApiResources.Add(testApiResource.ToEntity());
            context.ApiScopes.Add(testApiScope.ToEntity());
            await context.SaveChangesAsync();
        }

        IEnumerable<ApiResource> resources;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ResourceStore(context, FakeLogger<ResourceStore>.Create(), new NoneCancellationTokenProvider());
            resources = await store.FindApiResourcesByScopeNameAsync(new List<string>
            {
                testApiScope.Name
            });
        }

        resources.ShouldNotBeNull();
        resources.ShouldNotBeEmpty();
        resources.SingleOrDefault(x => x.Name == testApiResource.Name).ShouldNotBeNull();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindApiResourcesByScopeNameAsync_WhenResourcesExist_ExpectOnlyResourcesRequestedReturned(DbContextOptions<ConfigurationDbContext> options)
    {
        var testIdentityResource = CreateIdentityTestResource();
        var testApiResource = CreateApiResourceTestResource();
        var testApiScope = CreateApiScopeTestResource();
        testApiResource.Scopes.Add(testApiScope.Name);

        await using (var context = new ConfigurationDbContext(options))
        {
            context.IdentityResources.Add(testIdentityResource.ToEntity());
            context.ApiResources.Add(testApiResource.ToEntity());
            context.ApiScopes.Add(testApiScope.ToEntity());
            context.IdentityResources.Add(CreateIdentityTestResource().ToEntity());
            context.ApiResources.Add(CreateApiResourceTestResource().ToEntity());
            context.ApiScopes.Add(CreateApiScopeTestResource().ToEntity());
            await context.SaveChangesAsync();
        }

        IEnumerable<ApiResource> resources;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ResourceStore(context, FakeLogger<ResourceStore>.Create(), new NoneCancellationTokenProvider());
            resources = await store.FindApiResourcesByScopeNameAsync(new[] { testApiScope.Name });
        }

        resources.ShouldNotBeNull();
        resources.ShouldNotBeEmpty();
        resources.SingleOrDefault(x => x.Name == testApiResource.Name).ShouldNotBeNull();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindIdentityResourcesByScopeNameAsync_WhenResourceExists_ExpectResourceAndCollectionsReturned(DbContextOptions<ConfigurationDbContext> options)
    {
        var resource = CreateIdentityTestResource();

        await using (var context = new ConfigurationDbContext(options))
        {
            context.IdentityResources.Add(resource.ToEntity());
            await context.SaveChangesAsync();
        }

        IList<IdentityResource> resources;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ResourceStore(context, FakeLogger<ResourceStore>.Create(), new NoneCancellationTokenProvider());
            resources = (await store.FindIdentityResourcesByScopeNameAsync(new List<string>
            {
                resource.Name
            })).ToList();
        }

        resources.ShouldNotBeNull();
        resources.ShouldNotBeEmpty();
        var foundScope = resources.Single();

        foundScope.Name.ShouldBe(resource.Name);
        foundScope.UserClaims.ShouldNotBeNull();
        foundScope.UserClaims.ShouldNotBeEmpty();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindIdentityResourcesByScopeNameAsync_WhenResourcesExist_ExpectOnlyRequestedReturned(DbContextOptions<ConfigurationDbContext> options)
    {
        var resource = CreateIdentityTestResource();

        await using (var context = new ConfigurationDbContext(options))
        {
            context.IdentityResources.Add(resource.ToEntity());
            context.IdentityResources.Add(CreateIdentityTestResource().ToEntity());
            await context.SaveChangesAsync();
        }

        IList<IdentityResource> resources;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ResourceStore(context, FakeLogger<ResourceStore>.Create(), new NoneCancellationTokenProvider());
            resources = (await store.FindIdentityResourcesByScopeNameAsync(new List<string>
            {
                resource.Name
            })).ToList();
        }

        resources.ShouldNotBeNull();
        resources.ShouldNotBeEmpty();
        resources.SingleOrDefault(x => x.Name == resource.Name).ShouldNotBeNull();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindApiScopesByNameAsync_WhenResourceExists_ExpectResourceAndCollectionsReturned(DbContextOptions<ConfigurationDbContext> options)
    {
        var resource = CreateApiScopeTestResource();

        await using (var context = new ConfigurationDbContext(options))
        {
            context.ApiScopes.Add(resource.ToEntity());
            await context.SaveChangesAsync();
        }

        IList<ApiScope> resources;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ResourceStore(context, FakeLogger<ResourceStore>.Create(), new NoneCancellationTokenProvider());
            resources = (await store.FindApiScopesByNameAsync(new List<string>
            {
                resource.Name
            })).ToList();
        }

        resources.ShouldNotBeNull();
        resources.ShouldNotBeEmpty();
        var foundScope = resources.Single();

        foundScope.Name.ShouldBe(resource.Name);
        foundScope.UserClaims.ShouldNotBeNull();
        foundScope.UserClaims.ShouldNotBeEmpty();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindApiScopesByNameAsync_WhenResourcesExist_ExpectOnlyRequestedReturned(DbContextOptions<ConfigurationDbContext> options)
    {
        var resource = CreateApiScopeTestResource();

        await using (var context = new ConfigurationDbContext(options))
        {
            context.ApiScopes.Add(resource.ToEntity());
            context.ApiScopes.Add(CreateApiScopeTestResource().ToEntity());
            await context.SaveChangesAsync();
        }

        IList<ApiScope> resources;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ResourceStore(context, FakeLogger<ResourceStore>.Create(), new NoneCancellationTokenProvider());
            resources = (await store.FindApiScopesByNameAsync(new List<string>
            {
                resource.Name
            })).ToList();
        }

        resources.ShouldNotBeNull();
        resources.ShouldNotBeEmpty();
        resources.SingleOrDefault(x => x.Name == resource.Name).ShouldNotBeNull();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetAllResources_WhenAllResourcesRequested_ExpectAllResourcesIncludingHidden(DbContextOptions<ConfigurationDbContext> options)
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

        await using (var context = new ConfigurationDbContext(options))
        {
            context.IdentityResources.Add(visibleIdentityResource.ToEntity());
            context.ApiResources.Add(visibleApiResource.ToEntity());
            context.ApiScopes.Add(visibleApiScope.ToEntity());

            context.IdentityResources.Add(hiddenIdentityResource.ToEntity());
            context.ApiResources.Add(hiddenApiResource.ToEntity());
            context.ApiScopes.Add(hiddenApiScope.ToEntity());

            await context.SaveChangesAsync();
        }

        Resources resources;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ResourceStore(context, FakeLogger<ResourceStore>.Create(), new NoneCancellationTokenProvider());
            resources = await store.GetAllResourcesAsync();
        }

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
