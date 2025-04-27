// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Interface for persisting any type of grant.
/// </summary>
public interface IPersistedGrantStore
{
    /// <summary>
    /// Stores the grant.
    /// </summary>
    /// <param name="grant">The grant.</param>
    Task StoreAsync(PersistedGrant grant);

    /// <summary>
    /// Gets the grant.
    /// </summary>
    /// <param name="key">The key.</param>
    Task<PersistedGrant?> GetAsync(string key);

    /// <summary>
    /// Gets all grants based on the filter.
    /// </summary>
    /// <param name="filter">The filter.</param>
    Task<IEnumerable<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter);

    /// <summary>
    /// Removes the grant by key.
    /// </summary>
    /// <param name="key">The key.</param>
    Task RemoveAsync(string key);

    /// <summary>
    /// Removes all grants based on the filter.
    /// </summary>
    /// <param name="filter">The filter.</param>
    Task RemoveAllAsync(PersistedGrantFilter filter);
}
