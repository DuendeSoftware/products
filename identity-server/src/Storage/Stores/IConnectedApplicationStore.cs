// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Read-only store for unified access to all registered applications across protocols.
/// </summary>
public interface IConnectedApplicationStore
{
    /// <summary>
    /// Finds an application by its unique identifier (ClientId for OIDC, EntityId for SAML).
    /// </summary>
    /// <param name="identifier">The application identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The application, or null if not found.</returns>
    Task<IConnectedApplication?> FindByIdentifierAsync(string identifier, Ct ct);

    /// <summary>
    /// Returns all registered applications across all protocols.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>An async enumerable of all applications.</returns>
    IAsyncEnumerable<IConnectedApplication> GetAllAsync(Ct ct);
}
