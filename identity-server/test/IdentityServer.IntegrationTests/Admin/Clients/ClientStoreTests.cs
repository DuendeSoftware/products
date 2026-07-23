// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;
using SecretHashAlgorithm = Duende.IdentityServer.Admin.SecretHashAlgorithm;

namespace Duende.IdentityServer.IntegrationTests.Admin;

// These tests verify that data saved in the admin interface
// is made available to the IClientStore
public sealed class ClientStoreTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly List<IServiceScope> _scopes = [];


    private IClientAdmin BuildClientAdmin()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IClientAdmin>();
    }

    private IClientStore BuildClientStore()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IClientStore>();
    }

    [Fact]
    public async Task create_via_admin_then_find_by_client_id_returns_matching_client_model()
    {
        var admin = BuildClientAdmin();
        var clientStore = BuildClientStore();

        var clientId = $"client_{Guid.NewGuid():N}";
        var config = new CreateClient
        {
            ClientId = clientId,
            ClientName = "Integration Test Client",
            Enabled = true,
            RequireClientSecret = true,
            AllowedGrantTypes = [GrantType.ClientCredentials],
            AllowedScopes = ["api1"],
            AllowedCorsOrigins = ["https://app.example.com"],
            ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
        };

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var client = await clientStore.FindClientByIdAsync(clientId, _ct);

        client.ShouldNotBeNull();
        client.ClientId.ShouldBe(clientId);
        client.ClientName.ShouldBe("Integration Test Client");
        client.Enabled.ShouldBeTrue();
        client.RequireClientSecret.ShouldBeTrue();
        client.AllowedGrantTypes.ShouldContain(GrantType.ClientCredentials);
        client.AllowedScopes.ShouldContain("api1");
        client.AllowedCorsOrigins.ShouldContain("https://app.example.com");
    }

    [Fact]
    public async Task create_with_all_properties_then_find_maps_all_fields_correctly()
    {
        var admin = BuildClientAdmin();
        var clientStore = BuildClientStore();

        var clientId = $"client_{Guid.NewGuid():N}";
        var config = new CreateClient
        {
            ClientId = clientId,
            ClientName = "Full Properties Client",
            Description = "Full test client",
            ClientUri = "https://example.com",
            LogoUri = "https://example.com/logo.png",
            Enabled = true,
            RequireClientSecret = true,
            RequirePkce = false,
            AllowedGrantTypes = [GrantType.ClientCredentials],
            AllowedScopes = ["api1", "api2"],
            RedirectUris = ["https://app.example.com/callback"],
            AllowedCorsOrigins = ["https://app.example.com"],
            AccessTokenLifetime = 1800,
            IdentityTokenLifetime = 600,
            ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
        };

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var client = await clientStore.FindClientByIdAsync(clientId, _ct);

        client.ShouldNotBeNull();
        client.ClientId.ShouldBe(clientId);
        client.Enabled.ShouldBeTrue();
        client.RequireClientSecret.ShouldBeTrue();
        client.AllowedGrantTypes.ShouldContain(GrantType.ClientCredentials);
        client.AllowedScopes.ShouldContain("api1");
        client.AllowedScopes.ShouldContain("api2");
        client.RedirectUris.ShouldContain("https://app.example.com/callback");
        client.AllowedCorsOrigins.ShouldContain("https://app.example.com");
        client.AccessTokenLifetime.ShouldBe(1800);
        client.IdentityTokenLifetime.ShouldBe(600);
    }

    [Fact]
    public async Task create_then_update_via_admin_then_find_reflects_changes()
    {
        var admin = BuildClientAdmin();
        var clientStore = BuildClientStore();

        var clientId = $"client_{Guid.NewGuid():N}";
        var config = new CreateClient
        {
            ClientId = clientId,
            ClientName = "Before Update",
            RequireClientSecret = true,
            AllowedGrantTypes = [GrantType.ClientCredentials],
            AllowedScopes = ["api1"],
            ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
        };

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var updated = getResult.Item.ToUpdate();
        updated.ClientName = "After Update";
        updated.AllowedScopes = ["api1", "api2"];
        updated.AccessTokenLifetime = 7200;

        var updateResult = await admin.UpdateAsync(createResult.Id, updated, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var client = await clientStore.FindClientByIdAsync(clientId, _ct);

        client.ShouldNotBeNull();
        client.ClientName.ShouldBe("After Update");
        client.AllowedScopes.ShouldContain("api1");
        client.AllowedScopes.ShouldContain("api2");
        client.AccessTokenLifetime.ShouldBe(7200);
    }

    [Fact]
    public async Task delete_via_admin_then_find_by_client_id_returns_null()
    {
        var admin = BuildClientAdmin();
        var clientStore = BuildClientStore();

        var clientId = $"client_{Guid.NewGuid():N}";
        var config = new CreateClient
        {
            ClientId = clientId,
            RequireClientSecret = true,
            AllowedGrantTypes = [GrantType.ClientCredentials],
            AllowedScopes = ["api1"],
            ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
        };

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        // Verify it exists before deleting
        var before = await clientStore.FindClientByIdAsync(clientId, _ct);
        before.ShouldNotBeNull();

        var deleteResult = await admin.DeleteAsync(createResult.Id, _ct);
        deleteResult.IsSuccess.ShouldBeTrue($"Delete failed: {deleteResult}");

        var after = await clientStore.FindClientByIdAsync(clientId, _ct);
        after.ShouldBeNull();
    }


    [Fact]
    public async Task create_multiple_clients_get_all_returns_all()
    {
        var admin = BuildClientAdmin();
        var clientStore = BuildClientStore();

        // Use a common prefix to identify clients created in this test
        var prefix = $"batch_{Guid.NewGuid():N}_";
        var clientIds = Enumerable.Range(1, 3)
            .Select(i => $"{prefix}{i}")
            .ToList();

        foreach (var clientId in clientIds)
        {
            var result = await admin.CreateAsync(
                new CreateClient
                {
                    ClientId = clientId,
                    RequireClientSecret = true,
                    AllowedGrantTypes = [GrantType.ClientCredentials],
                    AllowedScopes = ["api1"],
                    ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
                },
                _ct);
            result.IsSuccess.ShouldBeTrue($"Create failed for {clientId}: {result}");
        }

        var allClients = await clientStore.GetAllClientsAsync(_ct).ToListAsync(_ct);

        // All three created client IDs must appear in the result
        foreach (var clientId in clientIds)
        {
            allClients.ShouldContain(c => c.ClientId == clientId, $"Expected client '{clientId}' in GetAllClientsAsync result");
        }
    }

    [Fact]
    public async Task get_all_clients_streams_all_via_async_enumerable()
    {
        var admin = BuildClientAdmin();
        var clientStore = BuildClientStore();

        var prefix = $"stream_{Guid.NewGuid():N}_";
        var clientIds = Enumerable.Range(1, 2)
            .Select(i => $"{prefix}{i}")
            .ToList();

        foreach (var clientId in clientIds)
        {
            var result = await admin.CreateAsync(
                new CreateClient
                {
                    ClientId = clientId,
                    RequireClientSecret = true,
                    AllowedGrantTypes = [GrantType.ClientCredentials],
                    AllowedScopes = ["api1"],
                    ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
                },
                _ct);
            result.IsSuccess.ShouldBeTrue($"Create failed for {clientId}: {result}");
        }

        // Consume the async enumerable manually to verify streaming works
        var streamed = new List<string>();
        await foreach (var client in clientStore.GetAllClientsAsync(_ct))
        {
            streamed.Add(client.ClientId);
        }

        // Every created client ID must have been yielded through the async stream
        foreach (var clientId in clientIds)
        {
            streamed.ShouldContain(clientId, $"Expected '{clientId}' in streamed results");
        }
    }

    [Fact]
    public async Task create_secret_via_admin_then_client_store_exposes_hashed_value()
    {
        var admin = BuildClientAdmin();
        var clientStore = BuildClientStore();

        var clientId = $"client_{Guid.NewGuid():N}";
        var storageId = await CreateClientAsync(admin, clientId);

        const string plaintext = "my-plaintext-secret";
        var secretResult = await admin.CreateSecretAsync(
            storageId,
            new CreateClientSecret
            {
                PlaintextValue = plaintext,
                HashAlgorithm = SecretHashAlgorithm.Sha256,
                Description = "test secret",
                Type = "SharedSecret"
            },
            _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var client = await clientStore.FindClientByIdAsync(clientId, _ct);

        client.ShouldNotBeNull();
        client.ClientSecrets.ShouldNotBeNull();
        client.ClientSecrets.ShouldHaveSingleItem();

        var secret = client.ClientSecrets.First();
        // The value must be hashed — not the plaintext
        secret.Value.ShouldNotBe(plaintext);
        // The type must be what was passed
        secret.Type.ShouldBe("SharedSecret");
    }

    [Fact]
    public async Task delete_secret_via_admin_then_client_store_shows_no_secrets()
    {
        var admin = BuildClientAdmin();
        var clientStore = BuildClientStore();

        var clientId = $"client_{Guid.NewGuid():N}";
        var storageId = await CreateClientAsync(admin, clientId);

        var secretResult = await admin.CreateSecretAsync(
            storageId,
            new CreateClientSecret
            {
                PlaintextValue = "secret-to-delete",
                HashAlgorithm = SecretHashAlgorithm.Sha256
            },
            _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        // Verify secret appears via store before deletion
        var before = await clientStore.FindClientByIdAsync(clientId, _ct);
        before.ShouldNotBeNull();
        before.ClientSecrets.ShouldNotBeEmpty();

        var deleteResult = await admin.DeleteSecretAsync(storageId, secretResult.Id, _ct);
        deleteResult.IsSuccess.ShouldBeTrue($"DeleteSecret failed: {deleteResult}");

        var after = await clientStore.FindClientByIdAsync(clientId, _ct);
        after.ShouldNotBeNull();
        after.ClientSecrets.ShouldBeEmpty();
    }

    [Fact]
    public async Task client_store_never_exposes_plaintext_secret()
    {
        var admin = BuildClientAdmin();
        var clientStore = BuildClientStore();

        var clientId = $"client_{Guid.NewGuid():N}";
        var storageId = await CreateClientAsync(admin, clientId);

        const string plaintext = "super-confidential-secret";
        var secretResult = await admin.CreateSecretAsync(
            storageId,
            new CreateClientSecret
            {
                PlaintextValue = plaintext,
                HashAlgorithm = SecretHashAlgorithm.Sha256
            },
            _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");

        var client = await clientStore.FindClientByIdAsync(clientId, _ct);

        client.ShouldNotBeNull();
        client.ClientSecrets.ShouldNotBeNull();
        client.ClientSecrets.ShouldHaveSingleItem();

        var secret = client.ClientSecrets.First();
        // IClientStore must NEVER expose the plaintext value
        secret.Value.ShouldNotBe(plaintext);
        // Value must be a non-empty hashed string
        secret.Value.ShouldNotBeNullOrWhiteSpace();
    }

    private async Task<Guid> CreateClientAsync(IClientAdmin admin, string clientId)
    {
        var result = await admin.CreateAsync(
            new CreateClient
            {
                ClientId = clientId,
                RequireClientSecret = false,
                AllowedGrantTypes = [GrantType.AuthorizationCode],
                AllowedScopes = ["api1"],
                RedirectUris = [$"https://{clientId}.example.com/callback"]
            },
            _ct);
        result.IsSuccess.ShouldBeTrue($"CreateClient failed: {result}");
        return result.Id;
    }
    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        await _fixture.DisposeAsync();
    }

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();
}
