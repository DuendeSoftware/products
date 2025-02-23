// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Generic;
using Duende.IdentityServer.Models;

namespace IdentityServerHost.Configuration;

public static class Clients
{
    public static IEnumerable<Client> Get()
    {
        var clients = new List<Client>();

        clients.AddRange(ClientsConsole.Get());
        clients.AddRange(ClientsWeb.Get());

        return clients;
    }
}