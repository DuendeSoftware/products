// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Retrieval of client configuration
/// </summary>
public interface IClientStore
{
    /// <summary>
    /// Finds a client by id
    /// </summary>
    /// <param name="clientId">The client id</param>
    /// <returns>The client</returns>
    Task<Client?> FindClientByIdAsync(string clientId);

#if NET10_0_OR_GREATER
    /// <summary>
    /// Returns all clients for enumeration purposes (e.g., conformance assessment).
    /// </summary>
    /// <returns>An async enumerable of all clients.</returns>
    IAsyncEnumerable<Client> GetAllClientsAsync();
#endif
}
