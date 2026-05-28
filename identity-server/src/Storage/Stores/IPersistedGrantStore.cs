// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Persists, retrieves, and removes authorization grants that require server-side state.
/// A grant represents that a resource owner has given authorization of some kind. Grant
/// types stored here include authorization codes, refresh tokens, reference tokens, and
/// user consent. Specialized grant types such as device flow and CIBA use their own
/// dedicated stores instead.
/// <para>
/// IdentityServer ships with an in-memory implementation for testing and an Entity
/// Framework implementation for durable storage. Custom implementations can be provided
/// to support other data stores or to optimize data access for a specific environment.
/// </para>
/// </summary>
public interface IPersistedGrantStore
{
    /// <summary>
    /// Stores a new grant or replaces an existing grant with the same key.
    /// </summary>
    /// <param name="grant">The grant to persist.</param>
    /// <param name="ct">The cancellation token.</param>
    Task StoreAsync(PersistedGrant grant, Ct ct);

    /// <summary>
    /// Retrieves a grant by its unique key. The key is a hex-encoded SHA-256 hash of
    /// the protocol value (e.g., the authorization code or refresh token parameter)
    /// combined with the grant type.
    /// </summary>
    /// <param name="key">The unique key that identifies the grant.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="PersistedGrant"/> with the specified <paramref name="key"/>, or
    /// <see langword="null"/> if no matching grant exists.
    /// </returns>
    Task<PersistedGrant?> GetAsync(string key, Ct ct);

    /// <summary>
    /// Retrieves all grants that satisfy the conditions of the specified filter. Multiple
    /// filter properties are combined with logical AND. At least one filter property must
    /// be set.
    /// </summary>
    /// <param name="filter">
    /// A <see cref="PersistedGrantFilter"/> that constrains the query by subject ID,
    /// session ID, client ID, and/or grant type.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A read-only collection of <see cref="PersistedGrant"/> objects that match the
    /// filter. Returns an empty collection when no grants match.
    /// </returns>
    Task<IReadOnlyCollection<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter, Ct ct);

    /// <summary>
    /// Removes the grant identified by the specified key.
    /// </summary>
    /// <param name="key">The unique key of the grant to remove.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveAsync(string key, Ct ct);

    /// <summary>
    /// Removes all grants that satisfy the conditions of the specified filter. Multiple
    /// filter properties are combined with logical AND. At least one filter property must
    /// be set.
    /// </summary>
    /// <param name="filter">
    /// A <see cref="PersistedGrantFilter"/> that constrains which grants are removed,
    /// by subject ID, session ID, client ID, and/or grant type.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveAllAsync(PersistedGrantFilter filter, Ct ct);
}
