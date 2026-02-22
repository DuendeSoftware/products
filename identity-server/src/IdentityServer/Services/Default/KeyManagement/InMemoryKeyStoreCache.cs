// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Services.KeyManagement;

/// <summary>
/// In-memory implementation of ISigningKeyStoreCache based on static variables. This expects to be used as a singleton.
/// </summary>
internal class InMemoryKeyStoreCache : ISigningKeyStoreCache
{
    private readonly TimeProvider _timeProvider;

    private object _lock = new object();

    private DateTime _expires = DateTime.MinValue;
    private IEnumerable<KeyContainer> _cache;

    /// <summary>
    /// Constructor for InMemoryKeyStoreCache.
    /// </summary>
    /// <param name="timeProvider"></param>
    public InMemoryKeyStoreCache(TimeProvider timeProvider) => _timeProvider = timeProvider;

    /// <summary>
    /// Returns cached keys.
    /// </summary>
    /// <returns></returns>
    public Task<IEnumerable<KeyContainer>> GetKeysAsync(Ct ct)
    {
        DateTime expires;
        IEnumerable<KeyContainer> keys;

        lock (_lock)
        {
            expires = _expires;
            keys = _cache;
        }

        if (keys != null && expires >= _timeProvider.GetUtcNow().UtcDateTime)
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
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public Task StoreKeysAsync(IEnumerable<KeyContainer> keys, TimeSpan duration, Ct ct)
    {
        lock (_lock)
        {
            _expires = _timeProvider.GetUtcNow().UtcDateTime.Add(duration);
            _cache = keys;
        }

        return Task.CompletedTask;
    }
}
