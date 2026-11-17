// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Infrastructure.DistributedLock;

/// <summary>
/// Default <see cref="IDistributedLock"/> that only coordinates within this process, using a
/// <see cref="KeyedSemaphore"/> keyed on <see cref="LockName.Value"/>. Used when no Duende Storage default
/// instance or Spaces is registered; every node then runs jobs and creates keys independently.
/// </summary>
internal sealed class InProcessDistributedLock : IDistributedLock
{
    private readonly KeyedSemaphore _semaphore = new();

    /// <inheritdoc />
    public bool IsDistributed => false;

    /// <inheritdoc />
    /// <remarks><paramref name="timing"/> is ignored: in-process leases don't outlive the process.</remarks>
#pragma warning disable CA2000 // Ownership of the releaser transfers to the returned Lease, which disposes it.
    public async Task<ILockLease?> TryAcquireAsync(LockName name, TimeSpan waitTimeout, LeaseTiming timing, Ct ct)
    {
        var releaser = await _semaphore.TryEnterAsync(name.Value, waitTimeout, ct);
        return releaser is null ? null : new Lease(releaser);
    }
#pragma warning restore CA2000

    private sealed class Lease(IAsyncDisposable releaser) : ILockLease
    {
        public Ct Lost => Ct.None;

        public ValueTask DisposeAsync() => releaser.DisposeAsync();
    }
}
