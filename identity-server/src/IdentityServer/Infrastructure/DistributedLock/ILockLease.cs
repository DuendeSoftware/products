// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Infrastructure.DistributedLock;

/// <summary>
/// A held lease on a <see cref="LockName"/>. Disposing releases the lock; dispose is idempotent.
/// </summary>
internal interface ILockLease : IAsyncDisposable
{
    /// <summary>
    /// A token that is cancelled when the lease is lost before it was explicitly released (for example,
    /// a renewal failure on a distributed lock). <see cref="InProcessDistributedLock"/> leases never lose
    /// their lease, so this is <see cref="Ct.None"/>.
    /// </summary>
    Ct Lost { get; }
}
