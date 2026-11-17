// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.ApiResources;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.IdentityServer.Admin.IdentityResources;

namespace Duende.IdentityServer.IntegrationTests.Admin;

public sealed class ResourceStoreTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task find_identity_resources_by_scope_name_returns_matching()
    {
        var admin = _fixture.IdentityResourceAdmin;
        var name = $"identity_{Guid.NewGuid():N}";

        var createResult = await admin.CreateAsync(new CreateIdentityResource
        {
            Name = name,
            UserClaims = ["sub"]
        }, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var store = _fixture.ResourceStore;
        var results = await store.FindIdentityResourcesByScopeNameAsync([name], _ct);

        results.ShouldHaveSingleItem();
        results.First().Name.ShouldBe(name);
    }

    [Fact]
    public async Task find_identity_resources_by_scope_name_returns_empty_for_nonexistent()
    {
        var store = _fixture.ResourceStore;
        var results = await store.FindIdentityResourcesByScopeNameAsync([$"nonexistent_{Guid.NewGuid():N}"], _ct);
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task find_identity_resources_with_empty_input_returns_empty()
    {
        var store = _fixture.ResourceStore;
        var results = await store.FindIdentityResourcesByScopeNameAsync([], _ct);
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task find_api_scopes_by_name_returns_matching()
    {
        var admin = _fixture.ApiScopeAdmin;
        var name = $"scope_{Guid.NewGuid():N}";

        var createResult = await admin.CreateAsync(new CreateApiScope
        {
            Name = name,
            UserClaims = ["email"]
        }, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var store = _fixture.ResourceStore;
        var results = await store.FindApiScopesByNameAsync([name], _ct);

        results.ShouldHaveSingleItem();
        results.First().Name.ShouldBe(name);
    }

    [Fact]
    public async Task find_api_scopes_by_name_returns_empty_for_nonexistent()
    {
        var store = _fixture.ResourceStore;
        var results = await store.FindApiScopesByNameAsync([$"nonexistent_{Guid.NewGuid():N}"], _ct);
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task find_api_scopes_with_empty_input_returns_empty()
    {
        var store = _fixture.ResourceStore;
        var results = await store.FindApiScopesByNameAsync([], _ct);
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task find_api_resources_by_name_returns_matching()
    {
        var scopeAdmin = _fixture.ApiScopeAdmin;
        var scopeName = $"scope_{Guid.NewGuid():N}";
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = scopeName }, _ct)).IsSuccess.ShouldBeTrue();

        var admin = _fixture.ApiResourceAdmin;
        var name = $"api_{Guid.NewGuid():N}";

        var createResult = await admin.CreateAsync(new CreateApiResource
        {
            Name = name,
            Scopes = [scopeName]
        }, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var store = _fixture.ResourceStore;
        var results = await store.FindApiResourcesByNameAsync([name], _ct);

        results.ShouldHaveSingleItem();
        results.First().Name.ShouldBe(name);
    }

    [Fact]
    public async Task find_api_resources_by_name_matches_case_exactly()
    {
        var scopeName = $"scope_{Guid.NewGuid():N}";
        (await _fixture.ApiScopeAdmin.CreateAsync(new CreateApiScope { Name = scopeName }, _ct)).IsSuccess.ShouldBeTrue();

        var lower = $"api_{Guid.NewGuid():N}";
        var upper = lower.ToUpperInvariant();
        (await _fixture.ApiResourceAdmin.CreateAsync(new CreateApiResource { Name = lower, Scopes = [scopeName] }, _ct)).IsSuccess.ShouldBeTrue();
        (await _fixture.ApiResourceAdmin.CreateAsync(new CreateApiResource { Name = upper, Scopes = [scopeName] }, _ct)).IsSuccess.ShouldBeTrue();

        var store = _fixture.ResourceStore;

        (await store.FindApiResourcesByNameAsync([lower], _ct)).Select(r => r.Name).ShouldBe([lower]);
        (await store.FindApiResourcesByNameAsync(["Api" + lower[3..]], _ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task by_name_lookups_find_names_beyond_the_first_thousand_requested()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var firstScope = $"scope_first_{suffix}";
        var lastScope = $"scope_last_{suffix}";
        var identityResource = $"identity_last_{suffix}";
        var apiResource = $"api_last_{suffix}";

        (await _fixture.ApiScopeAdmin.CreateAsync(new CreateApiScope { Name = firstScope }, _ct)).IsSuccess.ShouldBeTrue();
        (await _fixture.ApiScopeAdmin.CreateAsync(new CreateApiScope { Name = lastScope }, _ct)).IsSuccess.ShouldBeTrue();
        (await _fixture.IdentityResourceAdmin.CreateAsync(new CreateIdentityResource { Name = identityResource, UserClaims = ["sub"] }, _ct)).IsSuccess.ShouldBeTrue();
        (await _fixture.ApiResourceAdmin.CreateAsync(new CreateApiResource { Name = apiResource, Scopes = [firstScope] }, _ct)).IsSuccess.ShouldBeTrue();

        // More names than one name filter holds, with real names before and after the first 1,000.
        var unknownNames = Enumerable.Range(0, 1500).Select(i => $"unknown_{i}_{suffix}").ToList();
        string[] Names(string first, string last) => [first, .. unknownNames, last];

        var store = _fixture.ResourceStore;

        (await store.FindApiScopesByNameAsync(Names(firstScope, lastScope), _ct))
            .Select(s => s.Name).ShouldBe([firstScope, lastScope], ignoreOrder: true);
        (await store.FindIdentityResourcesByScopeNameAsync(Names(firstScope, identityResource), _ct))
            .Select(r => r.Name).ShouldBe([identityResource]);
        (await store.FindApiResourcesByNameAsync(Names(firstScope, apiResource), _ct))
            .Select(r => r.Name).ShouldBe([apiResource]);
    }

    [Fact]
    public async Task find_api_resources_by_name_returns_empty_for_nonexistent()
    {
        var store = _fixture.ResourceStore;
        var results = await store.FindApiResourcesByNameAsync([$"nonexistent_{Guid.NewGuid():N}"], _ct);
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task find_api_resources_with_empty_input_returns_empty()
    {
        var store = _fixture.ResourceStore;
        var results = await store.FindApiResourcesByNameAsync([], _ct);
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task find_api_resources_by_scope_name_returns_matching()
    {
        var scopeAdmin = _fixture.ApiScopeAdmin;
        var uniqueScope = $"scope_{Guid.NewGuid():N}";
        var otherScope = $"scope_other_{Guid.NewGuid():N}";
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = uniqueScope }, _ct)).IsSuccess.ShouldBeTrue();
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = otherScope }, _ct)).IsSuccess.ShouldBeTrue();

        var admin = _fixture.ApiResourceAdmin;
        var resourceName = $"api_{Guid.NewGuid():N}";

        var createResult = await admin.CreateAsync(new CreateApiResource
        {
            Name = resourceName,
            Scopes = [uniqueScope, otherScope]
        }, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var store = _fixture.ResourceStore;
        var results = await store.FindApiResourcesByScopeNameAsync([uniqueScope], _ct);

        results.ShouldContain(r => r.Name == resourceName);
    }

    [Fact]
    public async Task find_api_resources_by_scope_name_excludes_non_matching()
    {
        var scopeAdmin = _fixture.ApiScopeAdmin;
        var uniqueScope = $"scope_{Guid.NewGuid():N}";
        var differentScope = $"scope_diff_{Guid.NewGuid():N}";
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = uniqueScope }, _ct)).IsSuccess.ShouldBeTrue();
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = differentScope }, _ct)).IsSuccess.ShouldBeTrue();

        var admin = _fixture.ApiResourceAdmin;
        var matchingName = $"api_match_{Guid.NewGuid():N}";
        var nonMatchingName = $"api_nomatch_{Guid.NewGuid():N}";

        await admin.CreateAsync(new CreateApiResource
        {
            Name = matchingName,
            Scopes = [uniqueScope]
        }, _ct);

        await admin.CreateAsync(new CreateApiResource
        {
            Name = nonMatchingName,
            Scopes = [differentScope]
        }, _ct);

        var store = _fixture.ResourceStore;
        var results = await store.FindApiResourcesByScopeNameAsync([uniqueScope], _ct);

        results.ShouldContain(r => r.Name == matchingName);
        results.ShouldNotContain(r => r.Name == nonMatchingName);
    }

    [Fact]
    public async Task find_api_resources_by_scope_name_with_empty_input_returns_empty()
    {
        var store = _fixture.ResourceStore;
        var results = await store.FindApiResourcesByScopeNameAsync([], _ct);
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task find_api_resources_by_scope_name_is_exact_and_case_sensitive()
    {
        var scopeAdmin = _fixture.ApiScopeAdmin;
        var admin = _fixture.ApiResourceAdmin;
        var scopeName = $"Scope_{Guid.NewGuid():N}";
        var longerScopeName = scopeName + "_extra";
        var resourceName = $"api_{Guid.NewGuid():N}";
        var longerResourceName = $"api_longer_{Guid.NewGuid():N}";

        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = scopeName }, _ct)).IsSuccess.ShouldBeTrue();
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = longerScopeName }, _ct)).IsSuccess.ShouldBeTrue();
        (await admin.CreateAsync(new CreateApiResource { Name = resourceName, Scopes = [scopeName] }, _ct)).IsSuccess.ShouldBeTrue();
        (await admin.CreateAsync(new CreateApiResource { Name = longerResourceName, Scopes = [longerScopeName] }, _ct)).IsSuccess.ShouldBeTrue();

        var store = _fixture.ResourceStore;

        var exactMatch = await store.FindApiResourcesByScopeNameAsync([scopeName], _ct);
        exactMatch.ShouldContain(r => r.Name == resourceName);
        exactMatch.ShouldNotContain(r => r.Name == longerResourceName);

        var wrongCase = await store.FindApiResourcesByScopeNameAsync([scopeName.ToUpperInvariant()], _ct);
        wrongCase.ShouldNotContain(r => r.Name == resourceName);
    }

    [Fact]
    public async Task get_all_resources_returns_all_types()
    {
        var apiResourceAdmin = _fixture.ApiResourceAdmin;
        var apiScopeAdmin = _fixture.ApiScopeAdmin;
        var identityResourceAdmin = _fixture.IdentityResourceAdmin;

        var apiResourceName = $"api_{Guid.NewGuid():N}";
        var apiScopeName = $"scope_{Guid.NewGuid():N}";
        var apiScopeForResource = $"scope_for_resource_{Guid.NewGuid():N}";
        var identityResourceName = $"identity_{Guid.NewGuid():N}";

        (await apiScopeAdmin.CreateAsync(new CreateApiScope { Name = apiScopeForResource }, _ct)).IsSuccess.ShouldBeTrue();
        await apiResourceAdmin.CreateAsync(new CreateApiResource { Name = apiResourceName, Scopes = [apiScopeForResource] }, _ct);
        await apiScopeAdmin.CreateAsync(new CreateApiScope { Name = apiScopeName }, _ct);
        await identityResourceAdmin.CreateAsync(new CreateIdentityResource { Name = identityResourceName }, _ct);

        var store = _fixture.ResourceStore;
        var resources = await store.GetAllResourcesAsync(_ct);

        resources.ApiResources.ShouldContain(r => r.Name == apiResourceName);
        resources.ApiScopes.ShouldContain(s => s.Name == apiScopeName);
        resources.IdentityResources.ShouldContain(r => r.Name == identityResourceName);
    }

    [Fact]
    public async Task resource_store_does_not_filter_on_enabled()
    {
        var scopeAdmin = _fixture.ApiScopeAdmin;
        var someScope = $"scope_{Guid.NewGuid():N}";
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = someScope }, _ct)).IsSuccess.ShouldBeTrue();

        var admin = _fixture.ApiResourceAdmin;
        var disabledName = $"api_disabled_{Guid.NewGuid():N}";

        await admin.CreateAsync(new CreateApiResource
        {
            Name = disabledName,
            Enabled = false,
            Scopes = [someScope]
        }, _ct);

        var store = _fixture.ResourceStore;
        var results = await store.FindApiResourcesByNameAsync([disabledName], _ct);

        results.ShouldHaveSingleItem();
        results.First().Name.ShouldBe(disabledName);
        results.First().Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task find_api_resources_by_scope_name_with_multiple_scopes()
    {
        var scopeAdmin = _fixture.ApiScopeAdmin;
        var scope1 = $"scope1_{Guid.NewGuid():N}";
        var scope2 = $"scope2_{Guid.NewGuid():N}";
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = scope1 }, _ct)).IsSuccess.ShouldBeTrue();
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = scope2 }, _ct)).IsSuccess.ShouldBeTrue();

        var admin = _fixture.ApiResourceAdmin;
        var resource1Name = $"api1_{Guid.NewGuid():N}";
        var resource2Name = $"api2_{Guid.NewGuid():N}";

        await admin.CreateAsync(new CreateApiResource
        {
            Name = resource1Name,
            Scopes = [scope1]
        }, _ct);

        await admin.CreateAsync(new CreateApiResource
        {
            Name = resource2Name,
            Scopes = [scope2]
        }, _ct);

        var store = _fixture.ResourceStore;
        var results = await store.FindApiResourcesByScopeNameAsync([scope1, scope2], _ct);

        results.ShouldContain(r => r.Name == resource1Name);
        results.ShouldContain(r => r.Name == resource2Name);
    }

    [Fact]
    public async Task find_api_resources_by_scope_name_follows_scope_rename()
    {
        var originalName = $"scope_{Guid.NewGuid():N}";
        var newName = $"scope_{Guid.NewGuid():N}";
        var resourceName = $"api_{Guid.NewGuid():N}";
        var scopeId = await CreateScopeAsync(originalName);
        await CreateApiResourceAsync(resourceName, originalName);

        await RenameScopeAsync(scopeId, newName);

        var store = _fixture.ResourceStore;
        var results = await store.FindApiResourcesByScopeNameAsync([newName], _ct);
        var resource = results.ShouldHaveSingleItem();
        resource.Name.ShouldBe(resourceName);
        resource.Scopes.ShouldBe([newName]);
        (await store.FindApiResourcesByScopeNameAsync([originalName], _ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task reused_scope_name_does_not_match_api_resources_of_the_renamed_scope()
    {
        var originalName = $"scope_{Guid.NewGuid():N}";
        var newName = $"scope_{Guid.NewGuid():N}";
        var resourceName = $"api_{Guid.NewGuid():N}";
        var scopeId = await CreateScopeAsync(originalName);
        await CreateApiResourceAsync(resourceName, originalName);
        await RenameScopeAsync(scopeId, newName);

        await CreateScopeAsync(originalName);

        var store = _fixture.ResourceStore;
        (await store.FindApiResourcesByScopeNameAsync([originalName], _ct)).ShouldBeEmpty();
        var byName = await store.FindApiResourcesByNameAsync([resourceName], _ct);
        byName.ShouldHaveSingleItem().Scopes.ShouldBe([newName]);
        var all = await store.GetAllResourcesAsync(_ct);
        all.ApiResources.Single(r => r.Name == resourceName).Scopes.ShouldBe([newName]);
    }

    [Fact]
    public async Task find_api_resources_by_name_returns_each_resources_own_scopes()
    {
        var shared = $"shared_{Guid.NewGuid():N}";
        var only1 = $"only1_{Guid.NewGuid():N}";
        await CreateScopeAsync(shared);
        await CreateScopeAsync(only1);

        var api1 = $"api1_{Guid.NewGuid():N}";
        var api2 = $"api2_{Guid.NewGuid():N}";
        var apiWithoutScopes = $"api3_{Guid.NewGuid():N}";
        await CreateApiResourceAsync(api1, shared, only1);
        await CreateApiResourceAsync(api2, shared);
        await CreateApiResourceAsync(apiWithoutScopes);

        var results = await _fixture.ResourceStore.FindApiResourcesByNameAsync([api1, api2, apiWithoutScopes], _ct);

        results.Single(r => r.Name == api1).Scopes.ShouldBe([shared, only1], ignoreOrder: true);
        results.Single(r => r.Name == api2).Scopes.ShouldBe([shared]);
        results.Single(r => r.Name == apiWithoutScopes).Scopes.ShouldBeEmpty();
    }

    private async Task<ApiScopeId> CreateScopeAsync(string name)
    {
        var result = await _fixture.ApiScopeAdmin.CreateAsync(new CreateApiScope { Name = name }, _ct);
        result.IsSuccess.ShouldBeTrue($"Create scope failed: {result}");
        return result.Id!;
    }

    private async Task CreateApiResourceAsync(string name, params string[] scopes)
    {
        var result = await _fixture.ApiResourceAdmin.CreateAsync(new CreateApiResource { Name = name, Scopes = [.. scopes] }, _ct);
        result.IsSuccess.ShouldBeTrue($"Create API resource failed: {result}");
    }

    private async Task RenameScopeAsync(ApiScopeId id, string newName)
    {
        var scope = await _fixture.ApiScopeAdmin.GetAsync(id, _ct);
        var toUpdate = scope.Item!.ToUpdate();
        toUpdate.Name = newName;
        var result = await _fixture.ApiScopeAdmin.UpdateAsync(id, toUpdate, scope.Version!, _ct);
        result.IsSuccess.ShouldBeTrue($"Rename scope failed: {result}");
    }

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();
}
