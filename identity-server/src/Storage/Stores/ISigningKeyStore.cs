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
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<IEnumerable<SerializedKey>> LoadKeysAsync(CT ct);

    /// <summary>
    /// Persists new key in storage.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task StoreKeyAsync(SerializedKey key, CT ct);

    /// <summary>
    /// Deletes key from storage.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task DeleteKeyAsync(string id, CT ct);
}
