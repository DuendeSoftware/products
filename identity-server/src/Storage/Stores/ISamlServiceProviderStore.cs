// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Interface for retrieval of SAML Service Provider configuration.
/// </summary>
public interface ISamlServiceProviderStore
{
    /// <summary>
    /// Finds a SAML Service Provider by its entity identifier.
    /// </summary>
    /// <param name="entityId">The entity identifier of the Service Provider.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The Service Provider, or null if not found.</returns>
    Task<SamlServiceProvider?> FindByEntityIdAsync(string entityId, Ct ct);

    /// <summary>
    /// Returns all SAML service providers for enumeration purposes.
    /// </summary>
    IAsyncEnumerable<SamlServiceProvider> GetAllSamlServiceProvidersAsync(Ct ct);
}
