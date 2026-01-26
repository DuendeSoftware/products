// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace UnitTests.Stores;

public class InMemoryClientStoreTests
{
    [Fact]
    public void InMemoryClient_should_throw_if_contain_duplicate_client_ids()
    {
        var clients = new List<Client>
        {
            new Client { ClientId = "1"},
            new Client { ClientId = "1"},
            new Client { ClientId = "3"}
        };

        Action act = () => new InMemoryClientStore(clients);
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void InMemoryClient_should_not_throw_if_does_not_contain_duplicate_client_ids()
    {
        var clients = new List<Client>
        {
            new Client { ClientId = "1"},
            new Client { ClientId = "2"},
            new Client { ClientId = "3"}
        };

        new InMemoryClientStore(clients);
    }

#if NET10_0_OR_GREATER
    [Fact]
    public async Task GetAllClientsAsync_should_return_all_clients()
    {
        var clients = new List<Client>
        {
            new Client { ClientId = "client1", ClientName = "Client One" },
            new Client { ClientId = "client2", ClientName = "Client Two" },
            new Client { ClientId = "client3", ClientName = "Client Three" }
        };

        var store = new InMemoryClientStore(clients);

        var result = new List<Client>();
        await foreach (var client in store.GetAllClientsAsync())
        {
            result.Add(client);
        }

        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldContain(c => c.ClientId == "client1");
        result.ShouldContain(c => c.ClientId == "client2");
        result.ShouldContain(c => c.ClientId == "client3");
    }

    [Fact]
    public async Task GetAllClientsAsync_should_return_empty_when_no_clients()
    {
        var clients = new List<Client>();

        var store = new InMemoryClientStore(clients);

        var result = new List<Client>();
        await foreach (var client in store.GetAllClientsAsync())
        {
            result.Add(client);
        }

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }
#endif
}
