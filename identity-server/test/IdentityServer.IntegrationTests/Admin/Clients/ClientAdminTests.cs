// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography;
using System.Text;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.IntegrationTests.Admin.Clients;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin;

public sealed class ClientAdminTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly List<IServiceScope> _scopes = [];


    private IClientAdmin NewAdmin()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IClientAdmin>();
    }

    private IClientStore NewClientStore()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IClientStore>();
    }

    private async Task<Guid> CreateClientAsync(IClientAdmin admin, Ct ct)
    {
        var result = await admin.CreateAsync(
            new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" },
            ct);
        result.IsSuccess.ShouldBeTrue($"CreateClient failed: {result}");
        return result.Id;
    }

    [Fact]
    public async Task create_client_and_get_by_id_round_trips_all_fields()
    {
        var admin = NewAdmin();
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

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var loaded = getResult.Item;
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
        var admin = NewAdmin();
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
        getResult.Item.ClientId.ShouldBe(clientId);
        getResult.Item.ClientName.ShouldBe("ByClientId Test");
    }

    [Fact]
    public async Task create_client_returns_storage_id_and_version()
    {
        var admin = NewAdmin();
        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };

        var result = await admin.CreateAsync(client, _ct);

        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");
        result.Id.ShouldNotBe(Guid.Empty);
        result.Version.ShouldNotBeNull();
        result.Version.Value.ShouldBe(1);
    }

    [Fact]
    public async Task create_duplicate_client_id_returns_already_exists()
    {
        var admin = NewAdmin();
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
        var admin = NewAdmin();
        var clientId = $"client_{Guid.NewGuid():N}";
        var client = new CreateClient
        {
            ClientId = clientId,
            ClientName = "Original Name",
            Description = "Original description"
        };

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var toUpdate = getResult.Item.ToUpdate();
        toUpdate.ClientName = "Updated Name";
        toUpdate.Description = "Updated description";
        toUpdate.Enabled = false;

        var updateResult = await admin.UpdateAsync(createResult.Id, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var afterUpdate = await admin.GetAsync(createResult.Id, _ct);
        afterUpdate.Found.ShouldBeTrue();
        afterUpdate.Item.ClientName.ShouldBe("Updated Name");
        afterUpdate.Item.Description.ShouldBe("Updated description");
        afterUpdate.Item.Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task update_client_increments_version()
    {
        var admin = NewAdmin();
        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue();
        createResult.Version!.Value.ShouldBe(1);

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var updateResult = await admin.UpdateAsync(createResult.Id, getResult.Item.ToUpdate(), getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");
        updateResult.Version!.Value.ShouldBe(2);
    }

    [Fact]
    public async Task update_with_wrong_version_returns_version_conflict()
    {
        var admin = NewAdmin();
        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var wrongVersion = (DataVersion)999;
        var updateResult = await admin.UpdateAsync(createResult.Id, getResult.Item.ToUpdate(), wrongVersion, _ct);

        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors.ShouldNotBeNull();
        updateResult.Errors.ShouldContain(e => e.Code == "version_conflict");
    }

    [Fact]
    public async Task update_nonexistent_client_returns_not_found()
    {
        var admin = NewAdmin();
        var nonExistentId = Guid.CreateVersion7();
        var client = new UpdateClient { ClientId = $"client_{Guid.NewGuid():N}" };

        var result = await admin.UpdateAsync(nonExistentId, client, (DataVersion)1, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "not_found");
    }

    [Fact]
    public async Task delete_client_then_get_returns_not_found()
    {
        var admin = NewAdmin();
        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var deleteResult = await admin.DeleteAsync(createResult.Id, _ct);
        deleteResult.IsSuccess.ShouldBeTrue($"Delete failed: {deleteResult}");

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task delete_nonexistent_client_is_idempotent()
    {
        var admin = NewAdmin();
        var nonExistentId = Guid.CreateVersion7();

        var result = await admin.DeleteAsync(nonExistentId, _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task create_with_empty_client_id_returns_required_error()
    {
        var admin = NewAdmin();
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
        var admin = NewAdmin();
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
        var admin = NewAdmin();
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
        var admin = NewAdmin();
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
        var admin = NewAdmin();
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
        var admin = NewAdmin();
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

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();
        var originalSecrets = getResult.Item.ClientSecrets;
        originalSecrets.ShouldNotBeNull();
        originalSecrets.Count.ShouldBe(1);
        var originalSecretId = originalSecrets[0].Id;

        var toUpdate = getResult.Item.ToUpdate();
        toUpdate.ClientName = "Updated Name";

        var updateResult = await admin.UpdateAsync(createResult.Id, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var afterUpdate = await admin.GetAsync(createResult.Id, _ct);
        afterUpdate.Found.ShouldBeTrue();
        afterUpdate.Item.ClientName.ShouldBe("Updated Name");
        afterUpdate.Item.ClientSecrets.ShouldNotBeNull();
        afterUpdate.Item.ClientSecrets.Count.ShouldBe(1);
        afterUpdate.Item.ClientSecrets[0].Id.ShouldBe(originalSecretId);
    }

    [Fact]
    public async Task create_with_custom_validator_error_returns_error()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var dbName = $"test_{Guid.NewGuid():N}";
        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"));

        services.AddIdentityServer()
            .AddConfigurationStorage()
            .AddInMemoryDataExtensionSchemas([])
            .AddClientConfigurationValidator<RejectAllClientsValidator>();

        await using var provider = services.BuildServiceProvider();

        var schema = provider.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(_ct);

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
        var admin = NewAdmin();
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

        var clientStore = NewClientStore();
        var client = await clientStore.FindClientByIdAsync(getResult.Item.ClientId, _ct);
        client.ShouldNotBeNull();
        client.ClientSecrets.ShouldHaveSingleItem();
        client.ClientSecrets.First().Value.ShouldBe(expectedHash);
    }

    [Fact]
    public async Task create_secret_hashes_value_sha512()
    {
        var admin = NewAdmin();
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
        secretResult.Id.ShouldNotBe(Guid.Empty);

        var expectedHash = Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(plaintext)));

        // Verify through IClientStore which exposes the hashed value
        var getResult = await admin.GetAsync(clientId, _ct);
        getResult.Found.ShouldBeTrue();

        var clientStore = NewClientStore();
        var client = await clientStore.FindClientByIdAsync(getResult.Item.ClientId, _ct);
        client.ShouldNotBeNull();
        client.ClientSecrets.ShouldHaveSingleItem();
        client.ClientSecrets.First().Value.ShouldBe(expectedHash);
    }

    [Fact]
    public async Task get_client_does_not_expose_secret_value()
    {
        var admin = NewAdmin();
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

        var secrets = getResult.Item.ClientSecrets;
        secrets.ShouldNotBeNull();
        secrets.ShouldNotBeEmpty();

        var secret = secrets[0];
        secret.Id.ShouldNotBe(Guid.Empty);
        secret.Type.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task create_secret_with_empty_value_returns_required_error()
    {
        var admin = NewAdmin();
        var clientId = await CreateClientAsync(admin, _ct);

        var result = await admin.CreateSecretAsync(clientId, new CreateClientSecret { PlaintextValue = "" }, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "required");
    }

    [Fact]
    public async Task create_secret_for_nonexistent_client_returns_not_found()
    {
        var admin = NewAdmin();
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
    public async Task delete_secret_removes_it_from_client()
    {
        var admin = NewAdmin();
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

        var deleteResult = await admin.DeleteSecretAsync(clientId, secretResult.Id, _ct);
        deleteResult.IsSuccess.ShouldBeTrue($"DeleteSecret failed: {deleteResult}");

        var getResult = await admin.GetAsync(clientId, _ct);
        getResult.Found.ShouldBeTrue();
        var secrets = getResult.Item.ClientSecrets;
        secrets.ShouldNotBeNull();
        secrets.ShouldNotContain(s => s.Id == secretResult.Id);
    }

    [Fact]
    public async Task delete_nonexistent_secret_returns_not_found()
    {
        var admin = NewAdmin();
        var clientId = await CreateClientAsync(admin, _ct);

        var result = await admin.DeleteSecretAsync(clientId, Guid.NewGuid(), _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "not_found");
    }

    [Fact]
    public async Task query_with_no_filter_returns_all()
    {
        var prefix = $"q_{Guid.NewGuid():N}_";
        var admin = NewAdmin();

        await CreateQueryClient(admin, prefix + "a");
        await CreateQueryClient(admin, prefix + "b");
        await CreateQueryClient(admin, prefix + "c");

        var result = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(
                new ClientFilter { ClientId = prefix },
                (DataRange)DataRange.FromPage(1, (DataRangeSize)100)),
            _ct);

        result.Items.ShouldNotBeNull();
        result.Items.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task query_by_client_id_filter_returns_matching()
    {
        var uniquePart = $"q_{Guid.NewGuid():N}";
        var admin = NewAdmin();

        await CreateQueryClient(admin, uniquePart + "_match1");
        await CreateQueryClient(admin, uniquePart + "_match2");
        await CreateQueryClient(admin, $"other_{Guid.NewGuid():N}_nomatch");

        var result = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(
                new ClientFilter { ClientId = uniquePart }),
            _ct);

        result.Items.ShouldAllBe(c => c.ClientId.Contains(uniquePart));
        result.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task query_by_enabled_filter_returns_matching()
    {
        var enabledId = $"q_enabled_{Guid.NewGuid():N}";
        var disabledId = $"q_disabled_{Guid.NewGuid():N}";
        var admin = NewAdmin();

        await CreateQueryClient(admin, enabledId, enabled: true);
        await CreateQueryClient(admin, disabledId, enabled: false);

        var enabledResult = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(
                new ClientFilter { ClientId = enabledId, Enabled = true }),
            _ct);

        enabledResult.Items.ShouldContain(c => c.ClientId == enabledId);
        enabledResult.Items.ShouldNotContain(c => c.ClientId == disabledId);

        var disabledResult = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(
                new ClientFilter { ClientId = disabledId, Enabled = false }),
            _ct);

        disabledResult.Items.ShouldContain(c => c.ClientId == disabledId);
        disabledResult.Items.ShouldNotContain(c => c.ClientId == enabledId);
    }

    [Fact]
    public async Task query_by_grant_type_filter_returns_matching()
    {
        var withCodeId = $"q_code_{Guid.NewGuid():N}";
        var withCcId = $"q_cc_{Guid.NewGuid():N}";
        var admin = NewAdmin();

        await CreateQueryClient(admin, withCodeId, grantTypes: [GrantType.AuthorizationCode]);
        await CreateQueryClient(admin, withCcId, grantTypes: [GrantType.ClientCredentials]);

        var result = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(
                new ClientFilter { GrantType = GrantType.AuthorizationCode }),
            _ct);

        result.Items.ShouldContain(c => c.ClientId == withCodeId);
        result.Items.ShouldNotContain(c => c.ClientId == withCcId);
    }

    [Fact]
    public async Task query_by_scope_filter_returns_matching()
    {
        var uniqueScope = $"scope_{Guid.NewGuid():N}";
        var withScopeId = $"q_withscope_{Guid.NewGuid():N}";
        var withoutScopeId = $"q_noscope_{Guid.NewGuid():N}";
        var admin = NewAdmin();

        await CreateQueryClient(admin, withScopeId, scopes: [uniqueScope, "api1"]);
        await CreateQueryClient(admin, withoutScopeId, scopes: ["api1", "api2"]);

        var result = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(
                new ClientFilter { AllowedScope = uniqueScope }),
            _ct);

        result.Items.ShouldContain(c => c.ClientId == withScopeId);
        result.Items.ShouldNotContain(c => c.ClientId == withoutScopeId);
    }

    [Fact]
    public async Task query_with_pagination_returns_correct_page()
    {
        var paginationPrefix = $"q_page_{Guid.NewGuid():N}_";
        var admin = NewAdmin();

        for (var i = 0; i < 5; i++)
        {
            await CreateQueryClient(admin, paginationPrefix + i);
        }

        var page1 = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(
                new ClientFilter { ClientId = paginationPrefix },
                (DataRange)DataRange.FromPage(1, (DataRangeSize)2)),
            _ct);

        page1.Items.Count.ShouldBe(2);

        var page2 = await admin.QueryAsync(
            QueryRequest.Create<ClientFilter, ClientSortField>(
                new ClientFilter { ClientId = paginationPrefix },
                (DataRange)DataRange.FromPage(2, (DataRangeSize)2)),
            _ct);

        page2.Items.Count.ShouldBe(2);

        var page1Ids = page1.Items.Select(c => c.Id).ToHashSet();
        var page2Ids = page2.Items.Select(c => c.Id).ToHashSet();
        page1Ids.Intersect(page2Ids).ShouldBeEmpty();
    }

    private async Task<Guid> CreateQueryClient(
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
            AllowedGrantTypes = grantTypes,
            AllowedScopes = scopes,
            RedirectUris = grantTypes?.Contains(GrantType.AuthorizationCode) == true
                ? [$"https://{clientId}.example.com/callback"]
                : null,
            ClientSecrets = grantTypes?.Any(x => !string.Equals(x, GrantType.Implicit, StringComparison.Ordinal)) == true
                ? [new CreateClientSecret { PlaintextValue = "secret" }]
                : null
        }, _ct);
        result.IsSuccess.ShouldBeTrue($"Seed failed: {result}");
        return result.Id;
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

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        await _fixture.DisposeAsync();
    }
}
