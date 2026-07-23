// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.Storage;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin;

public sealed class ApiScopeAdminTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly List<IServiceScope> _scopes = [];

    private IApiScopeAdmin NewAdmin()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IApiScopeAdmin>();
    }

    [Fact]
    public async Task create_and_get_by_id_round_trips_all_fields()
    {
        var admin = NewAdmin();
        var apiScope = new ApiScopeConfiguration
        {
            Name = $"scope_{Guid.NewGuid():N}",
            DisplayName = "Test Scope",
            Description = "A test scope",
            Enabled = true,
            ShowInDiscoveryDocument = false,
            Required = true,
            Emphasize = true,
            UserClaims = ["sub", "email"]
        };

        var createResult = await admin.CreateAsync(apiScope, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var loaded = getResult.Item;
        loaded.Name.ShouldBe(apiScope.Name);
        loaded.DisplayName.ShouldBe(apiScope.DisplayName);
        loaded.Description.ShouldBe(apiScope.Description);
        loaded.Enabled.ShouldBe(apiScope.Enabled);
        loaded.ShowInDiscoveryDocument.ShouldBe(apiScope.ShowInDiscoveryDocument);
        loaded.Required.ShouldBe(apiScope.Required);
        loaded.Emphasize.ShouldBe(apiScope.Emphasize);
        loaded.UserClaims.ShouldNotBeNull();
        loaded.UserClaims.ShouldBe(apiScope.UserClaims);
    }

    [Fact]
    public async Task create_and_get_by_name_round_trips()
    {
        var admin = NewAdmin();
        var name = $"scope_{Guid.NewGuid():N}";
        var apiScope = new ApiScopeConfiguration
        {
            Name = name,
            DisplayName = "ByName Test"
        };

        var createResult = await admin.CreateAsync(apiScope, _ct);
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
        var apiScope = new ApiScopeConfiguration { Name = $"scope_{Guid.NewGuid():N}" };

        var result = await admin.CreateAsync(apiScope, _ct);

        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");
        result.Id.ShouldNotBe(Guid.Empty);
        result.Version.ShouldNotBeNull();
        result.Version.Value.ShouldBe(1);
    }

    [Fact]
    public async Task create_duplicate_name_returns_already_exists()
    {
        var admin = NewAdmin();
        var name = $"scope_{Guid.NewGuid():N}";

        var first = await admin.CreateAsync(new ApiScopeConfiguration { Name = name }, _ct);
        first.IsSuccess.ShouldBeTrue();

        var second = await admin.CreateAsync(new ApiScopeConfiguration { Name = name }, _ct);
        second.IsSuccess.ShouldBeFalse();
        second.Errors.ShouldNotBeNull();
        second.Errors.ShouldContain(e => e.Code == "already_exists");
    }

    [Fact]
    public async Task update_changes_applied_on_read()
    {
        var admin = NewAdmin();
        var apiScope = new ApiScopeConfiguration
        {
            Name = $"scope_{Guid.NewGuid():N}",
            DisplayName = "Original",
            Description = "Original description"
        };

        var createResult = await admin.CreateAsync(apiScope, _ct);
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
        var apiScope = new ApiScopeConfiguration { Name = $"scope_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(apiScope, _ct);
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
        var apiScope = new ApiScopeConfiguration { Name = $"scope_{Guid.NewGuid():N}" };

        var result = await admin.UpdateAsync(nonExistentId, apiScope, (DataVersion)1, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "not_found");
    }

    [Fact]
    public async Task update_rename_to_existing_name_returns_already_exists()
    {
        var admin = NewAdmin();
        var nameA = $"scope_{Guid.NewGuid():N}";
        var nameB = $"scope_{Guid.NewGuid():N}";

        (await admin.CreateAsync(new ApiScopeConfiguration { Name = nameA }, _ct)).IsSuccess.ShouldBeTrue();
        var createB = await admin.CreateAsync(new ApiScopeConfiguration { Name = nameB }, _ct);
        createB.IsSuccess.ShouldBeTrue();

        var getB = await admin.GetAsync(createB.Id, _ct);
        getB.Found.ShouldBeTrue();
        var configB = getB.Item;
        configB.Name = nameA; // rename B to A's name

        var updateResult = await admin.UpdateAsync(createB.Id, configB, getB.Version!, _ct);
        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors.ShouldContain(e => e.Code == "already_exists");
    }

    [Fact]
    public async Task delete_then_get_returns_not_found()
    {
        var admin = NewAdmin();
        var apiScope = new ApiScopeConfiguration { Name = $"scope_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(apiScope, _ct);
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
        var apiScope = new ApiScopeConfiguration { Name = "" };

        var result = await admin.CreateAsync(apiScope, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "required" && e.PropertyNames != null && e.PropertyNames.Contains("Name"));
    }

    [Fact]
    public async Task query_by_name_filter_returns_matching()
    {
        var uniquePart = $"q_{Guid.NewGuid():N}";
        var admin = NewAdmin();

        await admin.CreateAsync(new ApiScopeConfiguration { Name = uniquePart + "_match1" }, _ct);
        await admin.CreateAsync(new ApiScopeConfiguration { Name = uniquePart + "_match2" }, _ct);
        await admin.CreateAsync(new ApiScopeConfiguration { Name = $"other_{Guid.NewGuid():N}" }, _ct);

        var result = await admin.QueryAsync(
            QueryRequest.Create<ApiScopeFilter, ApiScopeSortField>(
                new ApiScopeFilter { Name = uniquePart }),
            _ct);

        result.Items.ShouldAllBe(s => s.Name.Contains(uniquePart));
        result.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task query_by_enabled_filter_returns_matching()
    {
        var admin = NewAdmin();
        var enabledName = $"q_enabled_{Guid.NewGuid():N}";
        var disabledName = $"q_disabled_{Guid.NewGuid():N}";

        await admin.CreateAsync(new ApiScopeConfiguration { Name = enabledName, Enabled = true }, _ct);
        await admin.CreateAsync(new ApiScopeConfiguration { Name = disabledName, Enabled = false }, _ct);

        var enabledResult = await admin.QueryAsync(
            QueryRequest.Create<ApiScopeFilter, ApiScopeSortField>(
                new ApiScopeFilter { Name = enabledName, Enabled = true }),
            _ct);

        enabledResult.Items.ShouldContain(s => s.Name == enabledName);
        enabledResult.Items.ShouldNotContain(s => s.Name == disabledName);
    }

    [Fact]
    public async Task query_with_pagination_returns_correct_page()
    {
        var prefix = $"q_page_{Guid.NewGuid():N}_";
        var admin = NewAdmin();

        for (var i = 0; i < 5; i++)
        {
            await admin.CreateAsync(new ApiScopeConfiguration { Name = prefix + i }, _ct);
        }

        var page1 = await admin.QueryAsync(
            QueryRequest.Create<ApiScopeFilter, ApiScopeSortField>(
                new ApiScopeFilter { Name = prefix },
                (DataRange)DataRange.FromPage(1, (DataRangeSize)2)),
            _ct);

        page1.Items.Count.ShouldBe(2);

        var page2 = await admin.QueryAsync(
            QueryRequest.Create<ApiScopeFilter, ApiScopeSortField>(
                new ApiScopeFilter { Name = prefix },
                (DataRange)DataRange.FromPage(2, (DataRangeSize)2)),
            _ct);

        page2.Items.Count.ShouldBe(2);

        var page1Ids = page1.Items.Select(s => s.Id).ToHashSet();
        var page2Ids = page2.Items.Select(s => s.Id).ToHashSet();
        page1Ids.Intersect(page2Ids).ShouldBeEmpty();
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
