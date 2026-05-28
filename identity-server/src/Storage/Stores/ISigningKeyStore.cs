// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Persists and retrieves serialized cryptographic signing keys used by the automatic
/// key management feature. IdentityServer uses this store to durably store keys so that
/// they survive server restarts and can be shared across multiple server instances.
/// Keys are stored as <see cref="SerializedKey"/> objects whose <c>Data</c> property
/// contains the authoritative, optionally data-protected, serialized key material.
/// </summary>
public interface ISigningKeyStore
{
    /// <summary>
    /// Returns all signing keys currently held in the store.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A read-only collection of all <see cref="SerializedKey"/> objects in the store.
    /// Returns an empty collection when no keys have been persisted.
    /// </returns>
    Task<IReadOnlyCollection<SerializedKey>> LoadKeysAsync(Ct ct);

    /// <summary>
    /// Persists a new signing key in the store.
    /// </summary>
    /// <param name="key">The serialized key to store.</param>
    /// <param name="ct">The cancellation token.</param>
    Task StoreKeyAsync(SerializedKey key, Ct ct);

    /// <summary>
    /// Removes the signing key with the specified identifier from the store.
    /// </summary>
    /// <param name="id">The unique identifier of the key to delete.</param>
    /// <param name="ct">The cancellation token.</param>
    Task DeleteKeyAsync(string id, Ct ct);
}
