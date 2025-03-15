// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Concurrent;

namespace Duende.IdentityServer.Internal;

/// <summary>
/// Represents a default implementation of a keyed concurrency lock,
/// allowing tasks to acquire and release locks based on specific keys.
/// </summary>
/// <typeparam name="T">The type associated with the keyed concurrency lock.</typeparam>
public class DefaultKeyedConcurrencyLock<T> : IKeyedConcurrencyLock<T>
{
    readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    /// <inheritdoc/>
    public Task<bool> LockAsync(string key, int millisecondsTimeout)
    {
        if (millisecondsTimeout <= 0)
        {
            throw new ArgumentException("millisecondsTimeout must be greater than zero.");
        }

        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        return semaphore.WaitAsync(millisecondsTimeout);
    }

    /// <inheritdoc/>
    public void Unlock(string key)
    {
        if (_locks.TryGetValue(key, out var semaphore))
        {
            semaphore.Release();
        }
    }
}
