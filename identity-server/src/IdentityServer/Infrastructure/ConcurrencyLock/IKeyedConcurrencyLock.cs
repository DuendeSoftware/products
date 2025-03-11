// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Internal;

public interface IKeyedConcurrencyLock<T>
{
    /// <summary>
    /// Attempts to acquire a concurrency lock for a specified key within a given timeout.
    /// </summary>
    /// <param name="key">The key associated with the lock to be acquired.</param>
    /// <param name="millisecondsTimeout">The maximum time, in milliseconds, to wait for acquiring the lock.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a boolean indicating
    /// whether the lock was successfully acquired (true) or timed out (false).
    /// </returns>
    Task<bool> LockAsync(string key, int millisecondsTimeout);

    /// <summary>
    /// Releases the concurrency lock associated with the specified key.
    /// </summary>
    /// <param name="key">The key for which the lock should be released.</param>
    void Unlock(string key);
}
