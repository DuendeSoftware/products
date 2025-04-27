// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Interface to model storage of serialized keys.
/// </summary>
public interface ISigningKeyStore
{
    /// <summary>
    /// Returns all the keys in storage.
    /// </summary>
    Task<IEnumerable<SerializedKey>> LoadKeysAsync();

    /// <summary>
    /// Persists new key in storage.
    /// </summary>
    /// <param name="key"></param>
    Task StoreKeyAsync(SerializedKey key);

    /// <summary>
    /// Deletes key from storage.
    /// </summary>
    /// <param name="id"></param>
    Task DeleteKeyAsync(string id);
}
