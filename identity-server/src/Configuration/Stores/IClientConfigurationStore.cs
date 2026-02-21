// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Interface for communication with the client configuration data store.
/// </summary>
public interface IClientConfigurationStore
{
    /// <summary>
    /// Adds a client to the configuration store.
    /// </summary>
    /// <param name="client">The client to add to the store</param>
    /// <param name="ct">The cancellation token.</param>
    Task AddAsync(Client client, CT ct);
}
