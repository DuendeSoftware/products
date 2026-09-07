// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Spaces;

namespace Duende.IdentityServer.Services.KeyManagement;

/// <summary>
/// In-memory implementation of ISigningKeyStoreCache. This is a scoped facade over InMemoryKeyStoreCacheState.
/// </summary>
internal sealed class InMemoryKeyStoreCache(
    TimeProvider timeProvider,
    InMemoryKeyStoreCacheState state,
    ISpaceContextAccessor? spaceContextAccessor) : ISigningKeyStoreCache
{
    private string GetPartitionKey()
    {
        if (spaceContextAccessor is null || !spaceContextAccessor.IsSpaceIdConfigured())
        {
            return "__default__";
        }

        return spaceContextAccessor.GetSpaceId().Value.ToString();
    }

    /// <summary>
    /// Returns cached keys.
    /// </summary>
    /// <returns></returns>
    public Task<IReadOnlyCollection<KeyContainer>?> GetKeysAsync(Ct ct)
    {
        var partitionKey = GetPartitionKey();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var keys = state.GetKeys(partitionKey, now);
        return Task.FromResult(keys);
    }

    /// <summary>
    /// Caches keys for duration.
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="duration"></param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public Task StoreKeysAsync(IReadOnlyCollection<KeyContainer> keys, TimeSpan duration, Ct ct)
    {
        var partitionKey = GetPartitionKey();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.Add(duration);
        state.StoreKeys(partitionKey, keys, now, expires);
        return Task.CompletedTask;
    }
}
