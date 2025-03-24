// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Services.Default.KeyManagement;


namespace IdentityServer.UnitTests.Caches;

public class ResourceStoreCacheTests
{
    private List<Client> _clients { get; set; } = new List<Client>();
    private List<IdentityResource> _identityResources { get; set; } = new List<IdentityResource>();
    private List<ApiResource> _resources { get; set; } = new List<ApiResource>();
    private List<ApiScope> _scopes { get; set; } = new List<ApiScope>();

    private MockClock _mockClock = new MockClock() { UtcNow = new DateTimeOffset(2022, 8, 9, 9, 0, 0, TimeSpan.Zero) };
    private ServiceProvider _provider;

    public ResourceStoreCacheTests()
    {
        _identityResources.Add(new IdentityResources.OpenId());
        _identityResources.Add(new IdentityResources.Profile());

        _resources.Add(new ApiResource("urn:api1") { Scopes = { "scope1", "sharedscope1" } });
        _resources.Add(new ApiResource("urn:api2") { Scopes = { "scope2", "sharedscope1" } });

        _scopes.Add(new ApiScope("scope1"));
        _scopes.Add(new ApiScope("scope2"));
        _scopes.Add(new ApiScope("sharedscope1"));

        var services = new ServiceCollection();
        services.AddIdentityServer()
            .AddInMemoryClients(_clients)
            .AddInMemoryIdentityResources(_identityResources)
            .AddInMemoryApiResources(_resources)
            .AddInMemoryApiScopes(_scopes)
            .AddResourceStoreCache<InMemoryResourcesStore>();

        services.AddSingleton(typeof(MockCache<>));
        services.AddSingleton(typeof(ICache<>), typeof(MockCache<>));
        services.AddSingleton<IClock>(_mockClock);

        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task FindIdentityResourcesByScopeNameAsync_should_populate_cache()
    {
        var cache = (MockCache<IdentityResource>)_provider.GetRequiredService<ICache<IdentityResource>>();
        var store = _provider.GetRequiredService<IResourceStore>();
        cache.CacheItems.Count.ShouldBe(0);

        var results = await store.FindIdentityResourcesByScopeNameAsync(new[] { "profile" });

        cache.CacheItems.Count.ShouldBe(1);
        cache.CacheItems.First().Value.Value.Name.ShouldBe("profile");
    }

    [Fact]
    public async Task FindApiResourcesByScopeNameAsync_should_populate_cache()
    {
        var cache = (MockCache<CachingResourceStore<InMemoryResourcesStore>.ApiResourceNames>)
            _provider.GetRequiredService<ICache<CachingResourceStore<InMemoryResourcesStore>.ApiResourceNames>>();
        var store = _provider.GetRequiredService<IResourceStore>();
        cache.CacheItems.Count.ShouldBe(0);

        var results = await store.FindApiResourcesByScopeNameAsync(new[] { "scope1" });

        cache.CacheItems.Count.ShouldBe(1);
        cache.CacheItems.First().Value.Value.Names.Single().ShouldBe("urn:api1");
    }

    [Fact]
    public async Task FindApiScopesByNameAsync_should_populate_cache()
    {
        var cache = (MockCache<ApiScope>)_provider.GetRequiredService<ICache<ApiScope>>();
        var store = _provider.GetRequiredService<IResourceStore>();
        cache.CacheItems.Count.ShouldBe(0);

        var results = await store.FindApiScopesByNameAsync(new[] { "scope1" });

        cache.CacheItems.Count.ShouldBe(1);
        cache.CacheItems.First().Value.Value.Name.ShouldBe("scope1");
    }
}
