// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Infrastructure.DistributedLock;

/// <summary>
/// Convenience overload of <see cref="IDistributedLock.TryAcquireAsync(LockName, TimeSpan, LeaseTiming, Ct)"/>
/// using <see cref="LeaseTiming.Default"/>.
/// </summary>
internal static class DistributedLockExtensions
{
    /// <inheritdoc cref="IDistributedLock.TryAcquireAsync(LockName, TimeSpan, LeaseTiming, Ct)"/>
    public static Task<ILockLease?> TryAcquireAsync(this IDistributedLock @lock, LockName name, TimeSpan waitTimeout, Ct ct) =>
        @lock.TryAcquireAsync(name, waitTimeout, LeaseTiming.Default, ct);
}
