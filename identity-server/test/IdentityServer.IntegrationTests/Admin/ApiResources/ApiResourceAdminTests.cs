// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.ApiResources;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.ApiResources;
using Duende.Storage;
using Microsoft.IdentityModel.Tokens;

using Duende.IdentityServer.Models;
using DataVersion = Duende.IdentityServer.Models.DataVersion;

namespace Duende.IdentityServer.IntegrationTests.Admin;

public sealed class ApiResourceAdminTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private async Task<ApiResourceDso.V1?> ReadResourceDsoAsync(ApiResourceId resourceId)
    {
        var partitionedStorage = await _fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Configuration, _ct);
        var result = await partitionedStorage.TryReadAsync(ApiResourceDso.EntityType, UuidV7.From(resourceId.Value), _ct);
        return result.Found ? ApiResourceDso.ToCurrent(result.Dso) : null;
    }

    private async Task<ApiResourceId> CreateResourceAsync(IApiResourceAdmin admin, string? name = null, Ct ct = default)
    {
        var result = await admin.CreateAsync(
            new CreateApiResource { Name = name ?? $"api_{Guid.NewGuid():N}" },
            ct == default ? _ct : ct);
        result.IsSuccess.ShouldBeTrue($"CreateResource failed: {result}");
        return result.Id!;
    }

    [Fact]
    public async Task create_and_get_by_id_round_trips_all_fields()
    {
        var scopeAdmin = _fixture.ApiScopeAdmin;
        var readScopeName = $"api1_read_{Guid.NewGuid():N}";
        var writeScopeName = $"api1_write_{Guid.NewGuid():N}";
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = readScopeName }, _ct)).IsSuccess.ShouldBeTrue();
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = writeScopeName }, _ct)).IsSuccess.ShouldBeTrue();

        var admin = _fixture.ApiResourceAdmin;
        var resource = new CreateApiResource
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

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var loaded = getResult.Item!;
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
        var admin = _fixture.ApiResourceAdmin;
        var name = $"api_{Guid.NewGuid():N}";
        var resource = new CreateApiResource
        {
            Name = name,
            DisplayName = "ByName Test"
        };

        var createResult = await admin.CreateAsync(resource, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetByNameAsync(name, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.Name.ShouldBe(name);
        getResult.Item!.DisplayName.ShouldBe("ByName Test");
    }

    [Fact]
    public async Task create_returns_storage_id_and_version()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resource = new CreateApiResource { Name = $"api_{Guid.NewGuid():N}" };

        var result = await admin.CreateAsync(resource, _ct);

        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");
        result.Id!.Value.ShouldNotBe(Guid.Empty);
        result.Version!.ShouldNotBeNull();
        result.Version!.Value.ShouldBe(1);
    }

    [Fact]
    public async Task create_duplicate_name_returns_already_exists()
    {
        var admin = _fixture.ApiResourceAdmin;
        var name = $"api_{Guid.NewGuid():N}";

        var first = await admin.CreateAsync(new CreateApiResource { Name = name }, _ct);
        first.IsSuccess.ShouldBeTrue();

        var second = await admin.CreateAsync(new CreateApiResource { Name = name }, _ct);
        second.IsSuccess.ShouldBeFalse();
        second.Errors.ShouldNotBeNull();
        second.Errors!.ShouldContain(e => e.Code == "already_exists");
    }

    [Fact]
    public async Task update_changes_applied_on_read()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resource = new CreateApiResource
        {
            Name = $"api_{Guid.NewGuid():N}",
            DisplayName = "Original",
            Description = "Original description"
        };

        var createResult = await admin.CreateAsync(resource, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var toUpdate = getResult.Item!.ToUpdate();
        toUpdate.DisplayName = "Updated";
        toUpdate.Description = "Updated description";
        toUpdate.Enabled = false;

        var updateResult = await admin.UpdateAsync(createResult.Id!, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var afterUpdate = await admin.GetAsync(createResult.Id!, _ct);
        afterUpdate.Found.ShouldBeTrue();
        afterUpdate.Item!.DisplayName.ShouldBe("Updated");
        afterUpdate.Item!.Description.ShouldBe("Updated description");
        afterUpdate.Item!.Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task update_with_wrong_version_returns_version_conflict()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resource = new CreateApiResource { Name = $"api_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(resource, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var wrongVersion = (DataVersion)999;
        var updateResult = await admin.UpdateAsync(createResult.Id!, getResult.Item!.ToUpdate(), wrongVersion, _ct);

        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors.ShouldNotBeNull();
        updateResult.Errors!.ShouldContain(e => e.Code == "version_conflict");
    }

    [Fact]
    public async Task update_nonexistent_returns_not_found()
    {
        var admin = _fixture.ApiResourceAdmin;
        ApiResourceId nonExistentId = UuidV7.New().Value;
        var resource = new UpdateApiResource { Name = $"api_{Guid.NewGuid():N}" };

        var result = await admin.UpdateAsync(nonExistentId, resource, (DataVersion)1, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors!.ShouldContain(e => e.Code == "not_found");
    }

    [Fact]
    public async Task update_rename_to_existing_name_returns_already_exists()
    {
        var admin = _fixture.ApiResourceAdmin;
        var nameA = $"api_{Guid.NewGuid():N}";
        var nameB = $"api_{Guid.NewGuid():N}";

        await CreateResourceAsync(admin, nameA);
        var idB = await CreateResourceAsync(admin, nameB);

        var getB = await admin.GetAsync(idB, _ct);
        getB.Found.ShouldBeTrue();
        var configB = getB.Item!.ToUpdate();
        configB.Name = nameA; // rename B to A's name

        var updateResult = await admin.UpdateAsync(idB, configB, getB.Version!, _ct);
        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors!.ShouldContain(e => e.Code == "already_exists");
    }

    [Fact]
    public async Task delete_then_get_returns_not_found()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resource = new CreateApiResource { Name = $"api_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(resource, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var deleteResult = await admin.DeleteAsync(createResult.Id!, _ct);
        deleteResult.IsSuccess.ShouldBeTrue($"Delete failed: {deleteResult}");

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task create_with_empty_name_returns_required_error()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resource = new CreateApiResource { Name = "" };

        var result = await admin.CreateAsync(resource, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors!.ShouldContain(e => e.Code == "required" && e.PropertyNames != null && e.PropertyNames.Contains("Name"));
    }

    [Fact]
    public async Task query_by_name_filter_returns_matching()
    {
        var uniquePart = $"q_{Guid.NewGuid():N}";
        var admin = _fixture.ApiResourceAdmin;

        await admin.CreateAsync(new CreateApiResource { Name = uniquePart + "_match1" }, _ct);
        await admin.CreateAsync(new CreateApiResource { Name = uniquePart + "_match2" }, _ct);
        await admin.CreateAsync(new CreateApiResource { Name = $"other_{Guid.NewGuid():N}" }, _ct);

        var result = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(
                new ApiResourceFilter { Name = uniquePart }),
            _ct);

        result.Results.ShouldAllBe(r => r.Name.Contains(uniquePart));
        result.Results.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task query_by_enabled_filter_returns_matching()
    {
        var admin = _fixture.ApiResourceAdmin;
        var enabledName = $"q_enabled_{Guid.NewGuid():N}";
        var disabledName = $"q_disabled_{Guid.NewGuid():N}";

        await admin.CreateAsync(new CreateApiResource { Name = enabledName, Enabled = true }, _ct);
        await admin.CreateAsync(new CreateApiResource { Name = disabledName, Enabled = false }, _ct);

        var enabledResult = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(
                new ApiResourceFilter { Name = enabledName, Enabled = true }),
            _ct);

        enabledResult.Results.ShouldContain(r => r.Name == enabledName);
        enabledResult.Results.ShouldNotContain(r => r.Name == disabledName);
    }

    [Fact]
    public async Task query_by_scope_filter_returns_matching()
    {
        var scopeAdmin = _fixture.ApiScopeAdmin;
        var uniqueScope = $"scope_{Guid.NewGuid():N}";
        var otherScope = $"scope_other_{Guid.NewGuid():N}";
        var otherScope1 = $"scope_other1_{Guid.NewGuid():N}";
        var otherScope2 = $"scope_other2_{Guid.NewGuid():N}";
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = uniqueScope }, _ct)).IsSuccess.ShouldBeTrue();
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = otherScope }, _ct)).IsSuccess.ShouldBeTrue();
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = otherScope1 }, _ct)).IsSuccess.ShouldBeTrue();
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = otherScope2 }, _ct)).IsSuccess.ShouldBeTrue();

        var admin = _fixture.ApiResourceAdmin;
        var withScopeName = $"q_withscope_{Guid.NewGuid():N}";
        var withoutScopeName = $"q_noscope_{Guid.NewGuid():N}";

        await admin.CreateAsync(new CreateApiResource { Name = withScopeName, Scopes = [uniqueScope, otherScope] }, _ct);
        await admin.CreateAsync(new CreateApiResource { Name = withoutScopeName, Scopes = [otherScope1, otherScope2] }, _ct);

        var result = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(
                new ApiResourceFilter { Scope = uniqueScope }),
            _ct);

        result.Results.ShouldContain(r => r.Name == withScopeName);
        result.Results.ShouldNotContain(r => r.Name == withoutScopeName);
    }

    [Fact]
    public async Task query_by_scope_filter_follows_scope_rename()
    {
        var scopeAdmin = _fixture.ApiScopeAdmin;
        var originalName = $"scope_{Guid.NewGuid():N}";
        var newName = $"scope_{Guid.NewGuid():N}";
        var resourceName = $"api_{Guid.NewGuid():N}";
        var createScope = await scopeAdmin.CreateAsync(new CreateApiScope { Name = originalName }, _ct);
        createScope.IsSuccess.ShouldBeTrue();
        var admin = _fixture.ApiResourceAdmin;
        (await admin.CreateAsync(new CreateApiResource { Name = resourceName, Scopes = [originalName] }, _ct)).IsSuccess.ShouldBeTrue();

        var scope = await scopeAdmin.GetAsync(createScope.Id!, _ct);
        var toUpdate = scope.Item!.ToUpdate();
        toUpdate.Name = newName;
        (await scopeAdmin.UpdateAsync(createScope.Id!, toUpdate, scope.Version!, _ct)).IsSuccess.ShouldBeTrue();

        var byNewName = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(new ApiResourceFilter { Scope = newName }),
            _ct);
        byNewName.Results.ShouldContain(r => r.Name == resourceName);
        var byOriginalName = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(new ApiResourceFilter { Scope = originalName }),
            _ct);
        byOriginalName.Results.ShouldNotContain(r => r.Name == resourceName);
    }

    [Fact]
    public async Task query_by_scope_filter_is_exact_and_case_sensitive()
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

        var exactMatch = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(new ApiResourceFilter { Scope = scopeName }),
            _ct);
        exactMatch.Results.ShouldContain(r => r.Name == resourceName);
        exactMatch.Results.ShouldNotContain(r => r.Name == longerResourceName);

        var wrongCase = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(new ApiResourceFilter { Scope = scopeName.ToUpperInvariant() }),
            _ct);
        wrongCase.Results.ShouldNotContain(r => r.Name == resourceName);
    }

    [Fact]
    public async Task query_by_scope_filter_for_unknown_scope_returns_empty_with_valid_paging()
    {
        var admin = _fixture.ApiResourceAdmin;

        var result = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(
                new ApiResourceFilter { Scope = $"scope_does_not_exist_{Guid.NewGuid():N}" },
                DataRange.FromPage(1, 10)),
            _ct);

        result.Results.ShouldBeEmpty();
    }

    [Fact]
    public async Task query_by_scope_filter_pages_matching_resources()
    {
        var scopeAdmin = _fixture.ApiScopeAdmin;
        var admin = _fixture.ApiResourceAdmin;
        var scopeName = $"scope_page_{Guid.NewGuid():N}";
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = scopeName }, _ct)).IsSuccess.ShouldBeTrue();

        var prefix = $"api_page_{Guid.NewGuid():N}_";
        for (var i = 0; i < 5; i++)
        {
            (await admin.CreateAsync(new CreateApiResource { Name = prefix + i, Scopes = [scopeName] }, _ct)).IsSuccess.ShouldBeTrue();
        }

        var page1 = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(
                new ApiResourceFilter { Scope = scopeName },
                DataRange.FromPage(1, 2)),
            _ct);
        page1.Results.Count.ShouldBe(2);

        var page2 = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(
                new ApiResourceFilter { Scope = scopeName },
                DataRange.FromPage(2, 2)),
            _ct);
        page2.Results.Count.ShouldBe(2);

        var page1Ids = page1.Results.Select(r => r.Id!).ToHashSet();
        var page2Ids = page2.Results.Select(r => r.Id!).ToHashSet();
        page1Ids.Intersect(page2Ids).ShouldBeEmpty();
    }

    [Fact]
    public async Task query_by_scope_filter_pages_through_continuation_token_without_duplicates_or_gaps()
    {
        var scopeAdmin = _fixture.ApiScopeAdmin;
        var admin = _fixture.ApiResourceAdmin;
        var scopeName = $"scope_ctoken_{Guid.NewGuid():N}";
        var otherScopeName = $"scope_ctoken_other_{Guid.NewGuid():N}";
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = scopeName }, _ct)).IsSuccess.ShouldBeTrue();
        (await scopeAdmin.CreateAsync(new CreateApiScope { Name = otherScopeName }, _ct)).IsSuccess.ShouldBeTrue();

        var prefix = $"api_ctoken_{Guid.NewGuid():N}_";
        var expectedNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 7; i++)
        {
            var name = prefix + i.ToString("D2");
            (await admin.CreateAsync(new CreateApiResource { Name = name, Scopes = [scopeName] }, _ct)).IsSuccess.ShouldBeTrue();
            expectedNames.Add(name);
        }

        // A non-matching resource, linked to a different scope, to prove the filter (not just the
        // paging) stays exact across the whole walk.
        (await admin.CreateAsync(new CreateApiResource { Name = $"{prefix}not_matching", Scopes = [otherScopeName] }, _ct)).IsSuccess.ShouldBeTrue();

        var results = new List<ApiResourceListItem>();
        var range = (DataRange?)DataRange.FromContinuationToken(null, 3);
        var pageCount = 0;

        do
        {
            var page = await admin.QueryAsync(
                QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(new ApiResourceFilter { Scope = scopeName }, range),
                _ct);

            results.AddRange(page.Results);
            pageCount++;

            range = page.HasNextResults && page.ResultsToken is { } nextToken
                ? DataRange.FromContinuationToken(nextToken)
                : null;
        }
        while (range is not null);

        pageCount.ShouldBeGreaterThan(1);
        results.Select(r => r.Name).ToHashSet(StringComparer.Ordinal).ShouldBe(expectedNames);
        results.Select(r => r.Id).Distinct().Count().ShouldBe(expectedNames.Count);

        // Reusing the same (beginning) token returns the exact same page again.
        var firstPage = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(
                new ApiResourceFilter { Scope = scopeName },
                DataRange.FromContinuationToken(null, 3)),
            _ct);
        var firstPageAgain = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(
                new ApiResourceFilter { Scope = scopeName },
                DataRange.FromContinuationToken(null, 3)),
            _ct);
        firstPageAgain.Results.Select(r => r.Id).ShouldBe(firstPage.Results.Select(r => r.Id));
    }

    [Fact]
    public async Task create_batch_with_scope_that_disappeared_after_resolve_returns_scope_does_not_exist()
    {
        // Simulates a scope deleted (or never existing) between the admin's name resolution and the
        // batch actually running, by driving the repository directly with a (name, id) pair for a
        // scope id that was never created: the link operation in the batch fails with DoesNotExist
        // exactly as it would for a concurrently deleted scope, without a production test seam.
        var repository = _fixture.ApiResourceRepository;
        var resourceName = $"api_{Guid.NewGuid():N}";
        var vanishedScopeName = $"scope_{Guid.NewGuid():N}";
        var vanishedScopeId = UuidV7.New().Value;
        var id = UuidV7.New();
        var dso = BuildApiResourceDso(id.Value, resourceName);

        var outcome = await repository.CreateAsync(id, dso, [(vanishedScopeName, vanishedScopeId)], _ct);

        outcome.Kind.ShouldBe(ApiResourceBatchOutcomeKind.ScopeDoesNotExist);
        outcome.ScopeName.ShouldBe(vanishedScopeName);

        // Nothing was saved: the whole batch, including the resource create, rolled back.
        (await repository.TryReadByNameAsync(resourceName, _ct)).ShouldBeNull();
    }

    [Fact]
    public async Task update_batch_with_scope_that_disappeared_after_resolve_returns_scope_does_not_exist()
    {
        var repository = _fixture.ApiResourceRepository;
        var resourceName = $"api_{Guid.NewGuid():N}";
        var id = UuidV7.New();
        var originalDso = BuildApiResourceDso(id.Value, resourceName);
        (await repository.CreateAsync(id, originalDso, [], _ct)).Kind.ShouldBe(ApiResourceBatchOutcomeKind.Success);

        var vanishedScopeName = $"scope_{Guid.NewGuid():N}";
        var vanishedScopeId = UuidV7.New().Value;
        var attemptedDso = originalDso with { Enabled = false };

        var outcome = await repository.UpdateAsync(
            id,
            attemptedDso,
            expectedVersion: 1,
            scopesToLink: [(vanishedScopeName, vanishedScopeId)],
            scopesToUnlink: [],
            _ct);

        outcome.Kind.ShouldBe(ApiResourceBatchOutcomeKind.ScopeDoesNotExist);
        outcome.ScopeName.ShouldBe(vanishedScopeName);

        // Nothing was saved: version, fields and scopes are all unchanged.
        var current = await repository.TryReadByIdAsync(id.Value, _ct);
        current.ShouldNotBeNull();
        current!.Value.Version.ShouldBe(1);
        current.Value.Dso.Enabled.ShouldBeTrue();
        (await repository.GetScopesAsync(id.Value, _ct)).ShouldBeEmpty();
    }

    private static ApiResourceDso.V1 BuildApiResourceDso(Guid id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Enabled = true,
            ShowInDiscoveryDocument = true,
            RequireResourceIndicator = false,
            UserClaims = [],
            AllowedAccessTokenSigningAlgorithms = [],
            ApiSecrets = []
        };

    [Fact]
    public async Task query_returns_scope_count_of_linked_scopes()
    {
        var scopeName1 = await CreateScopeAsync("scope1");
        var scopeName2 = await CreateScopeAsync("scope2");
        var admin = _fixture.ApiResourceAdmin;
        var resourceName = $"api_{Guid.NewGuid():N}";
        (await admin.CreateAsync(new CreateApiResource { Name = resourceName, Scopes = [scopeName1, scopeName2] }, _ct)).IsSuccess.ShouldBeTrue();

        var result = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(new ApiResourceFilter { Name = resourceName }),
            _ct);

        result.Results.ShouldHaveSingleItem().ScopeCount.ShouldBe(2);
    }

    [Fact]
    public async Task query_returns_scope_count_per_resource_with_shared_scopes_and_resources_without_scopes()
    {
        var shared = await CreateScopeAsync("shared");
        var only1 = await CreateScopeAsync("only1");
        var admin = _fixture.ApiResourceAdmin;
        var prefix = $"api_{Guid.NewGuid():N}";
        (await admin.CreateAsync(new CreateApiResource { Name = $"{prefix}_1", Scopes = [shared, only1] }, _ct)).IsSuccess.ShouldBeTrue();
        (await admin.CreateAsync(new CreateApiResource { Name = $"{prefix}_2", Scopes = [shared] }, _ct)).IsSuccess.ShouldBeTrue();
        (await admin.CreateAsync(new CreateApiResource { Name = $"{prefix}_3", Scopes = [] }, _ct)).IsSuccess.ShouldBeTrue();

        var result = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(new ApiResourceFilter { Name = prefix }),
            _ct);

        result.Results.ToDictionary(r => r.Name, r => r.ScopeCount).ShouldBe(new Dictionary<string, int>
        {
            [$"{prefix}_1"] = 2,
            [$"{prefix}_2"] = 1,
            [$"{prefix}_3"] = 0,
        });
    }

    [Fact]
    public async Task query_with_pagination_returns_correct_page()
    {
        var prefix = $"q_page_{Guid.NewGuid():N}_";
        var admin = _fixture.ApiResourceAdmin;

        for (var i = 0; i < 5; i++)
        {
            await admin.CreateAsync(new CreateApiResource { Name = prefix + i }, _ct);
        }

        var page1 = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(
                new ApiResourceFilter { Name = prefix },
                DataRange.FromPage(1, 2)),
            _ct);

        page1.Results.Count.ShouldBe(2);

        var page2 = await admin.QueryAsync(
            QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(
                new ApiResourceFilter { Name = prefix },
                DataRange.FromPage(2, 2)),
            _ct);

        page2.Results.Count.ShouldBe(2);

        var page1Ids = page1.Results.Select(r => r.Id!).ToHashSet();
        var page2Ids = page2.Results.Select(r => r.Id!).ToHashSet();
        page1Ids.Intersect(page2Ids).ShouldBeEmpty();
    }

    [Fact]
    public async Task create_secret_hashes_value_sha256()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resourceId = await CreateResourceAsync(admin);

        const string plaintext = "my-api-secret";
        var secretResult = await admin.CreateSecretAsync(
            resourceId, plaintext, SecretHashAlgorithm.Sha256, null, null, null, _ct);

        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var expectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));

        var resourceStore = _fixture.ResourceStore;
        var getResult = await admin.GetAsync(resourceId, _ct);
        getResult.Found.ShouldBeTrue();

        var resources = await resourceStore.FindApiResourcesByNameAsync([getResult.Item!.Name], _ct);
        resources.ShouldHaveSingleItem();
        resources.First().ApiSecrets.ShouldHaveSingleItem();
        resources.First().ApiSecrets.First().Value.ShouldBe(expectedHash);
    }

    [Fact]
    public async Task create_secret_hashes_value_sha512()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resourceId = await CreateResourceAsync(admin);

        const string plaintext = "sha512-api-secret";
        var secretResult = await admin.CreateSecretAsync(
            resourceId, plaintext, SecretHashAlgorithm.Sha512, "sha512 test", null, null, _ct);

        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret (SHA512) failed: {secretResult}");
        secretResult.Id!.Value.ShouldNotBe(Guid.Empty);

        var expectedHash = Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(plaintext)));

        var getResult = await admin.GetAsync(resourceId, _ct);
        getResult.Found.ShouldBeTrue();

        var resourceStore = _fixture.ResourceStore;
        var resources = await resourceStore.FindApiResourcesByNameAsync([getResult.Item!.Name], _ct);
        resources.ShouldHaveSingleItem();
        resources.First().ApiSecrets.ShouldHaveSingleItem();
        resources.First().ApiSecrets.First().Value.ShouldBe(expectedHash);
    }

    [Fact]
    public async Task create_secret_stores_x509_thumbprint_verbatim()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resourceId = await CreateResourceAsync(admin);

        using var cert = TestCert.Load();
        var plaintext = cert.Thumbprint;
        plaintext.ShouldNotBeNullOrEmpty();

        var secretResult = await admin.CreateSecretAsync(
            resourceId, plaintext!, null, null, null, IdentityServerConstants.SecretTypes.X509CertificateThumbprint, _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var dso = await ReadResourceDsoAsync(resourceId);
        dso.ShouldNotBeNull();
        dso.ApiSecrets.ShouldHaveSingleItem();
        var secret = dso.ApiSecrets[0];
        secret.Value.ShouldBe(plaintext);
        secret.HashAlgorithm.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task create_secret_stores_x509_name_verbatim()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resourceId = await CreateResourceAsync(admin);

        using var cert = TestCert.Load();
        var plaintext = cert.Subject;
        plaintext.ShouldNotBeNullOrEmpty();

        var secretResult = await admin.CreateSecretAsync(
            resourceId, plaintext, null, null, null, IdentityServerConstants.SecretTypes.X509CertificateName, _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var dso = await ReadResourceDsoAsync(resourceId);
        dso.ShouldNotBeNull();
        dso.ApiSecrets.ShouldHaveSingleItem();
        var secret = dso.ApiSecrets[0];
        secret.Value.ShouldBe(plaintext);
        secret.HashAlgorithm.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task create_secret_stores_x509_certificate_base64_verbatim()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resourceId = await CreateResourceAsync(admin);

        var plaintext = CreateBase64Certificate();

        var secretResult = await admin.CreateSecretAsync(
            resourceId, plaintext, null, null, null, IdentityServerConstants.SecretTypes.X509CertificateBase64, _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var dso = await ReadResourceDsoAsync(resourceId);
        dso.ShouldNotBeNull();
        dso.ApiSecrets.ShouldHaveSingleItem();
        var secret = dso.ApiSecrets[0];
        secret.Value.ShouldBe(plaintext);
        secret.HashAlgorithm.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task create_secret_stores_jwk_verbatim()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resourceId = await CreateResourceAsync(admin);

        var plaintext = CreatePublicJwkJson();

        var secretResult = await admin.CreateSecretAsync(
            resourceId, plaintext, null, null, null, IdentityServerConstants.SecretTypes.JsonWebKey, _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var dso = await ReadResourceDsoAsync(resourceId);
        dso.ShouldNotBeNull();
        dso.ApiSecrets.ShouldHaveSingleItem();
        var secret = dso.ApiSecrets[0];
        secret.Value.ShouldBe(plaintext);
        secret.HashAlgorithm.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task create_secret_hashes_shared_secret_type()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resourceId = await CreateResourceAsync(admin);

        const string plaintext = "shared-secret-value";
        var secretResult = await admin.CreateSecretAsync(
            resourceId, plaintext, null, null, null, IdentityServerConstants.SecretTypes.SharedSecret, _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var expectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));

        var resourceStore = _fixture.ResourceStore;
        var getResult = await admin.GetAsync(resourceId, _ct);
        getResult.Found.ShouldBeTrue();

        var resources = await resourceStore.FindApiResourcesByNameAsync([getResult.Item!.Name], _ct);
        resources.ShouldHaveSingleItem();
        resources.First().ApiSecrets.ShouldHaveSingleItem();
        resources.First().ApiSecrets.First().Value.ShouldBe(expectedHash);
    }

    [Fact]
    public async Task create_secret_hashes_null_type()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resourceId = await CreateResourceAsync(admin);

        const string plaintext = "null-type-secret-value";
        var secretResult = await admin.CreateSecretAsync(
            resourceId, plaintext, null, null, null, null, _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var expectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));

        var resourceStore = _fixture.ResourceStore;
        var getResult = await admin.GetAsync(resourceId, _ct);
        getResult.Found.ShouldBeTrue();

        var resources = await resourceStore.FindApiResourcesByNameAsync([getResult.Item!.Name], _ct);
        resources.ShouldHaveSingleItem();
        resources.First().ApiSecrets.ShouldHaveSingleItem();
        resources.First().ApiSecrets.First().Value.ShouldBe(expectedHash);
    }

    [Fact]
    public async Task create_secret_hashes_custom_type()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resourceId = await CreateResourceAsync(admin);

        const string plaintext = "custom-type-secret-value";
        var secretResult = await admin.CreateSecretAsync(
            resourceId, plaintext, null, null, null, "my-custom-type", _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var expectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));

        var resourceStore = _fixture.ResourceStore;
        var getResult = await admin.GetAsync(resourceId, _ct);
        getResult.Found.ShouldBeTrue();

        var resources = await resourceStore.FindApiResourcesByNameAsync([getResult.Item!.Name], _ct);
        resources.ShouldHaveSingleItem();
        resources.First().ApiSecrets.ShouldHaveSingleItem();
        resources.First().ApiSecrets.First().Value.ShouldBe(expectedHash);
    }

    [Fact]
    public async Task get_does_not_expose_secret_value()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resourceId = await CreateResourceAsync(admin);

        await admin.CreateSecretAsync(resourceId, "super-secret", SecretHashAlgorithm.Sha256, null, null, null, _ct);

        var getResult = await admin.GetAsync(resourceId, _ct);
        getResult.Found.ShouldBeTrue();

        var secrets = getResult.Item!.ApiSecrets;
        secrets.ShouldNotBeNull();
        secrets.ShouldNotBeEmpty();

        var secret = secrets[0];
        secret.Id!.Value.ShouldNotBe(Guid.Empty);
        secret.Type.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task create_secret_with_empty_value_returns_required_error()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resourceId = await CreateResourceAsync(admin);

        var result = await admin.CreateSecretAsync(resourceId, "", null, null, null, null, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors!.ShouldContain(e => e.Code == "required");
    }

    [Fact]
    public async Task delete_secret_removes_it()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resourceId = await CreateResourceAsync(admin);

        var secretResult = await admin.CreateSecretAsync(resourceId, "delete-me", SecretHashAlgorithm.Sha256, null, null, null, _ct);
        secretResult.IsSuccess.ShouldBeTrue();

        var deleteResult = await admin.DeleteSecretAsync(resourceId, secretResult.Id!, _ct);
        deleteResult.IsSuccess.ShouldBeTrue($"DeleteSecret failed: {deleteResult}");

        var getResult = await admin.GetAsync(resourceId, _ct);
        getResult.Found.ShouldBeTrue();
        var secrets = getResult.Item!.ApiSecrets;
        secrets.ShouldNotBeNull();
        secrets.ShouldNotContain(s => s.Id! == secretResult.Id!);
    }

    [Fact]
    public async Task delete_nonexistent_secret_returns_not_found()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resourceId = await CreateResourceAsync(admin);

        var result = await admin.DeleteSecretAsync(resourceId, ApiResourceSecretId.New(), _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors!.ShouldContain(e => e.Code == "not_found");
    }

    // UpdateApiResource no longer carries ApiSecrets; secrets are managed exclusively via
    // CreateSecretAsync/DeleteSecretAsync, and UpdateAsync is expected to preserve them.
    // The preservation is implemented server-side by re-attaching existingDso.ApiSecrets in
    // ApiResourceAdmin.MapToDso. These two tests pin that behavior so a future refactor of
    // MapToDso cannot silently wipe every secret on an unrelated resource edit.

    [Fact]
    public async Task update_preserves_existing_secrets()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resourceId = await CreateResourceAsync(admin);

        var secretResult = await admin.CreateSecretAsync(
            resourceId, "preserved-secret", SecretHashAlgorithm.Sha256, null, null, null, _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");
        var secretId = secretResult.Id!;

        var getResult = await admin.GetAsync(resourceId, _ct);
        getResult.Found.ShouldBeTrue();
        var update = getResult.Item!.ToUpdate();
        update.DisplayName = "Renamed display";

        var updateResult = await admin.UpdateAsync(resourceId, update, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var reloaded = await admin.GetAsync(resourceId, _ct);
        reloaded.Found.ShouldBeTrue();
        reloaded.Item!.DisplayName.ShouldBe("Renamed display");
        reloaded.Item!.ApiSecrets.ShouldNotBeNull();
        reloaded.Item!.ApiSecrets.ShouldContain(s => s.Id! == secretId,
            "UpdateAsync must preserve secrets managed via CreateSecretAsync/DeleteSecretAsync.");
    }

    [Fact]
    public async Task update_preserves_existing_secrets_across_rename()
    {
        // A rename takes the UpdateWithScopeChangesAsync branch in ApiResourceAdmin.UpdateAsync,
        // which is a different repository call than the non-rename path. Cover it explicitly.
        var admin = _fixture.ApiResourceAdmin;
        var originalName = $"api_{Guid.NewGuid():N}";
        var resourceId = await CreateResourceAsync(admin, originalName);

        var secretResult = await admin.CreateSecretAsync(
            resourceId, "preserved-across-rename", SecretHashAlgorithm.Sha256, null, null, null, _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");
        var secretId = secretResult.Id!;

        var getResult = await admin.GetAsync(resourceId, _ct);
        getResult.Found.ShouldBeTrue();
        var update = getResult.Item!.ToUpdate();
        update.Name = $"renamed_{Guid.NewGuid():N}";

        var updateResult = await admin.UpdateAsync(resourceId, update, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var reloaded = await admin.GetAsync(resourceId, _ct);
        reloaded.Found.ShouldBeTrue();
        reloaded.Item!.Name.ShouldBe(update.Name);
        reloaded.Item!.ApiSecrets.ShouldNotBeNull();
        reloaded.Item!.ApiSecrets.ShouldContain(s => s.Id! == secretId,
            "UpdateAsync must preserve secrets even when the resource is renamed.");
    }

    [Fact]
    public async Task update_replaces_api_resource_scopes()
    {
        var scopeNameA = await CreateScopeAsync("scope_a");
        var scopeNameB = await CreateScopeAsync("scope_b");
        var scopeNameC = await CreateScopeAsync("scope_c");
        var admin = _fixture.ApiResourceAdmin;
        var createResult = await admin.CreateAsync(
            new CreateApiResource { Name = $"api_{Guid.NewGuid():N}", Scopes = [scopeNameA, scopeNameB] },
            _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        var toUpdate = getResult.Item!.ToUpdate();
        toUpdate.Scopes = [scopeNameB, scopeNameC];
        var updateResult = await admin.UpdateAsync(createResult.Id!, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var afterUpdate = await admin.GetAsync(createResult.Id!, _ct);
        afterUpdate.Item!.Scopes.ShouldBe([scopeNameB, scopeNameC]);
    }

    [Fact]
    public async Task deleting_api_resource_lets_its_scope_be_deleted()
    {
        var scopeAdmin = _fixture.ApiScopeAdmin;
        var scopeName = $"scope_{Guid.NewGuid():N}";
        var createScopeResult = await scopeAdmin.CreateAsync(new CreateApiScope { Name = scopeName }, _ct);
        createScopeResult.IsSuccess.ShouldBeTrue();
        var admin = _fixture.ApiResourceAdmin;
        var createResult = await admin.CreateAsync(
            new CreateApiResource { Name = $"api_{Guid.NewGuid():N}", Scopes = [scopeName] },
            _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var deleteResult = await admin.DeleteAsync(createResult.Id!, _ct);
        deleteResult.IsSuccess.ShouldBeTrue($"Delete failed: {deleteResult}");

        var deleteScopeResult = await scopeAdmin.DeleteAsync(createScopeResult.Id!, _ct);
        deleteScopeResult.IsSuccess.ShouldBeTrue($"Delete scope failed: {deleteScopeResult}");
    }

    [Fact]
    public async Task create_with_nonexistent_scope_returns_error()
    {
        var admin = _fixture.ApiResourceAdmin;
        var nonExistentScopeName = $"scope_does_not_exist_{Guid.NewGuid():N}";

        var result = await admin.CreateAsync(
            new CreateApiResource { Name = $"api_{Guid.NewGuid():N}", Scopes = [nonExistentScopeName] },
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors!.ShouldContain(e => e.Code == "invalid_value"
            && e.PropertyNames != null && e.PropertyNames.Contains("Scopes")
            && e.Message.Contains(nonExistentScopeName));
    }

    [Fact]
    public async Task update_with_nonexistent_scope_returns_error()
    {
        var admin = _fixture.ApiResourceAdmin;
        var resourceId = await CreateResourceAsync(admin);
        var nonExistentScopeName = $"scope_does_not_exist_{Guid.NewGuid():N}";

        var getResult = await admin.GetAsync(resourceId, _ct);
        var toUpdate = getResult.Item!.ToUpdate();
        toUpdate.Scopes = [nonExistentScopeName];
        var result = await admin.UpdateAsync(resourceId, toUpdate, getResult.Version!, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors!.ShouldContain(e => e.Code == "invalid_value"
            && e.PropertyNames != null && e.PropertyNames.Contains("Scopes")
            && e.Message.Contains(nonExistentScopeName));
    }

    [Fact]
    public async Task renaming_api_resource_keeps_its_scopes()
    {
        var scopeName = await CreateScopeAsync("scope");
        var admin = _fixture.ApiResourceAdmin;
        var createResult = await admin.CreateAsync(
            new CreateApiResource { Name = $"api_{Guid.NewGuid():N}", Scopes = [scopeName] },
            _ct);
        createResult.IsSuccess.ShouldBeTrue();
        var resourceId = createResult.Id!;

        var getResult = await admin.GetAsync(resourceId, _ct);
        var toUpdate = getResult.Item!.ToUpdate();
        var newName = $"api_renamed_{Guid.NewGuid():N}";
        toUpdate.Name = newName;
        var updateResult = await admin.UpdateAsync(resourceId, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Rename failed: {updateResult}");

        var afterRename = await admin.GetByNameAsync(newName, _ct);
        afterRename.Item!.Scopes.ShouldBe([scopeName]);
    }

    private async Task<string> CreateScopeAsync(string prefix)
    {
        var name = $"{prefix}_{Guid.NewGuid():N}";
        var result = await _fixture.ApiScopeAdmin.CreateAsync(new CreateApiScope { Name = name }, _ct);
        result.IsSuccess.ShouldBeTrue($"Create scope failed: {result}");
        return name;
    }

    private static string CreateBase64Certificate()
    {
        using var cert = TestCert.Load();
        return Convert.ToBase64String(cert.Export(X509ContentType.Cert));
    }

    private static string CreatePublicJwkJson()
    {
        using var rsa = RSA.Create(2048);
        using var publicRsa = RSA.Create();
        publicRsa.ImportParameters(rsa.ExportParameters(false));
        var securityKey = new RsaSecurityKey(publicRsa);
        var publicJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(securityKey);
        publicJwk.Alg = SecurityAlgorithms.RsaSha256;
        publicJwk.D.ShouldBeNullOrEmpty();
        publicJwk.P.ShouldBeNullOrEmpty();
        publicJwk.Q.ShouldBeNullOrEmpty();
        publicJwk.DP.ShouldBeNullOrEmpty();
        publicJwk.DQ.ShouldBeNullOrEmpty();
        publicJwk.QI.ShouldBeNullOrEmpty();
        return JsonSerializer.Serialize(publicJwk);
    }

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();
}
