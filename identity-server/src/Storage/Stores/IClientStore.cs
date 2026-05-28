// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Persists and retrieves client configuration. IdentityServer uses this store to
/// dynamically load <see cref="Client"/> configuration by client ID during protocol
/// flows such as authorization, token issuance, and introspection. Implement this
/// interface to load clients from any backing store (database, configuration file,
/// remote service, etc.).
/// </summary>
public interface IClientStore
{
    /// <summary>
    /// Finds a client by its client ID.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client to look up.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="Client"/> with the specified <paramref name="clientId"/>, or
    /// <see langword="null"/> if no matching client exists.
    /// </returns>
    Task<Client?> FindClientByIdAsync(string clientId, Ct ct);

    /// <summary>
    /// Returns all registered clients as an asynchronous stream. This is used for
    /// enumeration purposes such as conformance assessment and bulk operations.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> that yields every <see cref="Client"/>
    /// in the store.
    /// </returns>
    IAsyncEnumerable<Client> GetAllClientsAsync(Ct ct);
}
