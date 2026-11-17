// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Concurrent;
using Duende.IdentityServer.Stores.Storage;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Infrastructure.DistributedLock;

/// <summary>
/// <see cref="IDistributedLock"/> backed by Duende Storage (the default instance, or Spaces, both of
/// which route through <see cref="IDistributedLockStorage"/>), coordinating across every node that
/// shares the same database. See the "Shutdown and cleanup" notes on the type for how it behaves
/// around <see cref="IHostApplicationLifetime.ApplicationStopping"/> and its own disposal.
/// </summary>
/// <remarks>
/// Implements both <see cref="IAsyncDisposable"/> and <see cref="IDisposable"/> because this type is
/// registered as a DI singleton, and many hosts and tests dispose their <see cref="IServiceProvider"/>
/// synchronously (<c>using var sp = services.BuildServiceProvider()</c>), which would otherwise throw
/// ("type only implements IAsyncDisposable"). Prefer <see cref="DisposeAsync"/>: it also releases any
/// lease still held at shutdown. The synchronous <see cref="Dispose"/> only stops local work (renewal
/// loops, pending acquires) and never touches the store, so any lease still held at that point is left
/// to expire on its own; see <see cref="Dispose"/>.
/// </remarks>
internal sealed class StorageDistributedLock : IDistributedLock, IAsyncDisposable, IDisposable
{
    /// <summary>
    /// How long leaked-lease release at shutdown, and a lease's own release, are allowed to take
    /// before giving up, so a hung store can't block a host's <c>StopAsync</c> indefinitely.
    /// </summary>
    internal static readonly TimeSpan ShutdownReleaseTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan InitialBackoff = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(1);

    private readonly IDistributedLockStorage _storage;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StorageDistributedLock> _logger;
    private readonly Guid _holderId = Guid.NewGuid();
    private readonly string _holderMachine = Environment.MachineName;
    private readonly KeyedSemaphore _gate;
    private readonly ConcurrentDictionary<StorageLockLease, byte> _leases = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly CancellationTokenSource _acquireStop = new();
    private readonly CancellationTokenRegistration _stoppingRegistration;
    private readonly Lock _registration = new();
    private int _disposed;
    private int _stopping;

    public StorageDistributedLock(
        IDistributedLockStorage storage,
        TimeProvider timeProvider,
        ILogger<StorageDistributedLock> logger,
        IHostApplicationLifetime? lifetime = null)
    {
        _storage = storage;
        _timeProvider = timeProvider;
        _gate = new KeyedSemaphore(timeProvider);
        _logger = logger;

        if (lifetime is not null)
        {
            _stoppingRegistration = lifetime.ApplicationStopping.Register(OnApplicationStopping);
        }
    }

    /// <inheritdoc />
    public bool IsDistributed => true;

    internal IDistributedLockStorage GetStorage() => _storage;

    private void OnApplicationStopping()
    {
        Interlocked.Exchange(ref _stopping, 1);
        try
        {
            _acquireStop.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed; nothing to cancel.
        }
    }

    /// <inheritdoc />
    public async Task<ILockLease?> TryAcquireAsync(LockName name, TimeSpan waitTimeout, LeaseTiming timing, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(timing);

        if (_disposed != 0 || _stopping != 0)
        {
            _logger.LockNotAcquiredApplicationStopping(name.Value);
            return null;
        }

        var deadline = _timeProvider.GetUtcNow() + waitTimeout;
        using var acquireLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _acquireStop.Token);

