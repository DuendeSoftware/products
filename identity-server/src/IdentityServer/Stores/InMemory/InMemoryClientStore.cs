// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// In-memory client store
/// </summary>
public class InMemoryClientStore : IClientStore
{
    private readonly IEnumerable<Client> _clients;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryClientStore"/> class.
    /// </summary>
    /// <param name="clients">The clients.</param>
    public InMemoryClientStore(IEnumerable<Client> clients)
    {
        if (clients.HasDuplicates(m => m.ClientId))
        {
            throw new ArgumentException("Clients must not contain duplicate ids");
        }
        _clients = clients;
    }

    /// <summary>
    /// Finds a client by id
    /// </summary>
    /// <param name="clientId">The client id</param>
    /// <returns>
    /// The client
    /// </returns>
    public Task<Client> FindClientByIdAsync(string clientId)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryClientStore.FindClientById");
        activity?.SetTag(Tracing.Properties.ClientId, clientId);

        var query =
            from client in _clients
            where client.ClientId == clientId
            select client;

        return Task.FromResult(query.SingleOrDefault());
    }

#if NET10_0_OR_GREATER
    /// <inheritdoc/>
    public async IAsyncEnumerable<Client> GetAllClientsAsync()
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryClientStore.GetAllClients");

        foreach (var client in _clients)
        {
            yield return client;
        }
    }
#endif
}

