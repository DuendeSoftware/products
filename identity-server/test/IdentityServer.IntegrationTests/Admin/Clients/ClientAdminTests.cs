// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.IntegrationTests.Admin.Clients;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using DataVersion = Duende.IdentityServer.Models.DataVersion;
using Duende.IdentityServer.Validation;
using Duende.Storage;
using Duende.IdentityServer.DataExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.IntegrationTests.Admin;

public sealed class ClientAdminTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private async Task<ClientId> CreateClientAsync(IClientAdmin admin, Ct ct)
    {
        var result = await admin.CreateAsync(
            new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" },
            ct);
        result.IsSuccess.ShouldBeTrue($"CreateClient failed: {result}");
        return result.Id!;
    }

    [Fact]
    public async Task create_with_unknown_scope_name_returns_validation_error()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient
        {
            ClientId = $"client_{Guid.NewGuid():N}",
            AllowedScopes = ["does_not_exist"]
        };

        var result = await admin.CreateAsync(client, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "invalid_value" && e.PropertyNames != null && e.PropertyNames.Contains("AllowedScopes"));
    }

    [Fact]
    public async Task create_with_duplicate_allowed_scope_name_links_once()
    {
        var admin = _fixture.ClientAdmin;
        await _fixture.EnsureApiScopesAsync("a");

        var clientId = $"client_{Guid.NewGuid():N}";
        var result = await admin.CreateAsync(
            new CreateClient { ClientId = clientId, AllowedScopes = ["a", "a"] }, _ct);
        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");

        var getResult = await admin.GetAsync(result.Id!, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.AllowedScopes.ShouldBe(["a"]);
    }

    [Fact]
    public async Task create_with_offline_access_in_allowed_scopes_returns_validation_error_pointing_to_allow_offline_access()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient
        {
            ClientId = $"client_{Guid.NewGuid():N}",
            AllowedScopes = [IdentityServerConstants.StandardScopes.OfflineAccess]
        };

        var result = await admin.CreateAsync(client, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "invalid_value"
            && e.PropertyNames != null && e.PropertyNames.Contains("AllowedScopes")
            && e.Message.Contains("AllowOfflineAccess", StringComparison.Ordinal));
    }

    [Fact]
    public async Task create_with_allowed_scope_naming_an_identity_resource_succeeds()
    {
        var admin = _fixture.ClientAdmin;
        var identityResourceName = $"idres_{Guid.NewGuid():N}";
        var createdResource = await _fixture.IdentityResourceAdmin.CreateAsync(
            new Duende.IdentityServer.Admin.IdentityResources.CreateIdentityResource { Name = identityResourceName }, _ct);
        createdResource.IsSuccess.ShouldBeTrue();

        var client = new CreateClient
        {
            ClientId = $"client_{Guid.NewGuid():N}",
            AllowedScopes = [identityResourceName]
        };

        var result = await admin.CreateAsync(client, _ct);

        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");
    }

    [Fact]
    public async Task renaming_an_api_scope_is_reflected_in_client_get_and_client_store()
    {
        var admin = _fixture.ClientAdmin;
        var oldName = $"scope_{Guid.NewGuid():N}";
        var newName = $"scope_renamed_{Guid.NewGuid():N}";

        var createdScope = await _fixture.ApiScopeAdmin.CreateAsync(new CreateApiScope { Name = oldName }, _ct);
        createdScope.IsSuccess.ShouldBeTrue();

        var clientId = $"client_{Guid.NewGuid():N}";
        var createResult = await admin.CreateAsync(new CreateClient { ClientId = clientId, AllowedScopes = [oldName] }, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var scopeGet = await _fixture.ApiScopeAdmin.GetAsync(createdScope.Id!, _ct);
        scopeGet.Found.ShouldBeTrue();
        var scopeUpdate = scopeGet.Item!.ToUpdate();
        scopeUpdate.Name = newName;
        var renameResult = await _fixture.ApiScopeAdmin.UpdateAsync(createdScope.Id!, scopeUpdate, scopeGet.Version!, _ct);
        renameResult.IsSuccess.ShouldBeTrue($"Rename failed: {renameResult}");

        var clientGet = await admin.GetAsync(createResult.Id!, _ct);
        clientGet.Found.ShouldBeTrue();
        clientGet.Item!.AllowedScopes.ShouldBe([newName]);

        var fromStore = await _fixture.ClientStore.FindClientByIdAsync(clientId, _ct);
        fromStore.ShouldNotBeNull();
        fromStore!.AllowedScopes.ShouldBe([newName]);
    }

    [Fact]
    public async Task renaming_an_identity_resource_is_reflected_in_client_get_and_client_store()
    {
        var admin = _fixture.ClientAdmin;
        var oldName = $"idres_{Guid.NewGuid():N}";
        var newName = $"idres_renamed_{Guid.NewGuid():N}";

        var createdResource = await _fixture.IdentityResourceAdmin.CreateAsync(
            new Duende.IdentityServer.Admin.IdentityResources.CreateIdentityResource { Name = oldName }, _ct);
        createdResource.IsSuccess.ShouldBeTrue();

        var clientId = $"client_{Guid.NewGuid():N}";
        var createResult = await admin.CreateAsync(new CreateClient { ClientId = clientId, AllowedScopes = [oldName] }, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var resourceGet = await _fixture.IdentityResourceAdmin.GetAsync(createdResource.Id!, _ct);
        resourceGet.Found.ShouldBeTrue();
        var resourceUpdate = resourceGet.Item!.ToUpdate();
        resourceUpdate.Name = newName;
        var renameResult = await _fixture.IdentityResourceAdmin.UpdateAsync(createdResource.Id!, resourceUpdate, resourceGet.Version!, _ct);
        renameResult.IsSuccess.ShouldBeTrue($"Rename failed: {renameResult}");

        var clientGet = await admin.GetAsync(createResult.Id!, _ct);
        clientGet.Found.ShouldBeTrue();
        clientGet.Item!.AllowedScopes.ShouldBe([newName]);

        var fromStore = await _fixture.ClientStore.FindClientByIdAsync(clientId, _ct);
        fromStore.ShouldNotBeNull();
        fromStore!.AllowedScopes.ShouldBe([newName]);
    }

    [Fact]
    public async Task deleting_an_api_scope_allowed_by_a_client_is_refused_naming_the_client()
    {
        var scopeName = $"scope_{Guid.NewGuid():N}";
        var createdScope = await _fixture.ApiScopeAdmin.CreateAsync(new CreateApiScope { Name = scopeName }, _ct);
        createdScope.IsSuccess.ShouldBeTrue();

        var clientId = $"client_{Guid.NewGuid():N}";
        var createResult = await _fixture.ClientAdmin.CreateAsync(
            new CreateClient { ClientId = clientId, AllowedScopes = [scopeName] }, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var deleteResult = await _fixture.ApiScopeAdmin.DeleteAsync(createdScope.Id!, _ct);

        deleteResult.IsSuccess.ShouldBeFalse();
        deleteResult.Errors.ShouldNotBeNull();
        deleteResult.Errors.ShouldContain(e => e.Code == "validation_failed" && e.Message.Contains(clientId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task deleting_an_identity_resource_allowed_by_a_client_is_refused_naming_the_client()
    {
        var resourceName = $"idres_{Guid.NewGuid():N}";
        var createdResource = await _fixture.IdentityResourceAdmin.CreateAsync(
            new Duende.IdentityServer.Admin.IdentityResources.CreateIdentityResource { Name = resourceName }, _ct);
        createdResource.IsSuccess.ShouldBeTrue();

        var clientId = $"client_{Guid.NewGuid():N}";
        var createResult = await _fixture.ClientAdmin.CreateAsync(
            new CreateClient { ClientId = clientId, AllowedScopes = [resourceName] }, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var deleteResult = await _fixture.IdentityResourceAdmin.DeleteAsync(createdResource.Id!, _ct);

        deleteResult.IsSuccess.ShouldBeFalse();
        deleteResult.Errors.ShouldNotBeNull();
        deleteResult.Errors.ShouldContain(e => e.Code == "validation_failed" && e.Message.Contains(clientId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task filter_by_allowed_scope_matches_exact_name_across_both_kinds_and_follows_rename()
    {
        var admin = _fixture.ClientAdmin;
        var apiScopeName = $"scope_{Guid.NewGuid():N}";
        var identityResourceName = $"idres_{Guid.NewGuid():N}";

        var createdScope = await _fixture.ApiScopeAdmin.CreateAsync(new CreateApiScope { Name = apiScopeName }, _ct);
        createdScope.IsSuccess.ShouldBeTrue();
        var createdResource = await _fixture.IdentityResourceAdmin.CreateAsync(
            new Duende.IdentityServer.Admin.IdentityResources.CreateIdentityResource { Name = identityResourceName }, _ct);
        createdResource.IsSuccess.ShouldBeTrue();

        var apiScopeClientId = $"client_api_{Guid.NewGuid():N}";
        var identityResourceClientId = $"client_idres_{Guid.NewGuid():N}";
        var unrelatedClientId = $"client_other_{Guid.NewGuid():N}";

        (await admin.CreateAsync(new CreateClient { ClientId = apiScopeClientId, AllowedScopes = [apiScopeName] }, _ct))
            .IsSuccess.ShouldBeTrue();
        (await admin.CreateAsync(new CreateClient { ClientId = identityResourceClientId, AllowedScopes = [identityResourceName] }, _ct))
            .IsSuccess.ShouldBeTrue();
        (await admin.CreateAsync(new CreateClient { ClientId = unrelatedClientId }, _ct))
            .IsSuccess.ShouldBeTrue();

        var byApiScope = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(new ClientFilter { AllowedScope = apiScopeName }), _ct);
        byApiScope.Results.ShouldContain(c => c.ClientId == apiScopeClientId);
        byApiScope.Results.ShouldNotContain(c => c.ClientId == identityResourceClientId);
        byApiScope.Results.ShouldNotContain(c => c.ClientId == unrelatedClientId);

        var byIdentityResource = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(new ClientFilter { AllowedScope = identityResourceName }), _ct);
        byIdentityResource.Results.ShouldContain(c => c.ClientId == identityResourceClientId);
        byIdentityResource.Results.ShouldNotContain(c => c.ClientId == apiScopeClientId);

        // Renaming the API scope: filtering by the old name no longer matches, the new name does.
        var scopeGet = await _fixture.ApiScopeAdmin.GetAsync(createdScope.Id!, _ct);
        var newApiScopeName = $"scope_renamed_{Guid.NewGuid():N}";
        var scopeUpdate = scopeGet.Item!.ToUpdate();
        scopeUpdate.Name = newApiScopeName;
        var renameResult = await _fixture.ApiScopeAdmin.UpdateAsync(createdScope.Id!, scopeUpdate, scopeGet.Version!, _ct);
        renameResult.IsSuccess.ShouldBeTrue($"Rename failed: {renameResult}");

        var byOldName = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(new ClientFilter { AllowedScope = apiScopeName }), _ct);
        byOldName.Results.ShouldNotContain(c => c.ClientId == apiScopeClientId);

        var byNewName = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(new ClientFilter { AllowedScope = newApiScopeName }), _ct);
        byNewName.Results.ShouldContain(c => c.ClientId == apiScopeClientId);
    }

    [Fact]
    public async Task list_reports_allowed_scope_count_across_both_kinds()
    {
        var admin = _fixture.ClientAdmin;
        var apiScopeName = $"scope_{Guid.NewGuid():N}";
        var identityResourceName = $"idres_{Guid.NewGuid():N}";

        (await _fixture.ApiScopeAdmin.CreateAsync(new CreateApiScope { Name = apiScopeName }, _ct)).IsSuccess.ShouldBeTrue();
        (await _fixture.IdentityResourceAdmin.CreateAsync(
            new Duende.IdentityServer.Admin.IdentityResources.CreateIdentityResource { Name = identityResourceName }, _ct))
            .IsSuccess.ShouldBeTrue();

        var clientId = $"client_{Guid.NewGuid():N}";
        var createResult = await admin.CreateAsync(
            new CreateClient { ClientId = clientId, AllowedScopes = [apiScopeName, identityResourceName] }, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var result = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(new ClientFilter { ClientId = clientId }), _ct);

        var listItem = result.Results.ShouldHaveSingleItem();
        listItem.AllowedScopeCount.ShouldBe(2);
    }

    [Fact]
    public async Task create_client_and_get_by_id_round_trips_all_fields()
    {
        var admin = _fixture.ClientAdmin;
        await _fixture.EnsureApiScopesAsync("api1", "api2");
        var client = new CreateClient
        {
            ClientId = $"client_{Guid.NewGuid():N}",
            ClientName = "Test Client",
            Description = "A test client",
            ClientUri = "https://example.com",
            LogoUri = "https://example.com/logo.png",
            Enabled = true,
            RequireClientSecret = true,
            RequirePkce = false,
            AllowPlainTextPkce = true,
            RequireConsent = true,
            AllowRememberConsent = false,
            AllowOfflineAccess = true,
            AccessTokenType = AccessTokenType.Reference,
            IncludeJwtId = false,
            IdentityTokenLifetime = 600,
            AccessTokenLifetime = 7200,
            AuthorizationCodeLifetime = 600,
            AbsoluteRefreshTokenLifetime = 1000000,
            SlidingRefreshTokenLifetime = 500000,
            RefreshTokenUsage = TokenUsage.OneTimeOnly,
            RefreshTokenExpiration = TokenExpiration.Sliding,
            AlwaysIncludeUserClaimsInIdToken = true,
            AlwaysSendClientClaims = true,
            ClientClaimsPrefix = "custom_",
            EnableLocalLogin = false,
            AllowedGrantTypes = [GrantType.ClientCredentials],
            AllowedScopes = ["api1", "api2"],
            RedirectUris = ["https://app.example.com/callback"],
            PostLogoutRedirectUris = ["https://app.example.com/signout"],
            AllowedCorsOrigins = ["https://app.example.com"],
            ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
        };
        client.ExtendedProperties.Set(TestClientAttributes.Department, "Engineering");

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var loaded = getResult.Item!;
        loaded.ClientId.ShouldBe(client.ClientId);
        loaded.ClientName.ShouldBe(client.ClientName);
        loaded.Description.ShouldBe(client.Description);
        loaded.ClientUri.ShouldBe(client.ClientUri);
        loaded.LogoUri.ShouldBe(client.LogoUri);
        loaded.Enabled.ShouldBe(client.Enabled);
        loaded.RequireClientSecret.ShouldBe(client.RequireClientSecret);
        loaded.RequirePkce.ShouldBe(client.RequirePkce);
        loaded.AllowPlainTextPkce.ShouldBe(client.AllowPlainTextPkce);
        loaded.RequireConsent.ShouldBe(client.RequireConsent);
        loaded.AllowRememberConsent.ShouldBe(client.AllowRememberConsent);
        loaded.AllowOfflineAccess.ShouldBe(client.AllowOfflineAccess);
        loaded.AccessTokenType.ShouldBe(client.AccessTokenType);
        loaded.IncludeJwtId.ShouldBe(client.IncludeJwtId);
        loaded.IdentityTokenLifetime.ShouldBe(client.IdentityTokenLifetime);
        loaded.AccessTokenLifetime.ShouldBe(client.AccessTokenLifetime);
        loaded.AuthorizationCodeLifetime.ShouldBe(client.AuthorizationCodeLifetime);
        loaded.AbsoluteRefreshTokenLifetime.ShouldBe(client.AbsoluteRefreshTokenLifetime);
        loaded.SlidingRefreshTokenLifetime.ShouldBe(client.SlidingRefreshTokenLifetime);
        loaded.RefreshTokenUsage.ShouldBe(client.RefreshTokenUsage);
        loaded.RefreshTokenExpiration.ShouldBe(client.RefreshTokenExpiration);
        loaded.AlwaysIncludeUserClaimsInIdToken.ShouldBe(client.AlwaysIncludeUserClaimsInIdToken);
        loaded.AlwaysSendClientClaims.ShouldBe(client.AlwaysSendClientClaims);
        loaded.ClientClaimsPrefix.ShouldBe(client.ClientClaimsPrefix);
        loaded.EnableLocalLogin.ShouldBe(client.EnableLocalLogin);
        loaded.AllowedGrantTypes.ShouldNotBeNull();
        loaded.AllowedGrantTypes.ShouldBe(client.AllowedGrantTypes);
        loaded.AllowedScopes.ShouldNotBeNull();
        loaded.AllowedScopes.ShouldBe(client.AllowedScopes);
        loaded.RedirectUris.ShouldNotBeNull();
        loaded.RedirectUris.ShouldBe(client.RedirectUris);
        loaded.PostLogoutRedirectUris.ShouldNotBeNull();
        loaded.PostLogoutRedirectUris.ShouldBe(client.PostLogoutRedirectUris);
        loaded.AllowedCorsOrigins.ShouldNotBeNull();
        loaded.AllowedCorsOrigins.ShouldBe(client.AllowedCorsOrigins);
        loaded.ExtendedProperties.Count.ShouldBe(1);
        var deptAttr = loaded.ExtendedProperties.FirstOrDefault(x => x.Code == TestClientAttributes.Department.Code);
        deptAttr.ShouldNotBeNull();
        deptAttr.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("Engineering");
    }

    [Fact]
    public async Task create_client_and_get_by_client_id_round_trips()
    {
        var admin = _fixture.ClientAdmin;
        var clientId = $"client_{Guid.NewGuid():N}";
        var client = new CreateClient
        {
            ClientId = clientId,
            ClientName = "ByClientId Test"
        };

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetByClientIdAsync(clientId, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.ClientId.ShouldBe(clientId);
        getResult.Item!.ClientName.ShouldBe("ByClientId Test");
    }

    [Fact]
    public async Task create_client_returns_storage_id_and_version()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };

        var result = await admin.CreateAsync(client, _ct);

        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");
        result.Id!.Value.ShouldNotBe(Guid.Empty);
        result.Version!.ShouldNotBeNull();
        result.Version!.Value.ShouldBe(1);
    }

    [Fact]
    public async Task create_duplicate_client_id_returns_already_exists()
    {
        var admin = _fixture.ClientAdmin;
        var clientId = $"client_{Guid.NewGuid():N}";
        var client = new CreateClient { ClientId = clientId };

        var first = await admin.CreateAsync(client, _ct);
        first.IsSuccess.ShouldBeTrue();

        var second = await admin.CreateAsync(new CreateClient { ClientId = clientId }, _ct);
        second.IsSuccess.ShouldBeFalse();
        second.Errors.ShouldNotBeNull();
        second.Errors.ShouldContain(e => e.Code == "already_exists");
    }

    [Fact]
    public async Task update_client_changes_applied_on_read()
    {
        var admin = _fixture.ClientAdmin;
        var clientId = $"client_{Guid.NewGuid():N}";
        var client = new CreateClient
        {
            ClientId = clientId,
            ClientName = "Original Name",
            Description = "Original description"
        };

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var toUpdate = getResult.Item!.ToUpdate();
        toUpdate.ClientName = "Updated Name";
        toUpdate.Description = "Updated description";
        toUpdate.Enabled = false;

        var updateResult = await admin.UpdateAsync(createResult.Id!, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var afterUpdate = await admin.GetAsync(createResult.Id!, _ct);
        afterUpdate.Found.ShouldBeTrue();
        afterUpdate.Item!.ClientName.ShouldBe("Updated Name");
        afterUpdate.Item!.Description.ShouldBe("Updated description");
        afterUpdate.Item!.Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task update_client_increments_version()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue();
        createResult.Version!.Value.ShouldBe(1);

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var updateResult = await admin.UpdateAsync(createResult.Id!, getResult.Item!.ToUpdate(), getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");
        updateResult.Version!.Value.ShouldBe(2);
    }

    [Fact]
    public async Task update_with_wrong_version_returns_version_conflict()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var wrongVersion = (DataVersion)999;
        var updateResult = await admin.UpdateAsync(createResult.Id!, getResult.Item!.ToUpdate(), wrongVersion, _ct);

        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors.ShouldNotBeNull();
        updateResult.Errors.ShouldContain(e => e.Code == "version_conflict");
    }

    [Fact]
    public async Task update_nonexistent_client_returns_not_found()
    {
        var admin = _fixture.ClientAdmin;
        var nonExistentId = ClientId.New();
        var client = new UpdateClient { ClientId = $"client_{Guid.NewGuid():N}" };

        var result = await admin.UpdateAsync(nonExistentId, client, (DataVersion)1, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "not_found");
    }

    [Fact]
    public void json_deserialization_can_set_list_properties_to_null()
    {
        // Proves that System.Text.Json CAN set list properties to null even when
        // the class declares defaults like `List<string> Foo { get; set; } = []`.
        // This validates the need for null coalescing operators in the mapping code.
        var json = """
            {
                "ClientId": "test",
                "AllowedGrantTypes": null,
                "AllowedScopes": null,
                "RedirectUris": null,
                "PostLogoutRedirectUris": null,
                "AllowedIdentityTokenSigningAlgorithms": null,
                "IdentityProviderRestrictions": null,
                "AllowedCorsOrigins": null,
                "Claims": null
            }
            """;

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<UpdateClient>(json)!;

        deserialized.AllowedGrantTypes.ShouldBeNull();
        deserialized.AllowedScopes.ShouldBeNull();
        deserialized.RedirectUris.ShouldBeNull();
        deserialized.PostLogoutRedirectUris.ShouldBeNull();
        deserialized.AllowedIdentityTokenSigningAlgorithms.ShouldBeNull();
        deserialized.IdentityProviderRestrictions.ShouldBeNull();
        deserialized.AllowedCorsOrigins.ShouldBeNull();
        deserialized.Claims.ShouldBeNull();
    }

    [Fact]
    public async Task update_with_json_deserialized_null_list_properties_succeeds()
    {
        // Consumers building admin UIs will likely deserialize UpdateClient from HTTP
        // request bodies. If the JSON contains explicit nulls for list properties,
        // System.Text.Json overwrites the C# default initializers with null.
        // The mapping code must handle this gracefully via null coalescing.
        var admin = _fixture.ClientAdmin;
        var clientId = $"client_{Guid.NewGuid():N}";
        await _fixture.EnsureApiScopesAsync("api1");
        var createResult = await admin.CreateAsync(
            new CreateClient
            {
                ClientId = clientId,
                AllowedGrantTypes = [GrantType.ClientCredentials],
                AllowedScopes = ["api1"]
            }, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        // Deserialize an UpdateClient from JSON with explicit null list properties,
        // exactly as an admin API endpoint would receive from a client.
        var json = $$"""
            {
                "ClientId": "{{clientId}}",
                "AllowedGrantTypes": null,
                "AllowedScopes": null,
                "RedirectUris": null,
                "PostLogoutRedirectUris": null,
                "AllowedIdentityTokenSigningAlgorithms": null,
                "IdentityProviderRestrictions": null,
                "AllowedCorsOrigins": null,
                "Claims": null
            }
            """;
        var toUpdate = System.Text.Json.JsonSerializer.Deserialize<UpdateClient>(json)!;

        var updateResult = await admin.UpdateAsync(createResult.Id!, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var afterUpdate = await admin.GetAsync(createResult.Id!, _ct);
        afterUpdate.Found.ShouldBeTrue();
        afterUpdate.Item!.AllowedGrantTypes.ShouldBeEmpty();
        afterUpdate.Item!.AllowedScopes.ShouldBeEmpty();
        afterUpdate.Item!.RedirectUris.ShouldBeEmpty();
        afterUpdate.Item!.PostLogoutRedirectUris.ShouldBeEmpty();
        afterUpdate.Item!.AllowedIdentityTokenSigningAlgorithms.ShouldBeEmpty();
        afterUpdate.Item!.IdentityProviderRestrictions.ShouldBeEmpty();
        afterUpdate.Item!.AllowedCorsOrigins.ShouldBeEmpty();
        afterUpdate.Item!.Claims.ShouldBeEmpty();
    }

    [Fact]
    public void json_deserialization_of_create_client_can_set_list_properties_to_null()
    {
        var json = """
            {
                "ClientId": "test",
                "AllowedGrantTypes": null,
                "AllowedScopes": null,
                "RedirectUris": null,
                "PostLogoutRedirectUris": null,
                "AllowedIdentityTokenSigningAlgorithms": null,
                "IdentityProviderRestrictions": null,
                "AllowedCorsOrigins": null,
                "Claims": null,
                "ClientSecrets": null
            }
            """;

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<CreateClient>(json)!;

        deserialized.AllowedGrantTypes.ShouldBeNull();
        deserialized.AllowedScopes.ShouldBeNull();
        deserialized.RedirectUris.ShouldBeNull();
        deserialized.PostLogoutRedirectUris.ShouldBeNull();
        deserialized.AllowedIdentityTokenSigningAlgorithms.ShouldBeNull();
        deserialized.IdentityProviderRestrictions.ShouldBeNull();
        deserialized.AllowedCorsOrigins.ShouldBeNull();
        deserialized.Claims.ShouldBeNull();
        deserialized.ClientSecrets.ShouldBeNull();
    }

    [Fact]
    public async Task create_with_json_deserialized_null_list_properties_succeeds()
    {
        // Same scenario as the update test: a CreateClient deserialized from JSON
        // with explicit null list properties should be handled gracefully.
        var admin = _fixture.ClientAdmin;
        var clientId = $"client_{Guid.NewGuid():N}";
        var json = $$"""
            {
                "ClientId": "{{clientId}}",
                "AllowedGrantTypes": null,
                "AllowedScopes": null,
                "RedirectUris": null,
                "PostLogoutRedirectUris": null,
                "AllowedIdentityTokenSigningAlgorithms": null,
                "IdentityProviderRestrictions": null,
                "AllowedCorsOrigins": null,
                "Claims": null,
                "ClientSecrets": null
            }
            """;
        var client = System.Text.Json.JsonSerializer.Deserialize<CreateClient>(json)!;

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.AllowedGrantTypes.ShouldBeEmpty();
        getResult.Item!.AllowedScopes.ShouldBeEmpty();
        getResult.Item!.RedirectUris.ShouldBeEmpty();
        getResult.Item!.PostLogoutRedirectUris.ShouldBeEmpty();
        getResult.Item!.AllowedIdentityTokenSigningAlgorithms.ShouldBeEmpty();
        getResult.Item!.IdentityProviderRestrictions.ShouldBeEmpty();
        getResult.Item!.AllowedCorsOrigins.ShouldBeEmpty();
        getResult.Item!.Claims.ShouldBeEmpty();
    }

    [Fact]
    public async Task delete_client_then_get_returns_not_found()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var deleteResult = await admin.DeleteAsync(createResult.Id!, _ct);
        deleteResult.IsSuccess.ShouldBeTrue($"Delete failed: {deleteResult}");

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task delete_nonexistent_client_is_idempotent()
    {
        var admin = _fixture.ClientAdmin;
        var nonExistentId = ClientId.New();

        var result = await admin.DeleteAsync(nonExistentId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task create_with_empty_client_id_returns_required_error()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient { ClientId = "" };

        var result = await admin.CreateAsync(client, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "required" && e.PropertyNames != null && e.PropertyNames.Contains("ClientId"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task create_with_empty_client_name_returns_validation_error(string clientName)
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient
        {
            ClientId = $"client_{Guid.NewGuid():N}",
            ClientName = clientName
        };

        var result = await admin.CreateAsync(client, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "invalid_value" && e.PropertyNames != null && e.PropertyNames.Contains("ClientName"));
    }

    [Fact]
    public async Task create_with_grant_type_containing_spaces_returns_validation_error()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient
        {
            ClientId = $"client_{Guid.NewGuid():N}",
            AllowedGrantTypes = ["grant with spaces"]
        };

        var result = await admin.CreateAsync(client, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "invalid_value" && e.PropertyNames != null && e.PropertyNames.Contains("AllowedGrantTypes"));
    }

    [Fact]
    public async Task create_with_duplicate_grant_types_returns_validation_error()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient
        {
            ClientId = $"client_{Guid.NewGuid():N}",
            AllowedGrantTypes = [GrantType.ClientCredentials, GrantType.ClientCredentials]
        };

        var result = await admin.CreateAsync(client, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "invalid_value" && e.PropertyNames != null && e.PropertyNames.Contains("AllowedGrantTypes"));
    }

    [Theory]
    [InlineData(GrantType.Implicit, GrantType.AuthorizationCode)]
    [InlineData(GrantType.Implicit, GrantType.Hybrid)]
    [InlineData(GrantType.AuthorizationCode, GrantType.Hybrid)]
    public async Task create_with_invalid_grant_type_combo_returns_error(string grantType1, string grantType2)
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient
        {
            ClientId = $"client_{Guid.NewGuid():N}",
            AllowedGrantTypes = [grantType1, grantType2]
        };

        var result = await admin.CreateAsync(client, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e =>
            (e.Code == "validation_failed" || e.Code == "invalid_value") &&
            e.PropertyNames != null &&
            e.PropertyNames.Contains("AllowedGrantTypes"));
    }

    [Fact]
    public async Task update_client_preserves_existing_secrets()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient
        {
            ClientId = $"client_{Guid.NewGuid():N}",
            ClientName = "Original Name",
            RequireClientSecret = true,
            AllowedGrantTypes = [GrantType.ClientCredentials],
            ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret123" }]
        };

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();
        var originalSecrets = getResult.Item!.ClientSecrets;
        originalSecrets.ShouldNotBeNull();
        originalSecrets.Count.ShouldBe(1);
        var originalSecretId = originalSecrets[0].Id!;

        var toUpdate = getResult.Item!.ToUpdate();
        toUpdate.ClientName = "Updated Name";

        var updateResult = await admin.UpdateAsync(createResult.Id!, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var afterUpdate = await admin.GetAsync(createResult.Id!, _ct);
        afterUpdate.Found.ShouldBeTrue();
        afterUpdate.Item!.ClientName.ShouldBe("Updated Name");
        afterUpdate.Item!.ClientSecrets.ShouldNotBeNull();
        afterUpdate.Item!.ClientSecrets.Count.ShouldBe(1);
        afterUpdate.Item!.ClientSecrets[0].Id!.ShouldBe(originalSecretId);
    }

    [Fact]
    public async Task create_with_custom_validator_error_returns_error()
    {
        await using var provider = await StorageTestFixture.CreateProviderAsync(
            builder => builder.AddClientConfigurationValidator<RejectAllClientsValidator>(), _ct);

        using var scope = provider.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<IClientAdmin>();

        var result = await admin.CreateAsync(new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" }, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task create_secret_hashes_value_sha256()
    {
        var admin = _fixture.ClientAdmin;
        var clientId = await CreateClientAsync(admin, _ct);

        const string plaintext = "my-secret-value";
        var secretResult = await admin.CreateSecretAsync(
            clientId,
            new CreateClientSecret
            {
                PlaintextValue = plaintext,
                HashAlgorithm = SecretHashAlgorithm.Sha256
            },
            _ct);

        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var expectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));

        // Verify through IClientStore which exposes the hashed value
        var getResult = await admin.GetAsync(clientId, _ct);
        getResult.Found.ShouldBeTrue();

        var clientStore = _fixture.ClientStore;
        var client = await clientStore.FindClientByIdAsync(getResult.Item!.ClientId, _ct);
        client.ShouldNotBeNull();
        client.ClientSecrets.ShouldHaveSingleItem();
        client.ClientSecrets.First().Value.ShouldBe(expectedHash);
    }

    [Fact]
    public async Task create_secret_hashes_value_sha512()
    {
        var admin = _fixture.ClientAdmin;
        var clientId = await CreateClientAsync(admin, _ct);

        const string plaintext = "sha512-secret";
        var secretResult = await admin.CreateSecretAsync(
            clientId,
            new CreateClientSecret
            {
                PlaintextValue = plaintext,
                HashAlgorithm = SecretHashAlgorithm.Sha512,
                Description = "sha512 test"
            },
            _ct);

        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret (SHA512) failed: {secretResult}");
        secretResult.Id!.Value.ShouldNotBe(Guid.Empty);

        var expectedHash = Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(plaintext)));

        // Verify through IClientStore which exposes the hashed value
        var getResult = await admin.GetAsync(clientId, _ct);
        getResult.Found.ShouldBeTrue();

        var clientStore = _fixture.ClientStore;
        var client = await clientStore.FindClientByIdAsync(getResult.Item!.ClientId, _ct);
        client.ShouldNotBeNull();
        client.ClientSecrets.ShouldHaveSingleItem();
        client.ClientSecrets.First().Value.ShouldBe(expectedHash);
    }

    [Fact]
    public async Task create_secret_with_custom_type_hashes_value()
    {
        var admin = _fixture.ClientAdmin;
        var clientId = await CreateClientAsync(admin, _ct);

        const string plaintext = "custom-type-secret-value";
        var secretResult = await admin.CreateSecretAsync(
            clientId,
            new CreateClientSecret
            {
                PlaintextValue = plaintext,
                Type = "CustomFoo"
            },
            _ct);

        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var expectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));

        var getResult = await admin.GetAsync(clientId, _ct);
        getResult.Found.ShouldBeTrue();

        var clientStore = _fixture.ClientStore;
        var client = await clientStore.FindClientByIdAsync(getResult.Item!.ClientId, _ct);
        client.ShouldNotBeNull();
        client.ClientSecrets.ShouldHaveSingleItem();
        client.ClientSecrets.First().Value.ShouldBe(expectedHash);
    }

    [Fact]
    public async Task create_client_with_x509_thumbprint_secret_stores_raw_value()
    {
        var admin = _fixture.ClientAdmin;
        const string thumbprint = "E1F2A3B4C5D6E7F8091A2B3C4D5E6F708192A3B4";

        var result = await admin.CreateAsync(
            new CreateClient
            {
                ClientId = $"client_{Guid.NewGuid():N}",
                ClientSecrets =
                [
                    new CreateClientSecret
                    {
                        PlaintextValue = thumbprint,
                        Type = IdentityServerConstants.SecretTypes.X509CertificateThumbprint
                    }
                ]
            },
            _ct);
        result.IsSuccess.ShouldBeTrue($"CreateClient failed: {result}");

        var getResult = await admin.GetAsync(result.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var clientStore = _fixture.ClientStore;
        var client = await clientStore.FindClientByIdAsync(getResult.Item!.ClientId, _ct);
        client.ShouldNotBeNull();
        client.ClientSecrets.ShouldHaveSingleItem();
        var value = client.ClientSecrets.First().Value;
        value.ShouldBe(thumbprint);
        value.ShouldNotBe(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(thumbprint))));
    }

    [Fact]
    public async Task create_secret_with_x509_thumbprint_stores_raw_value()
    {
        var admin = _fixture.ClientAdmin;
        var clientId = await CreateClientAsync(admin, _ct);

        const string thumbprint = "E1F2A3B4C5D6E7F8091A2B3C4D5E6F708192A3B4";
        var secretResult = await admin.CreateSecretAsync(
            clientId,
            new CreateClientSecret
            {
                PlaintextValue = thumbprint,
                Type = IdentityServerConstants.SecretTypes.X509CertificateThumbprint
            },
            _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var getResult = await admin.GetAsync(clientId, _ct);
        getResult.Found.ShouldBeTrue();

        var clientStore = _fixture.ClientStore;
        var client = await clientStore.FindClientByIdAsync(getResult.Item!.ClientId, _ct);
        client.ShouldNotBeNull();
        client.ClientSecrets.ShouldHaveSingleItem();
        var value = client.ClientSecrets.First().Value;
        value.ShouldBe(thumbprint);
        value.ShouldNotBe(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(thumbprint))));
    }

    [Fact]
    public async Task create_client_with_x509_name_secret_stores_raw_value()
    {
        var admin = _fixture.ClientAdmin;
        const string subjectName = "CN=test.example.com";

        var result = await admin.CreateAsync(
            new CreateClient
            {
                ClientId = $"client_{Guid.NewGuid():N}",
                ClientSecrets =
                [
                    new CreateClientSecret
                    {
                        PlaintextValue = subjectName,
                        Type = IdentityServerConstants.SecretTypes.X509CertificateName
                    }
                ]
            },
            _ct);
        result.IsSuccess.ShouldBeTrue($"CreateClient failed: {result}");

        var getResult = await admin.GetAsync(result.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var clientStore = _fixture.ClientStore;
        var client = await clientStore.FindClientByIdAsync(getResult.Item!.ClientId, _ct);
        client.ShouldNotBeNull();
        client.ClientSecrets.ShouldHaveSingleItem();
        var value = client.ClientSecrets.First().Value;
        value.ShouldBe(subjectName);
        value.ShouldNotBe(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(subjectName))));
    }

    [Fact]
    public async Task create_secret_with_x509_name_stores_raw_value()
    {
        var admin = _fixture.ClientAdmin;
        var clientId = await CreateClientAsync(admin, _ct);

        const string subjectName = "CN=test.example.com";
        var secretResult = await admin.CreateSecretAsync(
            clientId,
            new CreateClientSecret
            {
                PlaintextValue = subjectName,
                Type = IdentityServerConstants.SecretTypes.X509CertificateName
            },
            _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var getResult = await admin.GetAsync(clientId, _ct);
        getResult.Found.ShouldBeTrue();

        var clientStore = _fixture.ClientStore;
        var client = await clientStore.FindClientByIdAsync(getResult.Item!.ClientId, _ct);
        client.ShouldNotBeNull();
        client.ClientSecrets.ShouldHaveSingleItem();
        var value = client.ClientSecrets.First().Value;
        value.ShouldBe(subjectName);
        value.ShouldNotBe(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(subjectName))));
    }

    [Fact]
    public async Task create_client_with_jwk_secret_stores_raw_value()
    {
        var admin = _fixture.ClientAdmin;
        using var rsa = RSA.Create(2048);
        using var publicRsa = RSA.Create();
        publicRsa.ImportParameters(rsa.ExportParameters(false));
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(publicRsa));
        jwk.D.ShouldBeNullOrEmpty();
        var material = System.Text.Json.JsonSerializer.Serialize(jwk);

        var result = await admin.CreateAsync(
            new CreateClient
            {
                ClientId = $"client_{Guid.NewGuid():N}",
                ClientSecrets =
                [
                    new CreateClientSecret
                    {
                        PlaintextValue = material,
                        Type = IdentityServerConstants.SecretTypes.JsonWebKey
                    }
                ]
            },
            _ct);
        result.IsSuccess.ShouldBeTrue($"CreateClient failed: {result}");

        var getResult = await admin.GetAsync(result.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var clientStore = _fixture.ClientStore;
        var client = await clientStore.FindClientByIdAsync(getResult.Item!.ClientId, _ct);
        client.ShouldNotBeNull();
        client.ClientSecrets.ShouldHaveSingleItem();
        var value = client.ClientSecrets.First().Value;
        value.ShouldBe(material);
        value.ShouldNotBe(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(material))));
    }

    [Fact]
    public async Task create_secret_with_jwk_stores_raw_value()
    {
        var admin = _fixture.ClientAdmin;
        var clientId = await CreateClientAsync(admin, _ct);

        using var rsa = RSA.Create(2048);
        using var publicRsa = RSA.Create();
        publicRsa.ImportParameters(rsa.ExportParameters(false));
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(publicRsa));
        jwk.D.ShouldBeNullOrEmpty();
        var material = System.Text.Json.JsonSerializer.Serialize(jwk);

        var secretResult = await admin.CreateSecretAsync(
            clientId,
            new CreateClientSecret
            {
                PlaintextValue = material,
                Type = IdentityServerConstants.SecretTypes.JsonWebKey
            },
            _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var getResult = await admin.GetAsync(clientId, _ct);
        getResult.Found.ShouldBeTrue();

        var clientStore = _fixture.ClientStore;
        var client = await clientStore.FindClientByIdAsync(getResult.Item!.ClientId, _ct);
        client.ShouldNotBeNull();
        client.ClientSecrets.ShouldHaveSingleItem();
        var value = client.ClientSecrets.First().Value;
        value.ShouldBe(material);
        value.ShouldNotBe(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(material))));
    }

    [Fact]
    public async Task create_client_with_x509_base64_secret_stores_raw_value()
    {
        var admin = _fixture.ClientAdmin;
        using var cert = TestCert.Load();
        var material = Convert.ToBase64String(cert.Export(X509ContentType.Cert));

        var result = await admin.CreateAsync(
            new CreateClient
            {
                ClientId = $"client_{Guid.NewGuid():N}",
                ClientSecrets =
                [
                    new CreateClientSecret
                    {
                        PlaintextValue = material,
                        Type = IdentityServerConstants.SecretTypes.X509CertificateBase64
                    }
                ]
            },
            _ct);
        result.IsSuccess.ShouldBeTrue($"CreateClient failed: {result}");

        var getResult = await admin.GetAsync(result.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var clientStore = _fixture.ClientStore;
        var client = await clientStore.FindClientByIdAsync(getResult.Item!.ClientId, _ct);
        client.ShouldNotBeNull();
        client.ClientSecrets.ShouldHaveSingleItem();
        var value = client.ClientSecrets.First().Value;
        value.ShouldBe(material);
        value.ShouldNotBe(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(material))));
    }

    [Fact]
    public async Task create_secret_with_x509_base64_stores_raw_value()
    {
        var admin = _fixture.ClientAdmin;
        var clientId = await CreateClientAsync(admin, _ct);

        using var cert = TestCert.Load();
        var material = Convert.ToBase64String(cert.Export(X509ContentType.Cert));

        var secretResult = await admin.CreateSecretAsync(
            clientId,
            new CreateClientSecret
            {
                PlaintextValue = material,
                Type = IdentityServerConstants.SecretTypes.X509CertificateBase64
            },
            _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var getResult = await admin.GetAsync(clientId, _ct);
        getResult.Found.ShouldBeTrue();

        var clientStore = _fixture.ClientStore;
        var client = await clientStore.FindClientByIdAsync(getResult.Item!.ClientId, _ct);
        client.ShouldNotBeNull();
        client.ClientSecrets.ShouldHaveSingleItem();
        var value = client.ClientSecrets.First().Value;
        value.ShouldBe(material);
        value.ShouldNotBe(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(material))));
    }

    [Fact]
    public async Task x509_thumbprint_secret_created_with_client_validates_successfully()
    {
        using var cert = TestCert.Load();
        var admin = _fixture.ClientAdmin;

        var result = await admin.CreateAsync(
            new CreateClient
            {
                ClientId = $"client_{Guid.NewGuid():N}",
                ClientSecrets =
                [
                    new CreateClientSecret
                    {
                        PlaintextValue = cert.Thumbprint,
                        Type = IdentityServerConstants.SecretTypes.X509CertificateThumbprint
                    }
                ]
            },
            _ct);
        result.IsSuccess.ShouldBeTrue($"CreateClient failed: {result}");

        var getResult = await admin.GetAsync(result.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var clientStore = _fixture.ClientStore;
        var client = await clientStore.FindClientByIdAsync(getResult.Item!.ClientId, _ct);
        client.ShouldNotBeNull();

        var validator = new X509ThumbprintSecretValidator(new Logger<X509ThumbprintSecretValidator>(new LoggerFactory()));
        var parsedSecret = new ParsedSecret
        {
            Id = client.ClientId,
            Credential = cert,
            Type = IdentityServerConstants.ParsedSecretTypes.X509Certificate
        };

        var validationResult = await validator.ValidateAsync(client.ClientSecrets, parsedSecret, _ct);

        validationResult.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task x509_thumbprint_secret_added_to_client_validates_successfully()
    {
        using var cert = TestCert.Load();
        var admin = _fixture.ClientAdmin;
        var clientId = await CreateClientAsync(admin, _ct);

        var secretResult = await admin.CreateSecretAsync(
            clientId,
            new CreateClientSecret
            {
                PlaintextValue = cert.Thumbprint,
                Type = IdentityServerConstants.SecretTypes.X509CertificateThumbprint
            },
            _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var getResult = await admin.GetAsync(clientId, _ct);
        getResult.Found.ShouldBeTrue();

        var clientStore = _fixture.ClientStore;
        var client = await clientStore.FindClientByIdAsync(getResult.Item!.ClientId, _ct);
        client.ShouldNotBeNull();

        var validator = new X509ThumbprintSecretValidator(new Logger<X509ThumbprintSecretValidator>(new LoggerFactory()));
        var parsedSecret = new ParsedSecret
        {
            Id = client.ClientId,
            Credential = cert,
            Type = IdentityServerConstants.ParsedSecretTypes.X509Certificate
        };

        var validationResult = await validator.ValidateAsync(client.ClientSecrets, parsedSecret, _ct);

        validationResult.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task x509_name_secret_created_with_client_validates_successfully()
    {
        using var ca = TestCertificates.CreateCa();
        using var cert = TestCertificates.CreateIssued("CN=identityserver_testing", ca);
        var admin = _fixture.ClientAdmin;

        var result = await admin.CreateAsync(
            new CreateClient
            {
                ClientId = $"client_{Guid.NewGuid():N}",
                ClientSecrets =
                [
                    new CreateClientSecret
                    {
                        PlaintextValue = cert.Subject,
                        Type = IdentityServerConstants.SecretTypes.X509CertificateName
                    }
                ]
            },
            _ct);
        result.IsSuccess.ShouldBeTrue($"CreateClient failed: {result}");

        var getResult = await admin.GetAsync(result.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var clientStore = _fixture.ClientStore;
        var client = await clientStore.FindClientByIdAsync(getResult.Item!.ClientId, _ct);
        client.ShouldNotBeNull();

        var validator = new X509NameSecretValidator(new Logger<X509NameSecretValidator>(new LoggerFactory()));
        var parsedSecret = new ParsedSecret
        {
            Id = client.ClientId,
            Credential = cert,
            Type = IdentityServerConstants.ParsedSecretTypes.X509Certificate
        };

        var validationResult = await validator.ValidateAsync(client.ClientSecrets, parsedSecret, _ct);

        validationResult.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task x509_name_secret_added_to_client_validates_successfully()
    {
        using var ca = TestCertificates.CreateCa();
        using var cert = TestCertificates.CreateIssued("CN=identityserver_testing", ca);
        var admin = _fixture.ClientAdmin;
        var clientId = await CreateClientAsync(admin, _ct);

        var secretResult = await admin.CreateSecretAsync(
            clientId,
            new CreateClientSecret
            {
                PlaintextValue = cert.Subject,
                Type = IdentityServerConstants.SecretTypes.X509CertificateName
            },
            _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var getResult = await admin.GetAsync(clientId, _ct);
        getResult.Found.ShouldBeTrue();

        var clientStore = _fixture.ClientStore;
        var client = await clientStore.FindClientByIdAsync(getResult.Item!.ClientId, _ct);
        client.ShouldNotBeNull();

        var validator = new X509NameSecretValidator(new Logger<X509NameSecretValidator>(new LoggerFactory()));
        var parsedSecret = new ParsedSecret
        {
            Id = client.ClientId,
            Credential = cert,
            Type = IdentityServerConstants.ParsedSecretTypes.X509Certificate
        };

        var validationResult = await validator.ValidateAsync(client.ClientSecrets, parsedSecret, _ct);

        validationResult.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task get_client_does_not_expose_secret_value()
    {
        var admin = _fixture.ClientAdmin;
        var clientId = await CreateClientAsync(admin, _ct);

        await admin.CreateSecretAsync(
            clientId,
            new CreateClientSecret
            {
                PlaintextValue = "super-secret",
                HashAlgorithm = SecretHashAlgorithm.Sha256
            },
            _ct);

        var getResult = await admin.GetAsync(clientId, _ct);
        getResult.Found.ShouldBeTrue();

        var secrets = getResult.Item!.ClientSecrets;
        secrets.ShouldNotBeNull();
        secrets.ShouldNotBeEmpty();

        var secret = secrets[0];
        secret.Id!.Value.ShouldNotBe(Guid.Empty);
        secret.Type.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task create_secret_with_empty_value_returns_required_error()
    {
        var admin = _fixture.ClientAdmin;
        var clientId = await CreateClientAsync(admin, _ct);

        var result = await admin.CreateSecretAsync(clientId, new CreateClientSecret { PlaintextValue = "" }, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "required");
    }

    [Fact]
    public async Task create_secret_for_nonexistent_client_returns_not_found()
    {
        var admin = _fixture.ClientAdmin;
        var nonExistentId = UuidV7.New().Value;

        var result = await admin.CreateSecretAsync(
            nonExistentId,
            new CreateClientSecret
            {
                PlaintextValue = "some-secret",
                HashAlgorithm = SecretHashAlgorithm.Sha256
            },
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "not_found");
    }

    [Fact]
    public async Task create_secret_rejected_by_custom_validator_is_not_stored()
    {
        await using var provider = await StorageTestFixture.CreateProviderAsync(
            builder => builder.AddClientConfigurationValidator<RejectSecretTypeValidator>(),
            _ct);
        using var scope = provider.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<IClientAdmin>();
        var clientId = await CreateClientAsync(admin, _ct);

        var result = await admin.CreateSecretAsync(
            clientId,
            new CreateClientSecret { PlaintextValue = "secret", Type = RejectSecretTypeValidator.RejectedType },
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
        (await admin.GetAsync(clientId, _ct)).Item!.ClientSecrets.ShouldBeEmpty();
    }

    [Fact]
    public async Task delete_secret_removes_it_from_client()
    {
        var admin = _fixture.ClientAdmin;
        var clientId = await CreateClientAsync(admin, _ct);

        var secretResult = await admin.CreateSecretAsync(
            clientId,
            new CreateClientSecret
            {
                PlaintextValue = "delete-me",
                HashAlgorithm = SecretHashAlgorithm.Sha256
            },
            _ct);
        secretResult.IsSuccess.ShouldBeTrue();

        var deleteResult = await admin.DeleteSecretAsync(clientId, secretResult.Id!, _ct);
        deleteResult.IsSuccess.ShouldBeTrue($"DeleteSecret failed: {deleteResult}");

        var getResult = await admin.GetAsync(clientId, _ct);
        getResult.Found.ShouldBeTrue();
        var secrets = getResult.Item!.ClientSecrets;
        secrets.ShouldNotBeNull();
        secrets.ShouldNotContain(s => s.Id! == secretResult.Id!);
    }

    [Fact]
    public async Task delete_only_secret_of_client_that_requires_secret_is_rejected()
    {
        await using var provider = await StorageTestFixture.CreateProviderAsync(_ => { }, _ct);
        using var scope = provider.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<IClientAdmin>();
        var apiScopeAdmin = scope.ServiceProvider.GetRequiredService<IApiScopeAdmin>();
        (await apiScopeAdmin.CreateAsync(new Duende.IdentityServer.Admin.ApiScopes.CreateApiScope { Name = "api1" }, _ct))
            .IsSuccess.ShouldBeTrue();

        var created = await admin.CreateAsync(new CreateClient
        {
            ClientId = $"client_{Guid.NewGuid():N}",
            RequireClientSecret = true,
            AllowedGrantTypes = [GrantType.ClientCredentials],
            AllowedScopes = ["api1"],
            ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
        }, _ct);
        created.IsSuccess.ShouldBeTrue($"Create failed: {created}");
        var secretId = (await admin.GetAsync(created.Id!, _ct)).Item!.ClientSecrets!.Single().Id!;

        var result = await admin.DeleteSecretAsync(created.Id!, secretId, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
        (await admin.GetAsync(created.Id!, _ct)).Item!.ClientSecrets!.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task delete_nonexistent_secret_returns_not_found()
    {
        var admin = _fixture.ClientAdmin;
        var clientId = await CreateClientAsync(admin, _ct);

        var result = await admin.DeleteSecretAsync(clientId, SecretId.New(), _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "not_found");
    }

    [Fact]
    public async Task query_with_no_filter_returns_all()
    {
        var prefix = $"q_{Guid.NewGuid():N}_";
        var admin = _fixture.ClientAdmin;

        await CreateQueryClient(admin, prefix + "a");
        await CreateQueryClient(admin, prefix + "b");
        await CreateQueryClient(admin, prefix + "c");

        var result = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(
                new ClientFilter { ClientId = prefix },
                DataRange.FromPage(1, 100)),
            _ct);

        result.Results.ShouldNotBeNull();
        result.Results.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task query_by_client_id_filter_returns_matching()
    {
        var uniquePart = $"q_{Guid.NewGuid():N}";
        var admin = _fixture.ClientAdmin;

        await CreateQueryClient(admin, uniquePart + "_match1");
        await CreateQueryClient(admin, uniquePart + "_match2");
        await CreateQueryClient(admin, $"other_{Guid.NewGuid():N}_nomatch");

        var result = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(
                new ClientFilter { ClientId = uniquePart }),
            _ct);

        result.Results.ShouldAllBe(c => c.ClientId.Contains(uniquePart));
        result.Results.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task query_by_enabled_filter_returns_matching()
    {
        var enabledId = $"q_enabled_{Guid.NewGuid():N}";
        var disabledId = $"q_disabled_{Guid.NewGuid():N}";
        var admin = _fixture.ClientAdmin;

        await CreateQueryClient(admin, enabledId, enabled: true);
        await CreateQueryClient(admin, disabledId, enabled: false);

        var enabledResult = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(
                new ClientFilter { ClientId = enabledId, Enabled = true }),
            _ct);

        enabledResult.Results.ShouldContain(c => c.ClientId == enabledId);
        enabledResult.Results.ShouldNotContain(c => c.ClientId == disabledId);

        var disabledResult = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(
                new ClientFilter { ClientId = disabledId, Enabled = false }),
            _ct);

        disabledResult.Results.ShouldContain(c => c.ClientId == disabledId);
        disabledResult.Results.ShouldNotContain(c => c.ClientId == enabledId);
    }

    [Fact]
    public async Task query_by_grant_type_filter_returns_matching()
    {
        var withCodeId = $"q_code_{Guid.NewGuid():N}";
        var withCcId = $"q_cc_{Guid.NewGuid():N}";
        var admin = _fixture.ClientAdmin;

        await CreateQueryClient(admin, withCodeId, grantTypes: [GrantType.AuthorizationCode]);
        await CreateQueryClient(admin, withCcId, grantTypes: [GrantType.ClientCredentials]);

        var result = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(
                new ClientFilter { GrantType = GrantType.AuthorizationCode }),
            _ct);

        result.Results.ShouldContain(c => c.ClientId == withCodeId);
        result.Results.ShouldNotContain(c => c.ClientId == withCcId);
    }

    [Fact]
    public async Task query_by_scope_filter_returns_matching()
    {
        var uniqueScope = $"scope_{Guid.NewGuid():N}";
        var withScopeId = $"q_withscope_{Guid.NewGuid():N}";
        var withoutScopeId = $"q_noscope_{Guid.NewGuid():N}";
        var admin = _fixture.ClientAdmin;
        await _fixture.EnsureApiScopesAsync(uniqueScope, "api1", "api2");

        await CreateQueryClient(admin, withScopeId, scopes: [uniqueScope, "api1"]);
        await CreateQueryClient(admin, withoutScopeId, scopes: ["api1", "api2"]);

        var result = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(
                new ClientFilter { AllowedScope = uniqueScope }),
            _ct);

        result.Results.ShouldContain(c => c.ClientId == withScopeId);
        result.Results.ShouldNotContain(c => c.ClientId == withoutScopeId);
    }

    [Fact]
    public async Task query_with_pagination_returns_correct_page()
    {
        var paginationPrefix = $"q_page_{Guid.NewGuid():N}_";
        var admin = _fixture.ClientAdmin;

        for (var i = 0; i < 5; i++)
        {
            await CreateQueryClient(admin, paginationPrefix + i);
        }

        var page1 = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(
                new ClientFilter { ClientId = paginationPrefix },
                DataRange.FromPage(1, 2)),
            _ct);

        page1.Results.Count.ShouldBe(2);

        var page2 = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(
                new ClientFilter { ClientId = paginationPrefix },
                DataRange.FromPage(2, 2)),
            _ct);

        page2.Results.Count.ShouldBe(2);

        var page1Ids = page1.Results.Select(c => c.Id!).ToHashSet();
        var page2Ids = page2.Results.Select(c => c.Id!).ToHashSet();
        page1Ids.Intersect(page2Ids).ShouldBeEmpty();
    }

    private async Task<ClientId> CreateQueryClient(
        IClientAdmin admin,
        string clientId,
        bool enabled = true,
        List<string>? grantTypes = null,
        List<string>? scopes = null)
    {
        var result = await admin.CreateAsync(new CreateClient
        {
            ClientId = clientId,
            Enabled = enabled,
            AllowedGrantTypes = grantTypes ?? [],
            AllowedScopes = scopes ?? [],
            RedirectUris = grantTypes?.Contains(GrantType.AuthorizationCode) == true
                ? [$"https://{clientId}.example.com/callback"]
                : [],
            ClientSecrets = grantTypes?.Any(x => !string.Equals(x, GrantType.Implicit, StringComparison.Ordinal)) == true
                ? [new CreateClientSecret { PlaintextValue = "secret" }]
                : []
        }, _ct);
        result.IsSuccess.ShouldBeTrue($"Seed failed: {result}");
        return result.Id!;
    }

    private sealed class RejectSecretTypeValidator : IClientConfigurationValidator
    {
        public const string RejectedType = "rejected-type";

        public Task ValidateAsync(ClientConfigurationValidationContext context, Ct ct)
        {
            if (context.Client.ClientSecrets.Any(s => s.Type == RejectedType))
            {
                context.SetError("Secret type is not allowed.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RejectAllClientsValidator : IClientConfigurationValidator
    {
        public Task ValidateAsync(ClientConfigurationValidationContext context, Ct ct)
        {
            context.SetError("Custom validator: all clients are rejected.");
            return Task.CompletedTask;
        }
    }

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();
}