        IAsyncDisposable? gate;
#pragma warning disable CA2000 // Ownership of the releaser transfers to the returned lease, which disposes it.
        try
        {
            gate = await _gate.TryEnterAsync(name.Value, waitTimeout, acquireLinkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && _acquireStop.IsCancellationRequested)
        {
            // _acquireStop was cancelled (application stopping), not the caller's own token.
            _logger.LockNotAcquiredApplicationStopping(name.Value);
            return null;
        }
#pragma warning restore CA2000

        if (gate is null)
        {
            return null;
        }

        var gateReleased = false;
        try
        {
            var backoff = InitialBackoff;

            while (true)
            {
                if (_disposed != 0 || _stopping != 0)
                {
                    _logger.LockNotAcquiredApplicationStopping(name.Value);
                    return null;
                }

                (Guid EntityId, int Version, DateTimeOffset ExpiresAtUtc)? acquired;
                try
                {
                    acquired = await TryAcquireOnceAsync(name, timing, acquireLinkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested && _acquireStop.IsCancellationRequested)
                {
                    _logger.LockNotAcquiredApplicationStopping(name.Value);
                    return null;
                }

                // Slow storage calls can use up the lease's safe window before we return; a lease
                // that is already past its cutoff must not be handed out, and its record is released.
                if (acquired is not null && _timeProvider.GetUtcNow() >= acquired.Value.ExpiresAtUtc - timing.CancelBeforeExpiry)
                {
                    await ReleaseRefusedAsync(name, acquired.Value.EntityId, acquired.Value.Version).ConfigureAwait(false);
                    acquired = null;
                }

                if (acquired is not null)
                {
                    var (entityId, version, expiresAtUtc) = acquired.Value;
                    StorageLockLease? lease = null;

                    // Mutually exclusive with disposal's snapshot of _leases, so a lease is either
                    // refused or visible to disposal's cleanup.
                    lock (_registration)
                    {
                        if (_disposed == 0 && _stopping == 0)
                        {
#pragma warning disable CA2000 // Ownership transfers to the caller, or to disposal's cleanup via _leases.
                            lease = new StorageLockLease(
                                this,
                                name,
                                entityId,
                                version,
                                expiresAtUtc,
                                _holderId,
                                _holderMachine,
                                timing,
                                gate,
                                _logger,
                                _timeProvider,
                                _shutdown.Token);
#pragma warning restore CA2000
                            gateReleased = true; // gate ownership transfers to the lease
                            _leases[lease] = 0;
                        }
                    }

                    if (lease is null)
                    {
                        await ReleaseRefusedAsync(name, entityId, version).ConfigureAwait(false);
                        _logger.LockNotAcquiredApplicationStopping(name.Value);
                        return null;
                    }

                    _logger.LockAcquired(name.Value, version);
                    return lease;
                }

                _logger.LockBusy(name.Value);

                if (waitTimeout <= TimeSpan.Zero)
                {
                    return null;
                }

                var remaining = deadline - _timeProvider.GetUtcNow();
                if (remaining <= TimeSpan.Zero)
                {
                    return null;
                }

                var delay = backoff < remaining ? backoff : remaining;
#pragma warning disable CA5394 // Not security-sensitive: jitter on a retry backoff.
                var jitterFactor = 0.8 + (Random.Shared.NextDouble() * 0.4);
#pragma warning restore CA5394
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * jitterFactor);
                if (delay > remaining)
                {
                    delay = remaining;
                }
                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }

                try
                {
                    await Task.Delay(delay, _timeProvider, acquireLinkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested && _acquireStop.IsCancellationRequested)
                {
                    _logger.LockNotAcquiredApplicationStopping(name.Value);
                    return null;
                }

                backoff = TimeSpan.FromMilliseconds(Math.Min(backoff.TotalMilliseconds * 2, MaxBackoff.TotalMilliseconds));
            }
        }
        finally
        {
            if (!gateReleased)
            {
                await gate.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task ReleaseRefusedAsync(LockName name, Guid entityId, int version)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(ShutdownReleaseTimeout, _timeProvider);
            var key = DataStorageKey.Create(LockNameDskV1.Create(name));
            var dso = new DistributedLockDso.V1
            {
                Name = name.Value,
                HolderId = null,
                HolderMachine = null,
                ExpiresAtUtc = _timeProvider.GetUtcNow(),
            };

            await _storage.UpdateAsync(UuidV7.From(entityId), dso, version, key, timeoutCts.Token).ConfigureAwait(false);
        }
        catch
        {
            // Best effort: an unreleased record simply expires on its own.
        }
    }

    private async Task<(Guid EntityId, int Version, DateTimeOffset ExpiresAtUtc)?> TryAcquireOnceAsync(LockName name, LeaseTiming timing, Ct ct)
    {
        var storage = GetStorage();
        var key = DataStorageKey.Create(LockNameDskV1.Create(name));
        var now = _timeProvider.GetUtcNow();
        var newExpiry = now + timing.Duration;
        var newDso = new DistributedLockDso.V1
        {
            Name = name.Value,
            HolderId = _holderId,
            HolderMachine = _holderMachine,
            ExpiresAtUtc = newExpiry,
        };

        var found = await storage.TryReadAsync(key, ct).ConfigureAwait(false);

        if (!found.Found)
        {
            var newId = UuidV7.New();
            var createResult = await storage.CreateAsync(newId, newDso, key, ct).ConfigureAwait(false);
            return createResult == CreateResult.Success ? (newId.Value, 1, newExpiry) : null;
        }

        var dso = DistributedLockDso.ToCurrent(found.Dso);
        if (dso.HolderId is not null && dso.ExpiresAtUtc > now && dso.HolderId != _holderId)
        {
            return null;
        }

        var id = found.Id!.Value;
        var expectedVersion = found.Version!.Value;
        var updateResult = await storage.UpdateAsync(UuidV7.From(id), newDso, expectedVersion, key, ct).ConfigureAwait(false);
        return updateResult == UpdateResult.Success ? (id, expectedVersion + 1, newExpiry) : null;
    }

    internal void RemoveLease(StorageLockLease lease) => _leases.TryRemove(lease, out _);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lock (_registration)
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            Interlocked.Exchange(ref _stopping, 1);
        }

