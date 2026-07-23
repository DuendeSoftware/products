// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.Admin.SamlServiceProviders;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// Integration tests verifying that IConnectedApplicationStore correctly
/// delegates to storage-backed IClientStore and ISamlServiceProviderStore.
/// </summary>
public sealed class ConnectedApplicationStoreTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task find_by_identifier_returns_client_created_via_admin()
    {
        var clientId = $"client_{Guid.NewGuid():N}";
        var config = new CreateClient
        {
            ClientId = clientId,
            ClientName = "Connected App Test Client",
            Enabled = true,
            RequireClientSecret = true,
            AllowedGrantTypes = [GrantType.ClientCredentials],
            AllowedScopes = ["api1"],
            ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
        };

        var createResult = await _fixture.ClientAdmin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var result = await _fixture.ConnectedApplicationStore.FindByIdentifierAsync(clientId, _ct);

        result.ShouldNotBeNull();
        result.Identifier.ShouldBe(clientId);
        result.DisplayName.ShouldBe("Connected App Test Client");
        result.ProtocolType.ShouldBe("oidc");
        result.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task find_by_identifier_returns_saml_sp_created_via_admin()
    {
        var entityId = $"https://sp-{Guid.NewGuid():N}.example.com";
        var config = new SamlServiceProviderConfiguration
        {
            EntityId = entityId,
            DisplayName = "Connected App Test SP",
            Enabled = true,
            AssertionConsumerServiceUrls =
            [
                new SamlIndexedEndpointConfiguration
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ],
            AllowedScopes = ["openid"]
        };

        var createResult = await _fixture.SamlServiceProviderAdmin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var result = await _fixture.ConnectedApplicationStore.FindByIdentifierAsync(entityId, _ct);

        result.ShouldNotBeNull();
        result.Identifier.ShouldBe(entityId);
        result.DisplayName.ShouldBe("Connected App Test SP");
        result.ProtocolType.ShouldBe("saml2p");
        result.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task find_by_identifier_returns_null_for_nonexistent()
    {
        var result = await _fixture.ConnectedApplicationStore.FindByIdentifierAsync("nonexistent-app", _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task find_by_identifier_returns_client_when_both_protocols_share_identifier()
    {
        var sharedId = $"shared-{Guid.NewGuid():N}";

        var clientConfig = new CreateClient
        {
            ClientId = sharedId,
            ClientName = "OIDC Winner",
            Enabled = true,
            RequireClientSecret = true,
            AllowedGrantTypes = [GrantType.ClientCredentials],
            AllowedScopes = ["api1"],
            ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
        };

        var samlConfig = new SamlServiceProviderConfiguration
        {
            EntityId = sharedId,
            DisplayName = "SAML Loser",
            AssertionConsumerServiceUrls =
            [
                new SamlIndexedEndpointConfiguration
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ],
            AllowedScopes = ["openid"]
        };

        (await _fixture.ClientAdmin.CreateAsync(clientConfig, _ct)).IsSuccess.ShouldBeTrue();
        (await _fixture.SamlServiceProviderAdmin.CreateAsync(samlConfig, _ct)).IsSuccess.ShouldBeTrue();

        var result = await _fixture.ConnectedApplicationStore.FindByIdentifierAsync(sharedId, _ct);

        result.ShouldNotBeNull();
        result.DisplayName.ShouldBe("OIDC Winner");
        result.ProtocolType.ShouldBe("oidc");
    }

    [Fact]
    public async Task get_all_returns_both_clients_and_saml_sps()
    {
        var clientId = $"client_{Guid.NewGuid():N}";
        var entityId = $"https://sp-{Guid.NewGuid():N}.example.com";

        var clientConfig = new CreateClient
        {
            ClientId = clientId,
            ClientName = "GetAll Client",
            Enabled = true,
            RequireClientSecret = true,
            AllowedGrantTypes = [GrantType.ClientCredentials],
            AllowedScopes = ["api1"],
            ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
        };

        var samlConfig = new SamlServiceProviderConfiguration
        {
            EntityId = entityId,
            DisplayName = "GetAll SP",
            AssertionConsumerServiceUrls =
            [
                new SamlIndexedEndpointConfiguration
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ],
            AllowedScopes = ["openid"]
        };

        (await _fixture.ClientAdmin.CreateAsync(clientConfig, _ct)).IsSuccess.ShouldBeTrue();
        (await _fixture.SamlServiceProviderAdmin.CreateAsync(samlConfig, _ct)).IsSuccess.ShouldBeTrue();

        var results = new List<IConnectedApplication>();
        await foreach (var app in _fixture.ConnectedApplicationStore.GetAllAsync(_ct))
        {
            results.Add(app);
        }

        results.ShouldContain(a => a.Identifier == clientId);
        results.ShouldContain(a => a.Identifier == entityId);
    }

    [Fact]
    public async Task get_all_yields_clients_before_saml_sps()
    {
        var clientId = $"client_{Guid.NewGuid():N}";
        var entityId = $"https://sp-{Guid.NewGuid():N}.example.com";

        var clientConfig = new CreateClient
        {
            ClientId = clientId,
            ClientName = "Order Client",
            Enabled = true,
            RequireClientSecret = true,
            AllowedGrantTypes = [GrantType.ClientCredentials],
            AllowedScopes = ["api1"],
            ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
        };

        var samlConfig = new SamlServiceProviderConfiguration
        {
            EntityId = entityId,
            DisplayName = "Order SP",
            AssertionConsumerServiceUrls =
            [
                new SamlIndexedEndpointConfiguration
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ],
            AllowedScopes = ["openid"]
        };

        // Create SAML first, client second — order in storage shouldn't matter
        (await _fixture.SamlServiceProviderAdmin.CreateAsync(samlConfig, _ct)).IsSuccess.ShouldBeTrue();
        (await _fixture.ClientAdmin.CreateAsync(clientConfig, _ct)).IsSuccess.ShouldBeTrue();

        var identifiers = new List<string>();
        await foreach (var app in _fixture.ConnectedApplicationStore.GetAllAsync(_ct))
        {
            identifiers.Add(app.Identifier);
        }

        // Clients should appear before SAML SPs regardless of creation order
        var clientIndex = identifiers.IndexOf(clientId);
        var samlIndex = identifiers.IndexOf(entityId);

        clientIndex.ShouldBeGreaterThanOrEqualTo(0);
        samlIndex.ShouldBeGreaterThanOrEqualTo(0);
        clientIndex.ShouldBeLessThan(samlIndex);
    }

    public ValueTask InitializeAsync() => _fixture.InitializeAsync();

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();
}
