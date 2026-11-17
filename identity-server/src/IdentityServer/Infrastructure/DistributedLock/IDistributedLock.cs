// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Infrastructure.DistributedLock;

/// <summary>
/// Coordinates work by name, either within this process only, or (when <see cref="IsDistributed"/>) across
/// every node sharing the same Duende Storage default instance or Spaces.
/// </summary>
internal interface IDistributedLock
{
    /// <summary>
    /// <see langword="true"/> when this lock coordinates across nodes (Duende Storage); <see langword="false"/>
    /// when it only coordinates within this process.
    /// </summary>
    bool IsDistributed { get; }

    /// <summary>
    /// Tries to acquire the named lock.
    /// </summary>
    /// <param name="name">The lock to acquire.</param>
    /// <param name="waitTimeout">
    /// How long to wait for the lock to become available. <see cref="TimeSpan.Zero"/> makes a single attempt
    /// without waiting, which is what background jobs use so a non-holder skips its run instead of blocking.
    /// </param>
    /// <param name="timing">How long the lease lasts and how it is renewed. Ignored when not distributed.</param>
    /// <param name="ct">Cancels the wait. Cancellation throws <see cref="OperationCanceledException"/> and leaves no lock held.</param>
    /// <returns>
    /// A lease to dispose when the work is done, or <see langword="null"/> if the lock could not be acquired
    /// before <paramref name="waitTimeout"/> elapsed.
    /// </returns>
    Task<ILockLease?> TryAcquireAsync(LockName name, TimeSpan waitTimeout, LeaseTiming timing, Ct ct);
}
