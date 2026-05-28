// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Common;

namespace IdentityServer.UnitTests.Caches;

public class ResourceStoreCacheTests : IDisposable
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private readonly List<Client> _clients = [];
    private readonly List<IdentityResource> _identityResources = [];
    private readonly List<ApiResource> _resources = [];
    private readonly List<ApiScope> _scopes = [];

    private readonly FakeTimeProvider _fakeTimeProvider = new(new DateTimeOffset(2022, 8, 9, 9, 0, 0, TimeSpan.Zero));
    private readonly ServiceProvider _cacheProvider;
    private readonly ServiceProvider _provider;

    public ResourceStoreCacheTests()
    {
        _identityResources.Add(new IdentityResources.OpenId());
        _identityResources.Add(new IdentityResources.Profile());

        _resources.Add(new ApiResource("urn:api1") { Scopes = { "scope1", "sharedscope1" } });
        _resources.Add(new ApiResource("urn:api2") { Scopes = { "scope2", "sharedscope1" } });

        _scopes.Add(new ApiScope("scope1"));
        _scopes.Add(new ApiScope("scope2"));
        _scopes.Add(new ApiScope("sharedscope1"));

        _cacheProvider = TestHybridCacheHelper.BuildServiceProvider(_fakeTimeProvider);
        var cache = TestHybridCacheHelper.GetCache(_cacheProvider);

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(_fakeTimeProvider);
        services.AddIdentityServer()
            .AddInMemoryClients(_clients)
            .AddInMemoryIdentityResources(_identityResources)
            .AddInMemoryApiResources(_resources)
            .AddInMemoryApiScopes(_scopes)
            .AddResourceStoreCache<InMemoryResourcesStore>();

        // Override the keyed HybridCache with the time-controllable one from TestHybridCacheHelper
        services.AddKeyedSingleton<HybridCache>(ServiceProviderKeys.ConfigurationStoreCache, (_, _) => cache);

        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _cacheProvider.Dispose();
    }

    [Fact]
    public async Task FindIdentityResourcesByScopeNameAsync_should_populate_cache()
    {
        var store = _provider.GetRequiredService<IResourceStore>();

        var results = await store.FindIdentityResourcesByScopeNameAsync(["profile"], _ct);

        results.ShouldNotBeEmpty();
        results.Single().Name.ShouldBe("profile");
    }

    [Fact]
    public async Task FindApiResourcesByScopeNameAsync_should_populate_cache()
    {
        var store = _provider.GetRequiredService<IResourceStore>();

        var results = await store.FindApiResourcesByScopeNameAsync(["scope1"], _ct);

        results.ShouldNotBeEmpty();
        results.Single().Name.ShouldBe("urn:api1");
    }

    [Fact]
    public async Task FindApiScopesByNameAsync_should_populate_cache()
    {
        var store = _provider.GetRequiredService<IResourceStore>();

        var results = await store.FindApiScopesByNameAsync(["scope1"], _ct);

        results.ShouldNotBeEmpty();
        results.Single().Name.ShouldBe("scope1");
    }
}