        try
        {
            await _acquireStop.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Nothing to cancel.
        }

        try
        {
            await _shutdown.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Nothing to cancel.
        }

        var leftOver = _leases.Keys.ToArray();
        var releaseTasks = leftOver.Select(async lease =>
        {
            _logger.ReleasingLeakedLeaseAtShutdown(lease.Name.Value);
            try
            {
                await lease.ReleaseAtLockShutdownAsync().ConfigureAwait(false);
            }
            catch
            {
                // Never throw from dispose: the lease will simply expire on its own.
            }
        });

        try
        {
            await Task.WhenAll(releaseTasks).ConfigureAwait(false);
        }
        catch
        {
            // Never throw from dispose.
        }

        _stoppingRegistration.Dispose();
        _acquireStop.Dispose();
        _shutdown.Dispose();
    }

    /// <summary>
    /// Synchronous disposal, for callers (hosts, tests) that dispose the DI container with
    /// <c>IDisposable.Dispose</c> rather than <c>IAsyncDisposable.DisposeAsync</c>. Stops renewal loops
    /// and pending acquires exactly as <see cref="DisposeAsync"/> does, but never calls the store: a
    /// release there would require awaiting I/O, which a synchronous <see cref="Dispose"/> must not do.
    /// Any lease still held is therefore left in place and simply expires on its own; a warning is
    /// logged once when that happens. Safe to call together with <see cref="DisposeAsync"/>, in either
    /// order: whichever runs first claims <c>_disposed</c> and performs the cleanup; the other becomes
    /// a no-op.
    /// </summary>
    public void Dispose()
    {
        lock (_registration)
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            Interlocked.Exchange(ref _stopping, 1);
        }

        try
        {
            _acquireStop.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Nothing to cancel.
        }

        try
        {
            _shutdown.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Nothing to cancel.
        }

        var leftOver = _leases.Keys.ToArray();
        if (leftOver.Length > 0)
        {
            _logger.LeasesNotReleasedOnSynchronousDispose(leftOver.Length);
        }

        _stoppingRegistration.Dispose();
        _acquireStop.Dispose();
        _shutdown.Dispose();
    }
}
