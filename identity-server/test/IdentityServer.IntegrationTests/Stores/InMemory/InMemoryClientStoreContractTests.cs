// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.IntegrationTests.Stores.InMemory;

/// <summary>
/// InMemory contract tests for IClientStore.
/// Note: xUnit v3 creates a new instance per test method, so the backing list
/// starts empty for each test — providing natural test isolation.
/// </summary>
public class InMemoryClientStoreContractTests : ClientStoreContractTests
{
    private readonly List<Client> _clients = [];

    protected override StoreHandle<IClientStore> CreateStore() =>
        new(new InMemoryClientStore(_clients));

    protected override Task<StoreHandle<IClientStore>> CreateIsolatedStoreAsync() =>
        Task.FromResult(new StoreHandle<IClientStore>(new InMemoryClientStore([])));

    protected override Task SeedClientAsync(Client client)
    {
        _clients.Add(client);
        return Task.CompletedTask;
    }

    protected override Task SeedClientsAsync(IEnumerable<Client> clients)
    {
        _clients.AddRange(clients);
        return Task.CompletedTask;
    }
}
