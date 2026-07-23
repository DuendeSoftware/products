// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography;
using System.Text;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.ApiResources;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin;

public sealed class ApiResourceAdminTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly List<IServiceScope> _scopes = [];

    private IApiResourceAdmin NewAdmin()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IApiResourceAdmin>();
    }

    private IApiScopeAdmin NewScopeAdmin()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IApiScopeAdmin>();
    }

    private IResourceStore NewResourceStore()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IResourceStore>();
    }

    private async Task<ApiScopeDso.V1?> ReadScopeDsoAsync(Guid scopeId)
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        var storeFactory = scope.ServiceProvider.GetRequiredService<IStoreFactory>();
        var store = await storeFactory.GetStore(_ct);
        var result = await store.TryReadAsync(ApiScopeDso.EntityType, UuidV7.From(scopeId), _ct);
        return result.Found ? (ApiScopeDso.V1)result.Dso : null;
    }

    private async Task<Guid> CreateResourceAsync(IApiResourceAdmin admin, string? name = null, Ct ct = default)
    {
        var result = await admin.CreateAsync(
            new ApiResourceConfiguration { Name = name ?? $"api_{Guid.NewGuid():N}" },
            ct == default ? _ct : ct);
        result.IsSuccess.ShouldBeTrue($"CreateResource failed: {result}");
        return result.Id;
    }

    [Fact]
    public async Task create_and_get_by_id_round_trips_all_fields()
    {
        var scopeAdmin = NewScopeAdmin();
        var readScopeName = $"api1_read_{Guid.NewGuid():N}";
        var writeScopeName = $"api1_write_{Guid.NewGuid():N}";
        (await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = readScopeName }, _ct)).IsSuccess.ShouldBeTrue();
        (await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = writeScopeName }, _ct)).IsSuccess.ShouldBeTrue();

        var admin = NewAdmin();
        var resource = new ApiResourceConfiguration
        {
            Name = $"api_{Guid.NewGuid():N}",
            DisplayName = "Test API",
            Description = "A test API resource",
            Enabled = true,
            ShowInDiscoveryDocument = false,
            RequireResourceIndicator = true,
            UserClaims = ["sub", "email"],
            Scopes = [readScopeName, writeScopeName],
            AllowedAccessTokenSigningAlgorithms = ["RS256"]
        };

        var createResult = await admin.CreateAsync(resource, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var loaded = getResult.Item;
        loaded.Name.ShouldBe(resource.Name);
        loaded.DisplayName.ShouldBe(resource.DisplayName);
        loaded.Description.ShouldBe(resource.Description);
        loaded.Enabled.ShouldBe(resource.Enabled);
        loaded.ShowInDiscoveryDocument.ShouldBe(resource.ShowInDiscoveryDocument);
        loaded.RequireResourceIndicator.ShouldBe(resource.RequireResourceIndicator);
        loaded.UserClaims.ShouldNotBeNull();
        loaded.UserClaims.ShouldBe(resource.UserClaims);
        loaded.Scopes.ShouldNotBeNull();
        loaded.Scopes.ShouldBe(resource.Scopes);
        loaded.AllowedAccessTokenSigningAlgorithms.ShouldNotBeNull();
        loaded.AllowedAccessTokenSigningAlgorithms.ShouldBe(resource.AllowedAccessTokenSigningAlgorithms);
    }

    [Fact]
    public async Task create_and_get_by_name_round_trips()
    {
        var admin = NewAdmin();
        var name = $"api_{Guid.NewGuid():N}";
        var resource = new ApiResourceConfiguration
        {
            Name = name,
            DisplayName = "ByName Test"
        };

        var createResult = await admin.CreateAsync(resource, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetByNameAsync(name, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item.Name.ShouldBe(name);
        getResult.Item.DisplayName.ShouldBe("ByName Test");
    }

    [Fact]
    public async Task create_returns_storage_id_and_version()
    {
        var admin = NewAdmin();
        var resource = new ApiResourceConfiguration { Name = $"api_{Guid.NewGuid():N}" };

        var result = await admin.CreateAsync(resource, _ct);

        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");
        result.Id.ShouldNotBe(Guid.Empty);
        result.Version.ShouldNotBeNull();
        result.Version.Value.ShouldBe(1);
    }

    [Fact]
    public async Task create_duplicate_name_returns_already_exists()
    {
        var admin = NewAdmin();
        var name = $"api_{Guid.NewGuid():N}";

        var first = await admin.CreateAsync(new ApiResourceConfiguration { Name = name }, _ct);
        first.IsSuccess.ShouldBeTrue();

        var second = await admin.CreateAsync(new ApiResourceConfiguration { Name = name }, _ct);
        second.IsSuccess.ShouldBeFalse();
        second.Errors.ShouldNotBeNull();
        second.Errors.ShouldContain(e => e.Code == "already_exists");
    }

    [Fact]
    public async Task update_changes_applied_on_read()
    {
        var admin = NewAdmin();
        var resource = new ApiResourceConfiguration
        {
            Name = $"api_{Guid.NewGuid():N}",
            DisplayName = "Original",
            Description = "Original description"
        };

        var createResult = await admin.CreateAsync(resource, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var toUpdate = getResult.Item;
        toUpdate.DisplayName = "Updated";
        toUpdate.Description = "Updated description";
        toUpdate.Enabled = false;

        var updateResult = await admin.UpdateAsync(createResult.Id, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var afterUpdate = await admin.GetAsync(createResult.Id, _ct);
        afterUpdate.Found.ShouldBeTrue();
        afterUpdate.Item.DisplayName.ShouldBe("Updated");
        afterUpdate.Item.Description.ShouldBe("Updated description");
        afterUpdate.Item.Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task update_with_wrong_version_returns_version_conflict()
    {
        var admin = NewAdmin();
        var resource = new ApiResourceConfiguration { Name = $"api_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(resource, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var wrongVersion = (DataVersion)999;
        var updateResult = await admin.UpdateAsync(createResult.Id, getResult.Item, wrongVersion, _ct);

        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors.ShouldNotBeNull();
        updateResult.Errors.ShouldContain(e => e.Code == "version_conflict");
    }

    [Fact]
    public async Task update_nonexistent_returns_not_found()
    {
        var admin = NewAdmin();
        var nonExistentId = UuidV7.New().Value;
        var resource = new ApiResourceConfiguration { Name = $"api_{Guid.NewGuid():N}" };

        var result = await admin.UpdateAsync(nonExistentId, resource, (DataVersion)1, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "not_found");
    }

    [Fact]
    public async Task update_rename_to_existing_name_returns_already_exists()
    {
        var admin = NewAdmin();
        var nameA = $"api_{Guid.NewGuid():N}";
        var nameB = $"api_{Guid.NewGuid():N}";

        await CreateResourceAsync(admin, nameA);
        var idB = await CreateResourceAsync(admin, nameB);

        var getB = await admin.GetAsync(idB, _ct);
        getB.Found.ShouldBeTrue();
        var configB = getB.Item;
        configB.Name = nameA; // rename B to A's name

        var updateResult = await admin.UpdateAsync(idB, configB, getB.Version!, _ct);
        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors.ShouldContain(e => e.Code == "already_exists");
    }

    [Fact]
    public async Task delete_then_get_returns_not_found()
    {
        var admin = NewAdmin();
        var resource = new ApiResourceConfiguration { Name = $"api_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(resource, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var deleteResult = await admin.DeleteAsync(createResult.Id, _ct);
        deleteResult.IsSuccess.ShouldBeTrue($"Delete failed: {deleteResult}");

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task create_with_empty_name_returns_required_error()
    {
        var admin = NewAdmin();
        var resource = new ApiResourceConfiguration { Name = "" };

        var result = await admin.CreateAsync(resource, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "required" && e.PropertyNames != null && e.PropertyNames.Contains("Name"));
    }

    [Fact]
    public async Task query_by_name_filter_returns_matching()
    {
        var uniquePart = $"q_{Guid.NewGuid():N}";
        var admin = NewAdmin();

        await admin.CreateAsync(new ApiResourceConfiguration { Name = uniquePart + "_match1" }, _ct);
        await admin.CreateAsync(new ApiResourceConfiguration { Name = uniquePart + "_match2" }, _ct);
        await admin.CreateAsync(new ApiResourceConfiguration { Name = $"other_{Guid.NewGuid():N}" }, _ct);

        var result = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(
                new ApiResourceFilter { Name = uniquePart }),
            _ct);

        result.Items.ShouldAllBe(r => r.Name.Contains(uniquePart));
        result.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task query_by_enabled_filter_returns_matching()
    {
        var admin = NewAdmin();
        var enabledName = $"q_enabled_{Guid.NewGuid():N}";
        var disabledName = $"q_disabled_{Guid.NewGuid():N}";

        await admin.CreateAsync(new ApiResourceConfiguration { Name = enabledName, Enabled = true }, _ct);
        await admin.CreateAsync(new ApiResourceConfiguration { Name = disabledName, Enabled = false }, _ct);

        var enabledResult = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(
                new ApiResourceFilter { Name = enabledName, Enabled = true }),
            _ct);

        enabledResult.Items.ShouldContain(r => r.Name == enabledName);
        enabledResult.Items.ShouldNotContain(r => r.Name == disabledName);
    }

    [Fact]
    public async Task query_by_scope_filter_returns_matching()
    {
        var scopeAdmin = NewScopeAdmin();
        var uniqueScope = $"scope_{Guid.NewGuid():N}";
        var otherScope = $"scope_other_{Guid.NewGuid():N}";
        var otherScope1 = $"scope_other1_{Guid.NewGuid():N}";
        var otherScope2 = $"scope_other2_{Guid.NewGuid():N}";
        (await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = uniqueScope }, _ct)).IsSuccess.ShouldBeTrue();
        (await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = otherScope }, _ct)).IsSuccess.ShouldBeTrue();
        (await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = otherScope1 }, _ct)).IsSuccess.ShouldBeTrue();
        (await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = otherScope2 }, _ct)).IsSuccess.ShouldBeTrue();

        var admin = NewAdmin();
        var withScopeName = $"q_withscope_{Guid.NewGuid():N}";
        var withoutScopeName = $"q_noscope_{Guid.NewGuid():N}";

        await admin.CreateAsync(new ApiResourceConfiguration { Name = withScopeName, Scopes = [uniqueScope, otherScope] }, _ct);
        await admin.CreateAsync(new ApiResourceConfiguration { Name = withoutScopeName, Scopes = [otherScope1, otherScope2] }, _ct);

        var result = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(
                new ApiResourceFilter { Scope = uniqueScope }),
            _ct);

        result.Items.ShouldContain(r => r.Name == withScopeName);
        result.Items.ShouldNotContain(r => r.Name == withoutScopeName);
    }

    [Fact]
    public async Task query_with_pagination_returns_correct_page()
    {
        var prefix = $"q_page_{Guid.NewGuid():N}_";
        var admin = NewAdmin();

        for (var i = 0; i < 5; i++)
        {
            await admin.CreateAsync(new ApiResourceConfiguration { Name = prefix + i }, _ct);
        }

        var page1 = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(
                new ApiResourceFilter { Name = prefix },
                (DataRange)DataRange.FromPage(1, (DataRangeSize)2)),
            _ct);

        page1.Items.Count.ShouldBe(2);

        var page2 = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(
                new ApiResourceFilter { Name = prefix },
                (DataRange)DataRange.FromPage(2, (DataRangeSize)2)),
            _ct);

        page2.Items.Count.ShouldBe(2);

        var page1Ids = page1.Items.Select(r => r.Id).ToHashSet();
        var page2Ids = page2.Items.Select(r => r.Id).ToHashSet();
        page1Ids.Intersect(page2Ids).ShouldBeEmpty();
    }

    [Fact]
    public async Task create_secret_hashes_value_sha256()
    {
        var admin = NewAdmin();
        var resourceId = await CreateResourceAsync(admin);

        const string plaintext = "my-api-secret";
        var secretResult = await admin.CreateSecretAsync(
            resourceId, plaintext, SecretHashAlgorithm.Sha256, null, null, null, _ct);

        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var expectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));

        var resourceStore = NewResourceStore();
        var getResult = await admin.GetAsync(resourceId, _ct);
        getResult.Found.ShouldBeTrue();

        var resources = await resourceStore.FindApiResourcesByNameAsync([getResult.Item.Name], _ct);
        resources.ShouldHaveSingleItem();
        resources.First().ApiSecrets.ShouldHaveSingleItem();
        resources.First().ApiSecrets.First().Value.ShouldBe(expectedHash);
    }

    [Fact]
    public async Task create_secret_hashes_value_sha512()
    {
        var admin = NewAdmin();
        var resourceId = await CreateResourceAsync(admin);

        const string plaintext = "sha512-api-secret";
        var secretResult = await admin.CreateSecretAsync(
            resourceId, plaintext, SecretHashAlgorithm.Sha512, "sha512 test", null, null, _ct);

        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret (SHA512) failed: {secretResult}");
        secretResult.Id.ShouldNotBe(Guid.Empty);

        var expectedHash = Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(plaintext)));

        var getResult = await admin.GetAsync(resourceId, _ct);
        getResult.Found.ShouldBeTrue();

        var resourceStore = NewResourceStore();
        var resources = await resourceStore.FindApiResourcesByNameAsync([getResult.Item.Name], _ct);
        resources.ShouldHaveSingleItem();
        resources.First().ApiSecrets.ShouldHaveSingleItem();
        resources.First().ApiSecrets.First().Value.ShouldBe(expectedHash);
    }

    [Fact]
    public async Task get_does_not_expose_secret_value()
    {
        var admin = NewAdmin();
        var resourceId = await CreateResourceAsync(admin);

        await admin.CreateSecretAsync(resourceId, "super-secret", SecretHashAlgorithm.Sha256, null, null, null, _ct);

        var getResult = await admin.GetAsync(resourceId, _ct);
        getResult.Found.ShouldBeTrue();

        var secrets = getResult.Item.ApiSecrets;
        secrets.ShouldNotBeNull();
        secrets.ShouldNotBeEmpty();

        var secret = secrets.First();
        secret.Id.ShouldNotBe(Guid.Empty);
        secret.Type.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task create_secret_with_empty_value_returns_required_error()
    {
        var admin = NewAdmin();
        var resourceId = await CreateResourceAsync(admin);

        var result = await admin.CreateSecretAsync(resourceId, "", null, null, null, null, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "required");
    }

    [Fact]
    public async Task delete_secret_removes_it()
    {
        var admin = NewAdmin();
        var resourceId = await CreateResourceAsync(admin);

        var secretResult = await admin.CreateSecretAsync(resourceId, "delete-me", SecretHashAlgorithm.Sha256, null, null, null, _ct);
        secretResult.IsSuccess.ShouldBeTrue();

        var deleteResult = await admin.DeleteSecretAsync(resourceId, secretResult.Id, _ct);
        deleteResult.IsSuccess.ShouldBeTrue($"DeleteSecret failed: {deleteResult}");

        var getResult = await admin.GetAsync(resourceId, _ct);
        getResult.Found.ShouldBeTrue();
        var secrets = getResult.Item.ApiSecrets;
        secrets.ShouldNotBeNull();
        secrets.ShouldNotContain(s => s.Id == secretResult.Id);
    }

    [Fact]
    public async Task delete_nonexistent_secret_returns_not_found()
    {
        var admin = NewAdmin();
        var resourceId = await CreateResourceAsync(admin);

        var result = await admin.DeleteSecretAsync(resourceId, Guid.NewGuid(), _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "not_found");
    }

    [Fact]
    public async Task update_with_fabricated_secret_id_returns_validation_error()
    {
        var admin = NewAdmin();
        var resource = new ApiResourceConfiguration { Name = $"api_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(resource, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var toUpdate = getResult.Item;
        toUpdate.ApiSecrets =
        [
            new ApiResourceSecretConfiguration
            {
                Id = Guid.NewGuid(),
                Type = "SharedSecret"
            }
        ];

        var updateResult = await admin.UpdateAsync(createResult.Id, toUpdate, getResult.Version!, _ct);

        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors.ShouldNotBeNull();
        updateResult.Errors.ShouldContain(e => e.Code == "invalid_value");
    }

    [Fact]
    public async Task create_with_scopes_updates_scope_back_references()
    {
        var scopeAdmin = NewScopeAdmin();
        var scopeName = $"scope_{Guid.NewGuid():N}";
        var createScopeResult = await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = scopeName }, _ct);
        createScopeResult.IsSuccess.ShouldBeTrue();
        var scopeId = createScopeResult.Id;

        var admin = NewAdmin();
        var resourceName = $"api_{Guid.NewGuid():N}";
        var createResult = await admin.CreateAsync(new ApiResourceConfiguration { Name = resourceName, Scopes = [scopeName] }, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var scopeDso = await ReadScopeDsoAsync(scopeId);
        scopeDso.ShouldNotBeNull();
        scopeDso.ReferencedByApiResources.ShouldContain(r => r.Name == resourceName);
    }

    [Fact]
    public async Task update_scope_changes_updates_back_references()
    {
        var scopeAdmin = NewScopeAdmin();
        var scopeNameA = $"scope_a_{Guid.NewGuid():N}";
        var scopeNameB = $"scope_b_{Guid.NewGuid():N}";
        var scopeNameC = $"scope_c_{Guid.NewGuid():N}";
        var createA = await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = scopeNameA }, _ct);
        var createB = await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = scopeNameB }, _ct);
        var createC = await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = scopeNameC }, _ct);
        createA.IsSuccess.ShouldBeTrue();
        createB.IsSuccess.ShouldBeTrue();
        createC.IsSuccess.ShouldBeTrue();
        var scopeIdA = createA.Id;
        var scopeIdB = createB.Id;
        var scopeIdC = createC.Id;

        var admin = NewAdmin();
        var resourceName = $"api_{Guid.NewGuid():N}";
        var createResult = await admin.CreateAsync(
            new ApiResourceConfiguration { Name = resourceName, Scopes = [scopeNameA, scopeNameB] },
            _ct);
        createResult.IsSuccess.ShouldBeTrue();
        var resourceId = createResult.Id;

        var getResult = await admin.GetAsync(resourceId, _ct);
        getResult.Found.ShouldBeTrue();
        var toUpdate = getResult.Item;
        toUpdate.Scopes = [scopeNameB, scopeNameC];
        var updateResult = await admin.UpdateAsync(resourceId, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var scopeDsoA = await ReadScopeDsoAsync(scopeIdA);
        scopeDsoA.ShouldNotBeNull();
        scopeDsoA.ReferencedByApiResources.ShouldNotContain(r => r.Name == resourceName);

        var scopeDsoB = await ReadScopeDsoAsync(scopeIdB);
        scopeDsoB.ShouldNotBeNull();
        scopeDsoB.ReferencedByApiResources.ShouldContain(r => r.Name == resourceName);

        var scopeDsoC = await ReadScopeDsoAsync(scopeIdC);
        scopeDsoC.ShouldNotBeNull();
        scopeDsoC.ReferencedByApiResources.ShouldContain(r => r.Name == resourceName);
    }

    [Fact]
    public async Task delete_resource_cleans_up_scope_back_references()
    {
        var scopeAdmin = NewScopeAdmin();
        var scopeName = $"scope_{Guid.NewGuid():N}";
        var createScopeResult = await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = scopeName }, _ct);
        createScopeResult.IsSuccess.ShouldBeTrue();
        var scopeId = createScopeResult.Id;

        var admin = NewAdmin();
        var resourceName = $"api_{Guid.NewGuid():N}";
        var createResult = await admin.CreateAsync(
            new ApiResourceConfiguration { Name = resourceName, Scopes = [scopeName] },
            _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var scopeDsoBefore = await ReadScopeDsoAsync(scopeId);
        scopeDsoBefore.ShouldNotBeNull();
        scopeDsoBefore.ReferencedByApiResources.ShouldContain(r => r.Name == resourceName);

        var deleteResult = await admin.DeleteAsync(createResult.Id, _ct);
        deleteResult.IsSuccess.ShouldBeTrue($"Delete failed: {deleteResult}");

        var scopeDsoAfter = await ReadScopeDsoAsync(scopeId);
        scopeDsoAfter.ShouldNotBeNull();
        scopeDsoAfter.ReferencedByApiResources.ShouldNotContain(r => r.Name == resourceName);
    }

    [Fact]
    public async Task create_with_nonexistent_scope_returns_error()
    {
        var admin = NewAdmin();
        var nonExistentScopeName = $"scope_does_not_exist_{Guid.NewGuid():N}";

        var result = await admin.CreateAsync(
            new ApiResourceConfiguration { Name = $"api_{Guid.NewGuid():N}", Scopes = [nonExistentScopeName] },
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "invalid_value"
            && e.PropertyNames != null && e.PropertyNames.Contains("Scopes"));
    }

    [Fact]
    public async Task rename_resource_updates_back_reference_names()
    {
        var scopeAdmin = NewScopeAdmin();
        var scopeName = $"scope_{Guid.NewGuid():N}";
        var createScopeResult = await scopeAdmin.CreateAsync(new ApiScopeConfiguration { Name = scopeName }, _ct);
        createScopeResult.IsSuccess.ShouldBeTrue();
        var scopeId = createScopeResult.Id;

        var admin = NewAdmin();
        var originalName = $"api_{Guid.NewGuid():N}";
        var createResult = await admin.CreateAsync(
            new ApiResourceConfiguration { Name = originalName, Scopes = [scopeName] },
            _ct);
        createResult.IsSuccess.ShouldBeTrue();
        var resourceId = createResult.Id;

        var getResult = await admin.GetAsync(resourceId, _ct);
        getResult.Found.ShouldBeTrue();
        var toUpdate = getResult.Item;
        var newName = $"api_renamed_{Guid.NewGuid():N}";
        toUpdate.Name = newName;
        var updateResult = await admin.UpdateAsync(resourceId, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Rename failed: {updateResult}");

        var scopeDso = await ReadScopeDsoAsync(scopeId);
        scopeDso.ShouldNotBeNull();
        scopeDso.ReferencedByApiResources.ShouldNotContain(r => r.Name == originalName);
        scopeDso.ReferencedByApiResources.ShouldContain(r => r.Name == newName);
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
