// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Infrastructure.DistributedLock;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.Coordination;

/// <summary>
/// Runs a background job's cycle so that, when <see cref="IDistributedLock.IsDistributed"/>, at most one
/// node in the cluster runs it at a time. Without a distributed lock, every node runs every cycle, exactly
/// like today. One instance is owned by a single hosted service and is not safe to call <see cref="RunAsync"/>
/// concurrently with itself, but <see cref="ReleaseAsync"/> may be called from <c>StopAsync</c> while, or
/// right after, a run is in progress.
/// </summary>
internal sealed class SingleNodeJob(
    LockName name,
    LeaseReleaseMode mode,
    Func<TimeSpan> interval,
    Func<BackgroundJobLeaseOptions> options,
    IDistributedLock @lock,
    TimeProvider timeProvider,
    ILogger logger)
{
    private static readonly TimeSpan MinDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MinCancelBeforeExpiry = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxJitter = TimeSpan.FromSeconds(30);

    private readonly Lock _runGate = new();
    private ILockLease? _lease;
    private bool _running;
    private bool _releaseRequested;
    private bool _firstRun = true;
    private bool _timingWarned;

    /// <summary>The clock this job resolves timing and jitter against.</summary>
    internal TimeProvider TimeProvider => timeProvider;

    /// <summary>
    /// Runs one cycle of the job. Without a distributed lock, invokes <paramref name="work"/> directly.
    /// With a distributed lock: keeps a held lease across runs for <see cref="LeaseReleaseMode.KeepAcrossRuns"/>
    /// while it hasn't been lost, otherwise (re)acquires it with no wait. A non-holder returns
    /// <see cref="SingleNodeJobRunResult.SkippedNotLeaseHolder"/> without invoking <paramref name="work"/>;
    /// a lock that throws while acquiring returns <see cref="SingleNodeJobRunResult.LockUnavailable"/>,
    /// also without invoking <paramref name="work"/> (fail closed).
    /// </summary>
    /// <param name="work">
    /// The job's cycle. Receives a token linked to <paramref name="stoppingToken"/> and the lease's
    /// <see cref="ILockLease.Lost"/> token, and whether the lease was newly acquired for this run.
    /// </param>
    /// <param name="stoppingToken">The host's own stopping token. An <see cref="OperationCanceledException"/>
    /// caused by this token propagates, so the host's run loop exits.</param>
    public async Task<SingleNodeJobRunResult> RunAsync(Func<SingleNodeJobContext, Task> work, Ct stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (!@lock.IsDistributed)
        {
            var newlyAcquired = _firstRun;
            _firstRun = false;
            await work(new SingleNodeJobContext(stoppingToken, newlyAcquired)).ConfigureAwait(false);
            return SingleNodeJobRunResult.Ran;
        }

        lock (_runGate)
        {
            _running = true;
        }

        try
        {
            return await RunWithLeaseAsync(work, stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            bool release;
            lock (_runGate)
            {
                _running = false;
                release = _releaseRequested;
                _releaseRequested = false;
            }

            if (release)
            {
                await DisposeHeldLeaseAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<SingleNodeJobRunResult> RunWithLeaseAsync(Func<SingleNodeJobContext, Task> work, Ct stoppingToken)
    {
        var leaseNewlyAcquired = false;
        var lease = Volatile.Read(ref _lease);

        var keepHeld = lease is not null && mode == LeaseReleaseMode.KeepAcrossRuns && !lease.Lost.IsCancellationRequested;
        if (!keepHeld)
        {
            if (lease is not null)
            {
                await ForgetLeaseAsync(lease).ConfigureAwait(false);
            }

            var timing = ResolveTiming();
            ILockLease? acquired;
            try
            {
                acquired = await @lock.TryAcquireAsync(name, TimeSpan.Zero, timing, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.JobLockUnavailable(ex, name.Value);
                return SingleNodeJobRunResult.LockUnavailable;
            }

            if (acquired is null)
            {
                return SingleNodeJobRunResult.SkippedNotLeaseHolder;
            }

            lease = acquired;
            Volatile.Write(ref _lease, lease);
            leaseNewlyAcquired = true;
        }

        if (lease is null)
        {
            // Unreachable: the branch above always assigns a non-null lease or returns.
            throw new InvalidOperationException("Expected a held lease.");
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, lease.Lost);

        var lost = false;
        try
        {
            await work(new SingleNodeJobContext(linkedCts.Token, leaseNewlyAcquired)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lease.Lost.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
        {
            lost = true;
        }
        finally
        {
            if (lost || lease.Lost.IsCancellationRequested)
            {
                logger.JobLeaseLost(name.Value);
                await ForgetLeaseAsync(lease).ConfigureAwait(false);
                lost = true;
            }
            else if (mode == LeaseReleaseMode.AfterEachRun)
            {
                await ForgetLeaseAsync(lease).ConfigureAwait(false);
            }
        }

        return lost ? SingleNodeJobRunResult.LeaseLost : SingleNodeJobRunResult.Ran;
    }

    /// <summary>
    /// Releases a held lease, if any. A no-op when not currently holding one. While a run is in
    /// progress the lease is left held (another node must not start the same work), and is released
    /// when that run completes.
    /// </summary>
    public async Task ReleaseAsync(Ct ct)
    {
        lock (_runGate)
        {
            if (_running)
            {
                _releaseRequested = true;
                return;
            }
        }

        await DisposeHeldLeaseAsync().ConfigureAwait(false);
    }

    private async Task DisposeHeldLeaseAsync()
    {
        var lease = Interlocked.Exchange(ref _lease, null);
        if (lease is null)
        {
            return;
        }

        await lease.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The delay before the next cycle: <paramref name="baseInterval"/> plus jitter in
    /// <c>[0, min(10% of interval, 30s)]</c> when distributed, else <paramref name="baseInterval"/> unchanged.
    /// </summary>
    public TimeSpan NextDelay(TimeSpan baseInterval)
    {
        if (!@lock.IsDistributed)
        {
            return baseInterval;
        }

        var jitterCap = Min(TimeSpan.FromTicks(baseInterval.Ticks / 10), MaxJitter);
        if (jitterCap <= TimeSpan.Zero)
        {
            return baseInterval;
        }

#pragma warning disable CA5394 // Not security-sensitive: jitter on a job interval.
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * jitterCap.TotalMilliseconds);
#pragma warning restore CA5394
        return baseInterval + jitter;
    }

    private async Task ForgetLeaseAsync(ILockLease lease)
    {
        Interlocked.CompareExchange(ref _lease, null, lease);
        await lease.DisposeAsync().ConfigureAwait(false);
    }

    private LeaseTiming ResolveTiming()
    {
        var opts = options();
        var baseInterval = interval();

        var duration = opts.LeaseDuration ?? Clamp(baseInterval * 3, MinDuration, MaxDuration);
        if (duration <= TimeSpan.Zero)
        {
            duration = MinDuration;
        }

        var renewalInterval = opts.RenewalInterval ?? Divide(duration, 3);
        var cancelBeforeExpiry = opts.CancelBeforeExpiry ?? Max(Divide(duration, 10), MinCancelBeforeExpiry);

        var invalid = renewalInterval <= TimeSpan.Zero
            || cancelBeforeExpiry <= TimeSpan.Zero
            || renewalInterval >= duration
            || cancelBeforeExpiry >= duration
            || renewalInterval >= duration - cancelBeforeExpiry;

        if (invalid)
        {
            if (!_timingWarned)
            {
                _timingWarned = true;
                logger.JobLeaseTimingInvalid(name.Value, renewalInterval, cancelBeforeExpiry, duration);
            }

            cancelBeforeExpiry = Min(Max(Divide(duration, 10), MinCancelBeforeExpiry), Divide(duration, 2));
            renewalInterval = Divide(duration, 3);
            if (renewalInterval >= duration - cancelBeforeExpiry)
            {
                renewalInterval = Divide(duration - cancelBeforeExpiry, 2);
            }
            if (renewalInterval <= TimeSpan.Zero)
            {
                renewalInterval = TimeSpan.FromMilliseconds(1);
            }
        }

        return new LeaseTiming(duration, renewalInterval, cancelBeforeExpiry);
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max) =>
        value < min ? min : value > max ? max : value;

    private static TimeSpan Divide(TimeSpan value, int divisor) => TimeSpan.FromTicks(value.Ticks / divisor);

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
}
