// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Internal;

/// <summary>
/// Interface to model locking.
/// </summary>
[Obsolete("IConcurrencyLock<T> is no longer used by IdentityServer. It is retained only for source compatibility with code that still injects it. It will be removed in a future major version.")]
public interface IConcurrencyLock<T>
{
    /// <summary>
    /// Locks. Returns false if lock was not obtained within in the timeout.
    /// </summary>
    /// <returns></returns>
    Task<bool> LockAsync(int millisecondsTimeout);

    /// <summary>
    /// Unlocks
    /// </summary>
    /// <returns></returns>
    void Unlock();
}
