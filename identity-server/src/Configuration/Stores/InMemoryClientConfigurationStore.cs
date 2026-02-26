// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// In-Memory implementation of the client configuration store.
/// </summary>
/// <remark> This implementation of the store is intended for demos and tests,
/// but is almost certainly inappropriate for production use, as the dynamically
/// registered clients are not actually persisted anywhere outside memory.
/// </remark>
public class InMemoryClientConfigurationStore : IClientConfigurationStore
{
    private readonly ICollection<Client> _clients;

    /// <summary>
    /// Instantiates a new instance of the InMemoryClientConfigurationStore.
    /// </summary>
    /// <param name="clients">The in memory clients, which must be originally
    /// registered in the DI system as an ICollection.</param>
    public InMemoryClientConfigurationStore(ICollection<Client> clients) => _clients = clients;
    /// <inheritdoc/>
    public Task AddAsync(Client client, Ct _)
    {
        if (_clients.Select(c => c.ClientId).Contains(client.ClientId))
        {
            throw new Exception($"Attempted to add duplicate client id {client.ClientId} to the in memory clients");
        }
        _clients.Add(client);
        return Task.CompletedTask;
    }
}
