// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Internal;

/// <summary>
/// Interface to model locking.
/// </summary>
public interface IConcurrencyLock<T>
{
    /// <summary>
    /// Locks. Returns false if lock was not obtained within in the timeout.
    /// </summary>
    Task<bool> LockAsync(int millisecondsTimeout);

    /// <summary>
    /// Unlocks
    /// </summary>
    void Unlock();
}
