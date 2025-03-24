// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Services.KeyManagement;

/// <summary>
/// In-memory implementation of ISigningKeyStoreCache based on static variables. This expects to be used as a singleton.
/// </summary>
internal class InMemoryKeyStoreCache : ISigningKeyStoreCache
{
    private readonly IClock _clock;

    private object _lock = new object();

    private DateTime _expires = DateTime.MinValue;
    private IEnumerable<KeyContainer> _cache;

    /// <summary>
    /// Constructor for InMemoryKeyStoreCache.
    /// </summary>
    /// <param name="clock"></param>
    public InMemoryKeyStoreCache(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Returns cached keys.
    /// </summary>
    /// <returns></returns>
    public Task<IEnumerable<KeyContainer>> GetKeysAsync()
    {
        DateTime expires;
        IEnumerable<KeyContainer> keys;

        lock (_lock)
        {
            expires = _expires;
            keys = _cache;
        }

        if (keys != null && expires >= _clock.UtcNow.UtcDateTime)
        {
            return Task.FromResult(keys);
        }

        return Task.FromResult<IEnumerable<KeyContainer>>(null);
    }

    /// <summary>
    /// Caches keys for duration.
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="duration"></param>
    /// <returns></returns>
    public Task StoreKeysAsync(IEnumerable<KeyContainer> keys, TimeSpan duration)
    {
        lock (_lock)
        {
            _expires = _clock.UtcNow.UtcDateTime.Add(duration);
            _cache = keys;
        }

        return Task.CompletedTask;
    }
}
