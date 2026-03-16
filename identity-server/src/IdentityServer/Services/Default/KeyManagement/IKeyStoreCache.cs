// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Services.KeyManagement;

/// <summary>
/// Interface to model caching keys loaded from key store.
/// </summary>
public interface ISigningKeyStoreCache
{
    /// <summary>
    /// Returns cached keys.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<IReadOnlyCollection<KeyContainer>?> GetKeysAsync(Ct ct);

    /// <summary>
    /// Caches keys for duration.
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="duration"></param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task StoreKeysAsync(IReadOnlyCollection<KeyContainer> keys, TimeSpan duration, Ct ct);
}
