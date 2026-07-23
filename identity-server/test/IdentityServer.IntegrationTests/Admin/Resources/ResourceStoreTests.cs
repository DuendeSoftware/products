// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.ApiResources;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.IdentityServer.Admin.IdentityResources;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin;

public sealed class ResourceStoreTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly List<IServiceScope> _scopes = [];

    private IApiResourceAdmin NewApiResourceAdmin()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IApiResourceAdmin>();
    }

    private IApiScopeAdmin NewApiScopeAdmin()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IApiScopeAdmin>();
    }

    private IIdentityResourceAdmin NewIdentityResourceAdmin()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IIdentityResourceAdmin>();
    }

    private IResourceStore NewResourceStore()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IResourceStore>();
    }

    [Fact]
    public async Task find_identity_resources_by_scope_name_returns_matching()
    {
        var admin = NewIdentityResourceAdmin();
        var name = $"identity_{Guid.NewGuid():N}";

        var createResult = await admin.CreateAsync(new IdentityResourceConfiguration
        {
            Name = name,
            UserClaims = ["sub"]
        }, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var store = NewResourceStore();
        var results = await store.FindIdentityResourcesByScopeNameAsync([name], _ct);

        results.ShouldHaveSingleItem();
        results.First().Name.ShouldBe(name);
    }

    [Fact]
    public async Task find_identity_resources_by_scope_name_returns_empty_for_nonexistent()
    {
        var store = NewResourceStore();
        var results = await store.FindIdentityResourcesByScopeNameAsync([$"nonexistent_{Guid.NewGuid():N}"], _ct);
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task find_identity_resources_with_empty_input_returns_empty()
    {
        var store = NewResourceStore();
        var results = await store.FindIdentityResourcesByScopeNameAsync([], _ct);
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task find_api_scopes_by_name_returns_matching()
    {
        var admin = NewApiScopeAdmin();
        var name = $"scope_{Guid.NewGuid():N}";

        var createResult = await admin.CreateAsync(new ApiScopeConfiguration
        {
            Name = name,
            UserClaims = ["email"]
        }, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var store = NewResourceStore();
        var results = await store.FindApiScopesByNameAsync([name], _ct);

        results.ShouldHaveSingleItem();
        results.First().Name.ShouldBe(name);
    }

    [Fact]
    public async Task find_api_scopes_by_name_returns_empty_for_nonexistent()
    {
        var store = NewResourceStore();
        var results = await store.FindApiScopesByNameAsync([$"nonexistent_{Guid.NewGuid():N}"], _ct);
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task find_api_scopes_with_empty_input_returns_empty()
    {
        var store = NewResourceStore();
        var results = await store.FindApiScopesByNameAsync([], _ct);
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task find_api_resources_by_name_returns_matching()
    {
        var scopeAdmin = NewApiScopeAdmin();
        var scopeName = $"scope_{Guid.NewGuid():N}";
        (await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = scopeName }, _ct)).IsSuccess.ShouldBeTrue();

        var admin = NewApiResourceAdmin();
        var name = $"api_{Guid.NewGuid():N}";

        var createResult = await admin.CreateAsync(new ApiResourceConfiguration
        {
            Name = name,
            Scopes = [scopeName]
        }, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var store = NewResourceStore();
        var results = await store.FindApiResourcesByNameAsync([name], _ct);

        results.ShouldHaveSingleItem();
        results.First().Name.ShouldBe(name);
    }

    [Fact]
    public async Task find_api_resources_by_name_returns_empty_for_nonexistent()
    {
        var store = NewResourceStore();
        var results = await store.FindApiResourcesByNameAsync([$"nonexistent_{Guid.NewGuid():N}"], _ct);
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task find_api_resources_with_empty_input_returns_empty()
    {
        var store = NewResourceStore();
        var results = await store.FindApiResourcesByNameAsync([], _ct);
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task find_api_resources_by_scope_name_returns_matching()
    {
        var scopeAdmin = NewApiScopeAdmin();
        var uniqueScope = $"scope_{Guid.NewGuid():N}";
        var otherScope = $"scope_other_{Guid.NewGuid():N}";
        (await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = uniqueScope }, _ct)).IsSuccess.ShouldBeTrue();
        (await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = otherScope }, _ct)).IsSuccess.ShouldBeTrue();

        var admin = NewApiResourceAdmin();
        var resourceName = $"api_{Guid.NewGuid():N}";

        var createResult = await admin.CreateAsync(new ApiResourceConfiguration
        {
            Name = resourceName,
            Scopes = [uniqueScope, otherScope]
        }, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var store = NewResourceStore();
        var results = await store.FindApiResourcesByScopeNameAsync([uniqueScope], _ct);

        results.ShouldContain(r => r.Name == resourceName);
    }

    [Fact]
    public async Task find_api_resources_by_scope_name_excludes_non_matching()
    {
        var scopeAdmin = NewApiScopeAdmin();
        var uniqueScope = $"scope_{Guid.NewGuid():N}";
        var differentScope = $"scope_diff_{Guid.NewGuid():N}";
        (await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = uniqueScope }, _ct)).IsSuccess.ShouldBeTrue();
        (await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = differentScope }, _ct)).IsSuccess.ShouldBeTrue();

        var admin = NewApiResourceAdmin();
        var matchingName = $"api_match_{Guid.NewGuid():N}";
        var nonMatchingName = $"api_nomatch_{Guid.NewGuid():N}";

        await admin.CreateAsync(new ApiResourceConfiguration
        {
            Name = matchingName,
            Scopes = [uniqueScope]
        }, _ct);

        await admin.CreateAsync(new ApiResourceConfiguration
        {
            Name = nonMatchingName,
            Scopes = [differentScope]
        }, _ct);

        var store = NewResourceStore();
        var results = await store.FindApiResourcesByScopeNameAsync([uniqueScope], _ct);

        results.ShouldContain(r => r.Name == matchingName);
        results.ShouldNotContain(r => r.Name == nonMatchingName);
    }

    [Fact]
    public async Task find_api_resources_by_scope_name_with_empty_input_returns_empty()
    {
        var store = NewResourceStore();
        var results = await store.FindApiResourcesByScopeNameAsync([], _ct);
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task get_all_resources_returns_all_types()
    {
        var apiResourceAdmin = NewApiResourceAdmin();
        var apiScopeAdmin = NewApiScopeAdmin();
        var identityResourceAdmin = NewIdentityResourceAdmin();

        var apiResourceName = $"api_{Guid.NewGuid():N}";
        var apiScopeName = $"scope_{Guid.NewGuid():N}";
        var apiScopeForResource = $"scope_for_resource_{Guid.NewGuid():N}";
        var identityResourceName = $"identity_{Guid.NewGuid():N}";

        (await apiScopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = apiScopeForResource }, _ct)).IsSuccess.ShouldBeTrue();
        await apiResourceAdmin.CreateAsync(new ApiResourceConfiguration { Name = apiResourceName, Scopes = [apiScopeForResource] }, _ct);
        await apiScopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = apiScopeName }, _ct);
        await identityResourceAdmin.CreateAsync(new IdentityResourceConfiguration { Name = identityResourceName }, _ct);

        var store = NewResourceStore();
        var resources = await store.GetAllResourcesAsync(_ct);

        resources.ApiResources.ShouldContain(r => r.Name == apiResourceName);
        resources.ApiScopes.ShouldContain(s => s.Name == apiScopeName);
        resources.IdentityResources.ShouldContain(r => r.Name == identityResourceName);
    }

    [Fact]
    public async Task resource_store_does_not_filter_on_enabled()
    {
        var scopeAdmin = NewApiScopeAdmin();
        var someScope = $"scope_{Guid.NewGuid():N}";
        (await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = someScope }, _ct)).IsSuccess.ShouldBeTrue();

        var admin = NewApiResourceAdmin();
        var disabledName = $"api_disabled_{Guid.NewGuid():N}";

        await admin.CreateAsync(new ApiResourceConfiguration
        {
            Name = disabledName,
            Enabled = false,
            Scopes = [someScope]
        }, _ct);

        var store = NewResourceStore();
        var results = await store.FindApiResourcesByNameAsync([disabledName], _ct);

        results.ShouldHaveSingleItem();
        results.First().Name.ShouldBe(disabledName);
        results.First().Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task find_api_resources_by_scope_name_with_multiple_scopes()
    {
        var scopeAdmin = NewApiScopeAdmin();
        var scope1 = $"scope1_{Guid.NewGuid():N}";
        var scope2 = $"scope2_{Guid.NewGuid():N}";
        (await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = scope1 }, _ct)).IsSuccess.ShouldBeTrue();
        (await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = scope2 }, _ct)).IsSuccess.ShouldBeTrue();

        var admin = NewApiResourceAdmin();
        var resource1Name = $"api1_{Guid.NewGuid():N}";
        var resource2Name = $"api2_{Guid.NewGuid():N}";

        await admin.CreateAsync(new ApiResourceConfiguration
        {
            Name = resource1Name,
            Scopes = [scope1]
        }, _ct);

        await admin.CreateAsync(new ApiResourceConfiguration
        {
            Name = resource2Name,
            Scopes = [scope2]
        }, _ct);

        var store = NewResourceStore();
        var results = await store.FindApiResourcesByScopeNameAsync([scope1, scope2], _ct);

        results.ShouldContain(r => r.Name == resource1Name);
        results.ShouldContain(r => r.Name == resource2Name);
    }

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        await _fixture.DisposeAsync();
    }
}
