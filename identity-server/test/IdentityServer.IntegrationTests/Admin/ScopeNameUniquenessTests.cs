// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.ApiScopes;
using Duende.IdentityServer.Admin.IdentityResources;

namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// API scope and identity resource names must be unique across both kinds: a clash would otherwise make
/// <c>IResourceStoreExtensions.ValidateNameUniqueness</c> throw on every token request for that name.
/// </summary>
public sealed class ScopeNameUniquenessTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task creating_api_scope_with_identity_resource_name_is_refused()
    {
        var name = $"name_{Guid.NewGuid():N}";
        (await _fixture.IdentityResourceAdmin.CreateAsync(new CreateIdentityResource { Name = name }, _ct))
            .IsSuccess.ShouldBeTrue();

        var result = await _fixture.ApiScopeAdmin.CreateAsync(new CreateApiScope { Name = name }, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Code == "already_exists");
    }

    [Fact]
    public async Task creating_identity_resource_with_api_scope_name_is_refused()
    {
        var name = $"name_{Guid.NewGuid():N}";
        (await _fixture.ApiScopeAdmin.CreateAsync(new CreateApiScope { Name = name }, _ct))
            .IsSuccess.ShouldBeTrue();

        var result = await _fixture.IdentityResourceAdmin.CreateAsync(new CreateIdentityResource { Name = name }, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Code == "already_exists");
    }

    [Fact]
    public async Task renaming_api_scope_into_identity_resource_name_is_refused()
    {
        var takenName = $"name_{Guid.NewGuid():N}";
        (await _fixture.IdentityResourceAdmin.CreateAsync(new CreateIdentityResource { Name = takenName }, _ct))
            .IsSuccess.ShouldBeTrue();

        var create = await _fixture.ApiScopeAdmin.CreateAsync(new CreateApiScope { Name = $"scope_{Guid.NewGuid():N}" }, _ct);
        create.IsSuccess.ShouldBeTrue();
        var got = await _fixture.ApiScopeAdmin.GetAsync(create.Id!, _ct);
        var toUpdate = got.Item!.ToUpdate();
        toUpdate.Name = takenName;

        var result = await _fixture.ApiScopeAdmin.UpdateAsync(create.Id!, toUpdate, got.Version!, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Code == "already_exists");
    }

    [Fact]
    public async Task renaming_identity_resource_into_api_scope_name_is_refused()
    {
        var takenName = $"name_{Guid.NewGuid():N}";
        (await _fixture.ApiScopeAdmin.CreateAsync(new CreateApiScope { Name = takenName }, _ct))
            .IsSuccess.ShouldBeTrue();

        var create = await _fixture.IdentityResourceAdmin.CreateAsync(new CreateIdentityResource { Name = $"identity_{Guid.NewGuid():N}" }, _ct);
        create.IsSuccess.ShouldBeTrue();
        var got = await _fixture.IdentityResourceAdmin.GetAsync(create.Id!, _ct);
        var toUpdate = got.Item!.ToUpdate();
        toUpdate.Name = takenName;

        var result = await _fixture.IdentityResourceAdmin.UpdateAsync(create.Id!, toUpdate, got.Version!, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Code == "already_exists");
    }

    [Fact]
    public async Task renaming_away_frees_the_old_name_for_the_other_kind()
    {
        var oldName = $"name_{Guid.NewGuid():N}";
        var newName = $"name_{Guid.NewGuid():N}";

        var create = await _fixture.ApiScopeAdmin.CreateAsync(new CreateApiScope { Name = oldName }, _ct);
        create.IsSuccess.ShouldBeTrue();
        var got = await _fixture.ApiScopeAdmin.GetAsync(create.Id!, _ct);
        var toUpdate = got.Item!.ToUpdate();
        toUpdate.Name = newName;
        (await _fixture.ApiScopeAdmin.UpdateAsync(create.Id!, toUpdate, got.Version!, _ct)).IsSuccess.ShouldBeTrue();

        // The old name is now free: an identity resource can use it.
        var result = await _fixture.IdentityResourceAdmin.CreateAsync(new CreateIdentityResource { Name = oldName }, _ct);
        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");
    }

    [Fact]
    public async Task deleting_frees_the_name_for_the_other_kind()
    {
        var name = $"name_{Guid.NewGuid():N}";

        var create = await _fixture.ApiScopeAdmin.CreateAsync(new CreateApiScope { Name = name }, _ct);
        create.IsSuccess.ShouldBeTrue();
        (await _fixture.ApiScopeAdmin.DeleteAsync(create.Id!, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _fixture.IdentityResourceAdmin.CreateAsync(new CreateIdentityResource { Name = name }, _ct);
        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");
    }

    [Fact]
    public async Task two_simultaneous_creates_of_the_same_name_exactly_one_succeeds()
    {
        var name = $"name_{Guid.NewGuid():N}";

        var apiScopeTask = _fixture.ApiScopeAdmin.CreateAsync(new CreateApiScope { Name = name }, _ct);
        var identityResourceTask = _fixture.IdentityResourceAdmin.CreateAsync(new CreateIdentityResource { Name = name }, _ct);

        await Task.WhenAll(apiScopeTask, identityResourceTask);

        var apiScopeResult = await apiScopeTask;
        var identityResourceResult = await identityResourceTask;

        var successCount = (apiScopeResult.IsSuccess ? 1 : 0) + (identityResourceResult.IsSuccess ? 1 : 0);
        successCount.ShouldBe(1, $"ApiScope: {apiScopeResult}, IdentityResource: {identityResourceResult}");

        if (!apiScopeResult.IsSuccess)
        {
            apiScopeResult.Errors!.ShouldContain(e => e.Code == "already_exists");
        }

        if (!identityResourceResult.IsSuccess)
        {
            identityResourceResult.Errors!.ShouldContain(e => e.Code == "already_exists");
        }
    }

    [Fact]
    public async Task legacy_unguarded_row_still_blocks_the_other_kind_from_its_name()
    {
        // Simulates a row saved before the ScopeNameDso guard was introduced: it has no guard of its
        // own. The guard alone would not catch a clash here, so ApiScopeAdmin/IdentityResourceAdmin
        // also read the other kind's name directly before creating (and before renaming into it).
        var name = $"name_{Guid.NewGuid():N}";
        await _fixture.CreateLegacyUnguardedApiScopeAsync(name);

        var result = await _fixture.IdentityResourceAdmin.CreateAsync(new CreateIdentityResource { Name = name }, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Code == "already_exists");
    }

    [Fact]
    public async Task renaming_a_legacy_unguarded_row_creates_its_guard()
    {
        // The rename deletes any guard the entity's own ID holds (a no-op here, since there is none)
        // and then creates a guard for the new name. After the rename, the other kind must be refused
        // the new name even though the row never had a guard before.
        var oldName = $"name_{Guid.NewGuid():N}";
        var newName = $"name_{Guid.NewGuid():N}";
        var id = await _fixture.CreateLegacyUnguardedApiScopeAsync(oldName);

        var got = await _fixture.ApiScopeAdmin.GetAsync(id, _ct);
        got.Found.ShouldBeTrue();
        var toUpdate = got.Item!.ToUpdate();
        toUpdate.Name = newName;

        var updateResult = await _fixture.ApiScopeAdmin.UpdateAsync(id, toUpdate, got.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Rename failed: {updateResult}");

        var result = await _fixture.IdentityResourceAdmin.CreateAsync(new CreateIdentityResource { Name = newName }, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Code == "already_exists");
    }

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();
}
