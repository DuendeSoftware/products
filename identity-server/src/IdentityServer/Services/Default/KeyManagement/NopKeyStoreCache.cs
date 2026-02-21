// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Services.KeyManagement;

/// <summary>
/// Nop implementation of ISigningKeyStoreCache that does not cache keys.
/// </summary>
internal class NopKeyStoreCache : ISigningKeyStoreCache
{
    /// <summary>
    /// Returns null.
    /// </summary>
    /// <returns></returns>
    public Task<IEnumerable<KeyContainer>> GetKeysAsync(CT ct) => Task.FromResult<IEnumerable<KeyContainer>>(null);

    /// <summary>
    /// Does not cache keys.
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="duration"></param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public Task StoreKeysAsync(IEnumerable<KeyContainer> keys, TimeSpan duration, CT ct) => Task.CompletedTask;
}
