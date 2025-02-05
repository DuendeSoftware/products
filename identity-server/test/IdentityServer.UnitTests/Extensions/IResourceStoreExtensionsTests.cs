// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Shouldly;
using Xunit;

namespace UnitTests.Extensions;

public class IResourceStoreExtensionsTests
{
    [Fact]
    public async Task GetAllEnabledResourcesAsync_on_duplicate_identity_scopes_should_fail()
    {
        var store = new MockResourceStore()
        {
            IdentityResources = {
                new IdentityResource { Name = "A" },
                new IdentityResource { Name = "A" } }
        };

        Func<Task> a = () => store.GetAllEnabledResourcesAsync();
        await a.ShouldThrowAsync<Exception>("duplicate identity scopes*");
    }

    [Fact]
    public async Task GetAllEnabledResourcesAsync_without_duplicate_identity_scopes_should_succeed()
    {
        var store = new MockResourceStore()
        {
            IdentityResources = {
                new IdentityResource { Name = "A" },
                new IdentityResource { Name = "B" } }
        };

        await store.GetAllEnabledResourcesAsync();
    }

    [Fact]
    public async Task GetAllEnabledResourcesAsync_on_duplicate_api_resources_should_fail()
    {
        var store = new MockResourceStore()
        {
            ApiResources = { new ApiResource { Name = "a" }, new ApiResource { Name = "a" } }
        };

        Func<Task> a = () => store.GetAllEnabledResourcesAsync();
        await a.ShouldThrowAsync<Exception>("duplicate api resources*");
    }

    [Fact]
    public async Task GetAllEnabledResourcesAsync_without_duplicate_api_scopes_should_succeed()
    {
        var store = new MockResourceStore()
        {
            ApiResources = { new ApiResource("A"), new ApiResource("B") }
        };

        await store.GetAllEnabledResourcesAsync();
    }

    [Fact]
    public async Task FindResourcesByScopeAsync_on_duplicate_identity_scopes_should_fail()
    {
        var store = new MockResourceStore()
        {
            IdentityResources = {
                new IdentityResource { Name = "A" },
                new IdentityResource { Name = "A" } }
        };

        Func<Task> a = () => store.FindResourcesByScopeAsync(new string[] { "A" });
        await a.ShouldThrowAsync<Exception>("duplicate identity scopes*");
    }

    [Fact]
    public async Task FindResourcesByScopeAsync_without_duplicate_identity_scopes_should_succeed()
    {
        var store = new MockResourceStore()
        {
            IdentityResources = {
                new IdentityResource { Name = "A" },
                new IdentityResource { Name = "B" } }
        };

        await store.FindResourcesByScopeAsync(new string[] { "A" });
    }

    [Fact]
    public async Task FindResourcesByScopeAsync_on_duplicate_api_scopes_should_succeed()
    {
        var store = new MockResourceStore()
        {
            ApiResources = { 
                new ApiResource { Name = "api1", Scopes = { "a" } },
                new ApiResource() { Name = "api2", Scopes = { "a" } },
            },
            ApiScopes = { 
                new ApiScope("a") 
            } 
        };

        var result = await store.FindResourcesByScopeAsync(new string[] { "a" });
        result.ApiResources.Count.ShouldBe(2);
        result.ApiScopes.Count.ShouldBe(1);
        result.ApiResources.Select(x => x.Name).ShouldBe(["api1", "api2"]);
        result.ApiScopes.Select(x => x.Name).ShouldBe(["a"]);
    }

    [Fact]
    public async Task FindResourcesByScopeAsync_without_duplicate_api_scopes_should_succeed()
    {
        var store = new MockResourceStore()
        {
            ApiResources = { new ApiResource("A"), new ApiResource("B") }
        };

        await store.FindResourcesByScopeAsync(new string[] { "A" });
    }

    [Fact]
    public async Task FindResourcesByScopeAsync_with_duplicate_api_scopes_on_single_api_resource_should_succeed_and_only_reuturn_one_resource()
    {
        var store = new MockResourceStore()
        {
            ApiResources = { 
                new ApiResource { 
                    Name = "api1", Scopes = { "a", "a" }
                }
            },
            ApiScopes = {
                new ApiScope("a"),
            }
        };

        var result = await store.FindResourcesByScopeAsync(new string[] { "a" });
        result.ApiResources.Count.ShouldBe(1);
    }

    public class MockResourceStore : IResourceStore
    {
        public List<IdentityResource> IdentityResources { get; set; } = new List<IdentityResource>();
        public List<ApiResource> ApiResources { get; set; } = new List<ApiResource>();
        public List<ApiScope> ApiScopes { get; set; } = new List<ApiScope>();

        public Task<IEnumerable<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> names)
        {
            var apis = from a in ApiResources
                where names.Contains(a.Name)
                select a;
            return Task.FromResult(apis);
        }

        public Task<IEnumerable<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> names)
        {
            ArgumentNullException.ThrowIfNull(names);

            var api = from a in ApiResources
                where a.Scopes.Any(x => names.Contains(x))
                select a;

            return Task.FromResult(api);
        }

        public Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> names)
        {
            ArgumentNullException.ThrowIfNull(names);

            var identity = from i in IdentityResources
                where names.Contains(i.Name)
                select i;

            return Task.FromResult(identity);
        }

        public Task<IEnumerable<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames)
        {
            var q = from x in ApiScopes
                where scopeNames.Contains(x.Name)
                select x;
            return Task.FromResult(q);
        }

        public Task<Resources> GetAllResourcesAsync()
        {
            var result = new Resources(IdentityResources, ApiResources, ApiScopes);
            return Task.FromResult(result);
        }
    }
}
