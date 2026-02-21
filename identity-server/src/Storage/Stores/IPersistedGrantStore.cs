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
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task StoreAsync(PersistedGrant grant, CT ct);

    /// <summary>
    /// Gets the grant.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<PersistedGrant?> GetAsync(string key, CT ct);

    /// <summary>
    /// Gets all grants based on the filter.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<IEnumerable<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter, CT ct);

    /// <summary>
    /// Removes the grant by key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task RemoveAsync(string key, CT ct);

    /// <summary>
    /// Removes all grants based on the filter.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task RemoveAllAsync(PersistedGrantFilter filter, CT ct);
}
