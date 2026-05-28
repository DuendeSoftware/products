// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Runtime.CompilerServices;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace UnitTests.Stores;

public sealed class ConnectedApplicationStoreTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task FindByIdentifierAsync_WhenClientExists_ReturnsClient()
    {
        var client = new Client { ClientId = "oidc-client", ClientName = "OIDC Client" };
        var store = CreateStore(clients: [client]);

        var result = await store.FindByIdentifierAsync("oidc-client", _ct);

        result.ShouldNotBeNull();
        result.Identifier.ShouldBe("oidc-client");
        result.ShouldBeAssignableTo<Client>();
    }

    [Fact]
    public async Task FindByIdentifierAsync_WhenSamlSpExists_ReturnsSp()
    {
        var sp = new SamlServiceProvider { EntityId = "https://sp.example.com", DisplayName = "Test SP" };
        var store = CreateStore(samlSps: [sp]);

        var result = await store.FindByIdentifierAsync("https://sp.example.com", _ct);

        result.ShouldNotBeNull();
        result.Identifier.ShouldBe("https://sp.example.com");
        result.ShouldBeAssignableTo<SamlServiceProvider>();
    }

    [Fact]
    public async Task FindByIdentifierAsync_WhenNeitherExists_ReturnsNull()
    {
        var store = CreateStore();

        var result = await store.FindByIdentifierAsync("nonexistent", _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindByIdentifierAsync_WhenBothExist_ReturnsClientFirst()
    {
        // OIDC takes precedence — if the same identifier exists as both a ClientId and EntityId,
        // the Client is returned.
        var client = new Client { ClientId = "shared-id", ClientName = "OIDC" };
        var sp = new SamlServiceProvider { EntityId = "shared-id", DisplayName = "SAML" };
        var store = CreateStore(clients: [client], samlSps: [sp]);

        var result = await store.FindByIdentifierAsync("shared-id", _ct);

        result.ShouldNotBeNull();
        result.ShouldBeAssignableTo<Client>();
        result.DisplayName.ShouldBe("OIDC");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsClientsAndSps()
    {
        var client = new Client { ClientId = "c1", ClientName = "Client 1" };
        var sp = new SamlServiceProvider { EntityId = "https://sp1.example.com", DisplayName = "SP 1" };
        var store = CreateStore(clients: [client], samlSps: [sp]);

        var results = new List<IConnectedApplication>();
        await foreach (var app in store.GetAllAsync(_ct))
        {
            results.Add(app);
        }

        results.Count.ShouldBe(2);
        results.ShouldContain(a => a.Identifier == "c1");
        results.ShouldContain(a => a.Identifier == "https://sp1.example.com");
    }

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsNoResults()
    {
        var store = CreateStore();

        var results = new List<IConnectedApplication>();
        await foreach (var app in store.GetAllAsync(_ct))
        {
            results.Add(app);
        }

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_YieldsClientsBeforeSps()
    {
        var clients = new[]
        {
            new Client { ClientId = "c1" },
            new Client { ClientId = "c2" }
        };
        var sps = new[]
        {
            new SamlServiceProvider { EntityId = "sp1" },
            new SamlServiceProvider { EntityId = "sp2" }
        };
        var store = CreateStore(clients: clients, samlSps: sps);

        var identifiers = new List<string>();
        await foreach (var app in store.GetAllAsync(_ct))
        {
            identifiers.Add(app.Identifier);
        }

        identifiers.ShouldBe(["c1", "c2", "sp1", "sp2"]);
    }

    private static ConnectedApplicationStore CreateStore(
        IEnumerable<Client>? clients = null,
        IEnumerable<SamlServiceProvider>? samlSps = null) =>
        new(
            new StubClientStore(clients ?? []),
            new StubSamlServiceProviderStore(samlSps ?? []));

    private sealed class StubClientStore : IClientStore
    {
        private readonly IEnumerable<Client> _clients;

        public StubClientStore(IEnumerable<Client> clients) => _clients = clients;

        public Task<Client?> FindClientByIdAsync(string clientId, Ct ct) =>
            Task.FromResult(_clients.FirstOrDefault(c => c.ClientId == clientId));

        public async IAsyncEnumerable<Client> GetAllClientsAsync([EnumeratorCancellation] Ct ct)
        {
            foreach (var client in _clients)
            {
                yield return client;
            }

            await Task.CompletedTask;
        }
    }

    private sealed class StubSamlServiceProviderStore : ISamlServiceProviderStore
    {
        private readonly IEnumerable<SamlServiceProvider> _sps;

        public StubSamlServiceProviderStore(IEnumerable<SamlServiceProvider> sps) => _sps = sps;

        public Task<SamlServiceProvider?> FindByEntityIdAsync(string entityId, Ct ct) =>
            Task.FromResult(_sps.FirstOrDefault(sp => sp.EntityId == entityId));

        public async IAsyncEnumerable<SamlServiceProvider> GetAllSamlServiceProvidersAsync([EnumeratorCancellation] Ct ct)
        {
            foreach (var sp in _sps)
            {
                yield return sp;
            }

            await Task.CompletedTask;
        }
    }
}
