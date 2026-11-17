// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Stores.Storage;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Infrastructure.DistributedLock;

/// <summary>
/// A held lease on a Storage-backed <see cref="LockName"/>, renewed in the background until it is
/// disposed, lost (a renewal failure that can't recover before the lease would expire), or the
/// owning <see cref="StorageDistributedLock"/> is disposed.
/// </summary>
internal sealed class StorageLockLease : ILockLease
{
    private readonly StorageDistributedLock _owner;
    private readonly LockName _name;
    private readonly Guid _entityId;
    private readonly Guid _holderId;
    private readonly string _holderMachine;
    private readonly LeaseTiming _timing;
    private readonly IAsyncDisposable _gate;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _renewalCts;
    private readonly CancellationTokenSource _lost = new();
    private readonly Ct _lostToken;
    private readonly ITimer _lossTimer;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly Task _renewalTask;

    private int _version;
    private DateTimeOffset _expiresAtUtc;
    private readonly Ct _shutdownToken;
    private int _disposed;

    internal StorageLockLease(
        StorageDistributedLock owner,
        LockName name,
        Guid entityId,
        int version,
        DateTimeOffset expiresAtUtc,
        Guid holderId,
        string holderMachine,
        LeaseTiming timing,
        IAsyncDisposable gate,
        ILogger logger,
        TimeProvider timeProvider,
        Ct shutdownToken)
    {
        _owner = owner;
        _name = name;
        _entityId = entityId;
        _version = version;
        _expiresAtUtc = expiresAtUtc;
        _holderId = holderId;
        _holderMachine = holderMachine;
        _timing = timing;
        _gate = gate;
        _logger = logger;
        _timeProvider = timeProvider;
        _lostToken = _lost.Token;
        _shutdownToken = shutdownToken;
        _renewalCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
        _lossTimer = timeProvider.CreateTimer(_ => CancelLost(), null, UntilLossDeadline(), Timeout.InfiniteTimeSpan);
        _renewalTask = RunRenewalLoopAsync(_renewalCts.Token);
    }

    internal LockName Name => _name;

    /// <inheritdoc />
    public Ct Lost => _lostToken;

    private TimeSpan UntilLossDeadline()
    {
        var remaining = _expiresAtUtc - _timing.CancelBeforeExpiry - _timeProvider.GetUtcNow();
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private async Task RunRenewalLoopAsync(Ct ct)
    {
        while (true)
        {
            try
            {
                await Task.Delay(_timing.RenewalInterval, _timeProvider, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Lease disposed, or the lock's shutdown token was cancelled: stop renewing without
                // treating this as a lost lease.
                return;
            }

            await _mutex.WaitAsync(Ct.None).ConfigureAwait(false);
            try
            {
                if (_disposed != 0)
                {
                    return;
                }

                var now = _timeProvider.GetUtcNow();
                if (_lostToken.IsCancellationRequested || now >= _expiresAtUtc - _timing.CancelBeforeExpiry)
                {
                    _logger.LeaseLost(_name.Value);
                    CancelLost();
                    return;
                }

                try
                {
                    var storage = _owner.GetStorage();
                    var key = DataStorageKey.Create(LockNameDskV1.Create(_name));
                    var newExpiry = now + _timing.Duration;
                    var dso = new DistributedLockDso.V1
                    {
                        Name = _name.Value,
                        HolderId = _holderId,
                        HolderMachine = _holderMachine,
                        ExpiresAtUtc = newExpiry,
                    };

                    // A renewal still running at the loss deadline is cancelled: the lease can't be
                    // trusted past it, and a hung store must not delay Lost.
                    using var renewalLinked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lostToken);
                    var result = await storage.UpdateAsync(UuidV7.From(_entityId), dso, _version, key, renewalLinked.Token).ConfigureAwait(false);
                    if (result == UpdateResult.Success && !_lostToken.IsCancellationRequested)
                    {
                        _version++;
                        _expiresAtUtc = newExpiry;
                        _lossTimer.Change(UntilLossDeadline(), Timeout.InfiniteTimeSpan);
                        _logger.LeaseRenewed(_name.Value, _version);
                    }
                    else
                    {
                        _logger.LeaseLost(_name.Value);
                        CancelLost();
                        return;
                    }
                }
                catch (OperationCanceledException) when (_lostToken.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    _logger.LeaseLost(_name.Value);
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Keep retrying every RenewalInterval until the loop's top-of-iteration check
                    // above determines we're past ExpiresAtUtc - CancelBeforeExpiry.
                    _logger.LeaseRenewalFailed(ex, _name.Value);
                }
            }
            finally
            {
                _mutex.Release();
            }
        }
    }

    private void CancelLost()
    {
        try
        {
            _lost.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed; nothing to cancel.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Once the owning lock is stopping, its shutdown token is cancelled and may already be disposed,
        // so linking it would throw. A cancelled token gives the same outcome: no store release, the
        // record expires on its own (or the lock's own disposal has already released it).
        if (_shutdownToken.IsCancellationRequested)
        {
            await DisposeCoreAsync(new Ct(canceled: true)).ConfigureAwait(false);
            return;
        }

        using var timeoutCts = new CancellationTokenSource(StorageDistributedLock.ShutdownReleaseTimeout, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, _shutdownToken);
        await DisposeCoreAsync(linked.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases this lease as part of the owning lock's shutdown cleanup, for a lease its caller
    /// never disposed. Uses its own bounded timeout, not the (already cancelled) lock shutdown
    /// token, so a hung store can't extend the lock's own <see cref="StorageDistributedLock.DisposeAsync"/>.
    /// </summary>
    internal async Task ReleaseAtLockShutdownAsync()
    {
        using var timeoutCts = new CancellationTokenSource(StorageDistributedLock.ShutdownReleaseTimeout, _timeProvider);
        await DisposeCoreAsync(timeoutCts.Token).ConfigureAwait(false);
    }

    private async ValueTask DisposeCoreAsync(Ct releaseCt)
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _lossTimer.DisposeAsync().ConfigureAwait(false);
        await _renewalCts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _renewalTask.ConfigureAwait(false);
        }
        catch
        {
            // Never throw from dispose.
        }
        finally
        {
            _renewalCts.Dispose();
        }

        await _mutex.WaitAsync(Ct.None).ConfigureAwait(false);
        try
        {
            if (!_lost.IsCancellationRequested)
            {
                try
                {
                    var storage = _owner.GetStorage();
                    var key = DataStorageKey.Create(LockNameDskV1.Create(_name));
                    var dso = new DistributedLockDso.V1
                    {
                        Name = _name.Value,
                        HolderId = null,
                        HolderMachine = null,
                        ExpiresAtUtc = _timeProvider.GetUtcNow(),
                    };

                    var result = await storage.UpdateAsync(UuidV7.From(_entityId), dso, _version, key, releaseCt).ConfigureAwait(false);
                    if (result == UpdateResult.Success)
                    {
                        _logger.LeaseReleased(_name.Value);
                    }
                    else
                    {
                        _logger.LeaseReleaseNotSuccessful(_name.Value, result);
                    }
                }
                catch (Exception ex)
                {
                    // Never throw from dispose: an unreleased lease simply expires on its own.
                    _logger.LeaseReleaseFailed(ex, _name.Value);
                }
            }
        }
        finally
        {
            _mutex.Release();
            _mutex.Dispose();
        }

        await _gate.DisposeAsync().ConfigureAwait(false);
        _lost.Dispose();
        _owner.RemoveLease(this);
    }
}
