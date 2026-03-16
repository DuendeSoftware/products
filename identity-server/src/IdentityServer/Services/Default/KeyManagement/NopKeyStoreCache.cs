// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

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
    public Task<IReadOnlyCollection<KeyContainer>?> GetKeysAsync(Ct _) => Task.FromResult<IReadOnlyCollection<KeyContainer>?>(null);

    /// <inheritdoc/>
    public Task StoreKeysAsync(IReadOnlyCollection<KeyContainer> keys, TimeSpan duration, Ct _) => Task.CompletedTask;
}
