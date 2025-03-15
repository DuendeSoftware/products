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
}
