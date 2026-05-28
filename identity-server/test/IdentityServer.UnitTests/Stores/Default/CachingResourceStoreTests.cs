// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Common;

namespace UnitTests.Stores.Default;

public class CachingResourceStoreTests : IDisposable
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private readonly List<IdentityResource> _identityResources = [];
    private readonly List<ApiResource> _apiResources = [];
    private readonly List<ApiScope> _apiScopes = [];
    private readonly SpyResourceStore _spy;
    private readonly FakeTimeProvider _fakeTimeProvider = new();
    private readonly IdentityServerOptions _options = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly CachingResourceStore<SpyResourceStore> _subject;

    public CachingResourceStoreTests()
    {
        _spy = new SpyResourceStore(_identityResources, _apiResources, _apiScopes);
        _serviceProvider = TestHybridCacheHelper.BuildServiceProvider(_fakeTimeProvider);
        var cache = TestHybridCacheHelper.GetCache(_serviceProvider);
        _subject = new CachingResourceStore<SpyResourceStore>(_options, _spy, cache);
    }

    public void Dispose() => _serviceProvider.Dispose();

    [Fact]
    public async Task FindApiScopesByNameAsync_should_populate_cache()
    {
        _apiScopes.Add(new ApiScope("scope1"));
        _apiScopes.Add(new ApiScope("scope2"));
        _apiScopes.Add(new ApiScope("scope3"));
        _apiScopes.Add(new ApiScope("scope4"));

        var items = await _subject.FindApiScopesByNameAsync(["scope3", "scope1", "scope2", "invalid"], _ct);
        items.Count().ShouldBe(3);
        _spy.GetAllResourcesCalls.ShouldBe(1);

        // Second call should use cache
        items = await _subject.FindApiScopesByNameAsync(["scope1", "scope2", "scope3"], _ct);
        items.Count().ShouldBe(3);
        _spy.GetAllResourcesCalls.ShouldBe(1);
    }

    [Fact]
    public async Task FindApiScopesByNameAsync_should_return_matching_scopes()
    {
        _apiScopes.Add(new ApiScope("scope1"));
        _apiScopes.Add(new ApiScope("scope2"));
        _apiScopes.Add(new ApiScope("scope3"));
        _apiScopes.Add(new ApiScope("scope4"));

        var items = await _subject.FindApiScopesByNameAsync(["scope1"], _ct);
        items.Count().ShouldBe(1);
        _spy.GetAllResourcesCalls.ShouldBe(1);

        // Requesting different scopes still uses the same cached snapshot
        items = await _subject.FindApiScopesByNameAsync(["scope1", "scope2"], _ct);
        items.Count().ShouldBe(2);
        _spy.GetAllResourcesCalls.ShouldBe(1);

        items = await _subject.FindApiScopesByNameAsync(["scope3", "scope2", "scope4"], _ct);
        items.Count().ShouldBe(3);
        _spy.GetAllResourcesCalls.ShouldBe(1);
    }

    [Fact]
    public async Task FindApiResourcesByScopeNameAsync_should_populate_cache()
    {
        _apiResources.Add(new ApiResource("foo") { Scopes = { "foo2", "foo1" } });
        _apiResources.Add(new ApiResource("bar") { Scopes = { "bar2", "bar1" } });
        _apiScopes.Add(new ApiScope("foo2"));
        _apiScopes.Add(new ApiScope("foo1"));
        _apiScopes.Add(new ApiScope("bar2"));
        _apiScopes.Add(new ApiScope("bar1"));

        var items = await _subject.FindApiResourcesByScopeNameAsync(["foo1"], _ct);
        items.Count().ShouldBe(1);
        items.Select(x => x.Name).ShouldBe(["foo"]);

        items = await _subject.FindApiResourcesByScopeNameAsync(["foo2"], _ct);
        items.Count().ShouldBe(1);
        items.Select(x => x.Name).ShouldBe(["foo"]);

        items = await _subject.FindApiResourcesByScopeNameAsync(["foo1", "bar1"], _ct);
        items.Count().ShouldBe(2);
        items.Select(x => x.Name).ShouldBe(["foo", "bar"], ignoreOrder: true);

        items = await _subject.FindApiResourcesByScopeNameAsync(["foo2", "foo1", "bar2", "bar1"], _ct);
        items.Count().ShouldBe(2);
        items.Select(x => x.Name).ShouldBe(["foo", "bar"], ignoreOrder: true);
    }

    [Fact]
    public async Task FindApiResourcesByScopeNameAsync_should_return_same_results_twice()
    {
        _apiResources.Add(new ApiResource("foo") { Scopes = { "foo", "foo1" } });
        _apiResources.Add(new ApiResource("bar") { Scopes = { "bar", "bar1" } });

        var items = await _subject.FindApiResourcesByScopeNameAsync(["foo", "foo1", "bar", "bar1"], _ct);
        items.Count().ShouldBe(2);
        items.Select(x => x.Name).ShouldBe(["foo", "bar"], ignoreOrder: true);

        items = await _subject.FindApiResourcesByScopeNameAsync(["foo", "foo1", "bar", "bar1"], _ct);
        items.Count().ShouldBe(2);
        items.Select(x => x.Name).ShouldBe(["foo", "bar"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetAllResourcesAsync_should_use_single_cache_entry()
    {
        _identityResources.Add(new IdentityResources.OpenId());
        _apiResources.Add(new ApiResource("api1") { Scopes = { "scope1" } });
        _apiScopes.Add(new ApiScope("scope1"));

        var all = await _subject.GetAllResourcesAsync(_ct);
        all.IdentityResources.Count.ShouldBe(1);
        all.ApiResources.Count.ShouldBe(1);
        all.ApiScopes.Count.ShouldBe(1);
        _spy.GetAllResourcesCalls.ShouldBe(1);

        // Second call should use cache
        all = await _subject.GetAllResourcesAsync(_ct);
        all.IdentityResources.Count.ShouldBe(1);
        _spy.GetAllResourcesCalls.ShouldBe(1);
    }

    [Fact]
    public async Task All_lookups_share_single_cached_snapshot()
    {
        _identityResources.Add(new IdentityResources.OpenId());
        _apiResources.Add(new ApiResource("api1") { Scopes = { "scope1" } });
        _apiScopes.Add(new ApiScope("scope1"));

        await _subject.FindApiScopesByNameAsync(["scope1"], _ct);
        await _subject.FindApiResourcesByScopeNameAsync(["scope1"], _ct);
        await _subject.FindApiResourcesByNameAsync(["api1"], _ct);
        await _subject.FindIdentityResourcesByScopeNameAsync(["openid"], _ct);
        await _subject.GetAllResourcesAsync(_ct);

        // All five calls should have been served from a single GetAllResources fetch
        _spy.GetAllResourcesCalls.ShouldBe(1);
    }

    /// <summary>
    /// Spy around InMemoryResourcesStore that tracks calls to find methods.
    /// </summary>
    private sealed class SpyResourceStore(
        List<IdentityResource> identityResources,
        List<ApiResource> apiResources,
        List<ApiScope> apiScopes) : IResourceStore
    {
        private readonly InMemoryResourcesStore _inner = new(identityResources, apiResources, apiScopes);

        public int FindApiScopesByNameCalls { get; private set; }
        public int FindApiResourcesByNameCalls { get; private set; }
        public int FindIdentityResourcesByScopeNameCalls { get; private set; }
        public int FindApiResourcesByScopeNameCalls { get; private set; }
        public int GetAllResourcesCalls { get; private set; }

        public Task<IReadOnlyCollection<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames, Ct ct)
        {
            FindApiResourcesByNameCalls++;
            return _inner.FindApiResourcesByNameAsync(apiResourceNames, ct);
        }

        public Task<IReadOnlyCollection<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct ct)
        {
            FindApiResourcesByScopeNameCalls++;
            return _inner.FindApiResourcesByScopeNameAsync(scopeNames, ct);
        }

        public Task<IReadOnlyCollection<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames, Ct ct)
        {
            FindApiScopesByNameCalls++;
            return _inner.FindApiScopesByNameAsync(scopeNames, ct);
        }

        public Task<IReadOnlyCollection<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct ct)
        {
            FindIdentityResourcesByScopeNameCalls++;
            return _inner.FindIdentityResourcesByScopeNameAsync(scopeNames, ct);
        }

        public Task<Resources> GetAllResourcesAsync(Ct ct)
        {
            GetAllResourcesCalls++;
            return _inner.GetAllResourcesAsync(ct);
        }
    }
}
