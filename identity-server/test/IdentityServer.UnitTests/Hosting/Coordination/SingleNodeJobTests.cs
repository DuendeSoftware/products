// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.Coordination;
using Duende.IdentityServer.Infrastructure.DistributedLock;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Hosting.Coordination;

/// <summary>
/// Covers <see cref="SingleNodeJob"/> against a fake <see cref="IDistributedLock"/>, so lease
/// acquisition, loss, release modes and timing resolution can be driven deterministically.
/// </summary>
public class SingleNodeJobTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private static readonly LockName Name = LockName.OutboxProcessor;

    private SingleNodeJob CreateJob(
        IDistributedLock @lock,
        LeaseReleaseMode mode = LeaseReleaseMode.AfterEachRun,
        TimeSpan? interval = null,
        BackgroundJobLeaseOptions? options = null) =>
        new(
            Name,
            mode,
            () => interval ?? TimeSpan.FromSeconds(30),
            () => options ?? new BackgroundJobLeaseOptions(),
            @lock,
            _time,
            NullLogger.Instance);

    [Fact]
    public async Task Non_holder_skips_without_invoking_work()
    {
        var @lock = new FakeDistributedLock { AcquireResult = _ => Task.FromResult<ILockLease?>(null) };
        var job = CreateJob(@lock);
        var invoked = false;

        var result = await job.RunAsync(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        }, _ct);

        result.ShouldBe(SingleNodeJobRunResult.SkippedNotLeaseHolder);
        invoked.ShouldBeFalse();
    }

    [Fact]
    public async Task Lock_exception_yields_LockUnavailable_and_does_not_invoke_work()
    {
        var @lock = new FakeDistributedLock { AcquireResult = _ => Task.FromException<ILockLease?>(new InvalidOperationException("boom")) };
        var job = CreateJob(@lock);
        var invoked = false;

        var result = await job.RunAsync(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        }, _ct);

        result.ShouldBe(SingleNodeJobRunResult.LockUnavailable);
        invoked.ShouldBeFalse();
    }

    [Fact]
    public async Task In_process_lock_runs_work_every_time_and_never_calls_TryAcquireAsync()
    {
        var @lock = new InProcessDistributedLock();
        var job = CreateJob(@lock);
        var runs = 0;

        for (var i = 0; i < 3; i++)
        {
            var context = default(SingleNodeJobContext);
            var result = await job.RunAsync(ctx =>
            {
                context = ctx;
                runs++;
                return Task.CompletedTask;
            }, _ct);

            result.ShouldBe(SingleNodeJobRunResult.Ran);
            context.LeaseNewlyAcquired.ShouldBe(i == 0, "only the first run is newly acquired without a distributed lock");
        }

        runs.ShouldBe(3);
    }

    [Fact]
    public async Task AfterEachRun_disposes_lease_after_successful_work()
    {
        var lease = new FakeLease();
        var @lock = new FakeDistributedLock { AcquireResult = _ => Task.FromResult<ILockLease?>(lease) };
        var job = CreateJob(@lock, LeaseReleaseMode.AfterEachRun);

        var result = await job.RunAsync(_ => Task.CompletedTask, _ct);

        result.ShouldBe(SingleNodeJobRunResult.Ran);
        lease.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task AfterEachRun_disposes_lease_when_work_throws()
    {
        var lease = new FakeLease();
        var @lock = new FakeDistributedLock { AcquireResult = _ => Task.FromResult<ILockLease?>(lease) };
        var job = CreateJob(@lock, LeaseReleaseMode.AfterEachRun);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            job.RunAsync(_ => throw new InvalidOperationException("boom"), _ct));

        lease.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task AfterEachRun_lets_the_next_run_reacquire()
    {
        var leaseA = new FakeLease();
        var leaseB = new FakeLease();
        var acquireCount = 0;
        var @lock = new FakeDistributedLock
        {
            AcquireResult = _ =>
            {
                acquireCount++;
                return Task.FromResult<ILockLease?>(acquireCount == 1 ? leaseA : leaseB);
            },
        };
        var job = CreateJob(@lock, LeaseReleaseMode.AfterEachRun);

        await job.RunAsync(_ => Task.CompletedTask, _ct);
        await job.RunAsync(_ => Task.CompletedTask, _ct);

        acquireCount.ShouldBe(2, "AfterEachRun must acquire again on every run");
        leaseA.Disposed.ShouldBeTrue();
        leaseB.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task KeepAcrossRuns_second_run_is_not_newly_acquired_and_does_not_reacquire()
    {
        var lease = new FakeLease();
        var acquireCount = 0;
        var @lock = new FakeDistributedLock
        {
            AcquireResult = _ =>
            {
                acquireCount++;
                return Task.FromResult<ILockLease?>(lease);
            },
        };
        var job = CreateJob(@lock, LeaseReleaseMode.KeepAcrossRuns);

        var firstContext = default(SingleNodeJobContext);
        await job.RunAsync(ctx =>
        {
            firstContext = ctx;
            return Task.CompletedTask;
        }, _ct);

        var secondContext = default(SingleNodeJobContext);
        await job.RunAsync(ctx =>
        {
            secondContext = ctx;
            return Task.CompletedTask;
        }, _ct);

        firstContext.LeaseNewlyAcquired.ShouldBeTrue();
        secondContext.LeaseNewlyAcquired.ShouldBeFalse();
        acquireCount.ShouldBe(1, "KeepAcrossRuns must not reacquire while the lease is still held");
        lease.Disposed.ShouldBeFalse("the lease must stay held between runs");
    }

    [Fact]
    public async Task KeepAcrossRuns_after_loss_and_reacquire_reports_newly_acquired_again()
    {
        var leaseA = new FakeLease();
        var leaseB = new FakeLease();
        var acquireCount = 0;
        var @lock = new FakeDistributedLock
        {
            AcquireResult = _ =>
            {
                acquireCount++;
                return Task.FromResult<ILockLease?>(acquireCount == 1 ? leaseA : leaseB);
            },
        };
        var job = CreateJob(@lock, LeaseReleaseMode.KeepAcrossRuns);

        await job.RunAsync(_ => Task.CompletedTask, _ct);
        leaseA.TriggerLost();

        var secondContext = default(SingleNodeJobContext);
        var result = await job.RunAsync(ctx =>
        {
            secondContext = ctx;
            return Task.CompletedTask;
        }, _ct);

        result.ShouldBe(SingleNodeJobRunResult.Ran);
        secondContext.LeaseNewlyAcquired.ShouldBeTrue();
        acquireCount.ShouldBe(2);
    }

    [Fact]
    public async Task Lost_during_work_cancels_the_work_token_and_returns_LeaseLost()
    {
        var lease = new FakeLease();
        var @lock = new FakeDistributedLock { AcquireResult = _ => Task.FromResult<ILockLease?>(lease) };
        var job = CreateJob(@lock, LeaseReleaseMode.KeepAcrossRuns);

        var result = await job.RunAsync(async ctx =>
        {
            lease.TriggerLost();
            await Task.Delay(Timeout.InfiniteTimeSpan, ctx.CancellationToken);
        }, _ct);

        result.ShouldBe(SingleNodeJobRunResult.LeaseLost);
        lease.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task StoppingToken_cancellation_propagates_and_exits()
    {
        var lease = new FakeLease();
        var @lock = new FakeDistributedLock { AcquireResult = _ => Task.FromResult<ILockLease?>(lease) };
        var job = CreateJob(@lock, LeaseReleaseMode.KeepAcrossRuns);

        using var cts = new CancellationTokenSource();

        var runTask = job.RunAsync(async ctx =>
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.InfiniteTimeSpan, ctx.CancellationToken);
        }, cts.Token);

        await Should.ThrowAsync<OperationCanceledException>(() => runTask);
    }

    [Fact]
    public async Task ReleaseAsync_releases_a_held_lease_and_is_a_no_op_otherwise()
    {
        var lease = new FakeLease();
        var @lock = new FakeDistributedLock { AcquireResult = _ => Task.FromResult<ILockLease?>(lease) };
        var job = CreateJob(@lock, LeaseReleaseMode.KeepAcrossRuns);

        await job.RunAsync(_ => Task.CompletedTask, _ct);
        lease.Disposed.ShouldBeFalse();

        await job.ReleaseAsync(_ct);
        lease.Disposed.ShouldBeTrue();

        await job.ReleaseAsync(_ct); // no-op, must not throw or dispose again incorrectly
    }

    [Fact]
    public void NextDelay_without_distributed_lock_returns_interval_unchanged()
    {
        var job = CreateJob(new InProcessDistributedLock());
        var interval = TimeSpan.FromSeconds(30);

        job.NextDelay(interval).ShouldBe(interval);
    }

    [Fact]
    public void NextDelay_with_distributed_lock_adds_bounded_jitter()
    {
        var @lock = new FakeDistributedLock { IsDistributed = true };
        var job = CreateJob(@lock);
        var interval = TimeSpan.FromSeconds(100);
        var maxJitter = TimeSpan.FromSeconds(10); // 10% of interval, below the 30s cap

        for (var i = 0; i < 50; i++)
        {
            var delay = job.NextDelay(interval);
            delay.ShouldBeGreaterThanOrEqualTo(interval);
            delay.ShouldBeLessThanOrEqualTo(interval + maxJitter);
        }
    }

    [Fact]
    public void NextDelay_jitter_is_capped_at_30_seconds_for_a_long_interval()
    {
        var @lock = new FakeDistributedLock { IsDistributed = true };
        var job = CreateJob(@lock);
        var interval = TimeSpan.FromHours(1); // 10% would be 6 minutes; capped at 30s
        var maxJitter = TimeSpan.FromSeconds(30);

        for (var i = 0; i < 50; i++)
        {
            var delay = job.NextDelay(interval);
            delay.ShouldBeLessThanOrEqualTo(interval + maxJitter);
        }
    }

    [Fact]
    public async Task Timing_defaults_are_derived_from_the_interval_when_not_configured()
    {
        var @lock = new FakeDistributedLock();
        var job = CreateJob(@lock, interval: TimeSpan.FromSeconds(20));

        await job.RunAsync(_ => Task.CompletedTask, _ct);

        var timing = @lock.Timings.ShouldHaveSingleItem();
        timing.Duration.ShouldBe(TimeSpan.FromSeconds(60)); // 3 x interval, within [30s, 5min]
        timing.RenewalInterval.ShouldBe(TimeSpan.FromSeconds(20)); // Duration / 3
        timing.CancelBeforeExpiry.ShouldBe(TimeSpan.FromSeconds(6)); // max(Duration/10, 5s)
    }

    [Fact]
    public async Task Timing_duration_is_clamped_to_the_30_second_minimum()
    {
        var @lock = new FakeDistributedLock();
        var job = CreateJob(@lock, interval: TimeSpan.FromSeconds(1));

        await job.RunAsync(_ => Task.CompletedTask, _ct);

        @lock.Timings.ShouldHaveSingleItem().Duration.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Timing_duration_is_clamped_to_the_5_minute_maximum()
    {
        var @lock = new FakeDistributedLock();
        var job = CreateJob(@lock, interval: TimeSpan.FromHours(1));

        await job.RunAsync(_ => Task.CompletedTask, _ct);

        @lock.Timings.ShouldHaveSingleItem().Duration.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Invalid_configured_timing_is_clamped_and_logged_once()
    {
        var @lock = new FakeDistributedLock();
        var options = new BackgroundJobLeaseOptions
        {
            LeaseDuration = TimeSpan.FromSeconds(10),
            RenewalInterval = TimeSpan.FromSeconds(10), // invalid: equals duration
            CancelBeforeExpiry = TimeSpan.FromSeconds(10), // invalid: equals duration
        };
        var job = CreateJob(@lock, options: options);

        await job.RunAsync(_ => Task.CompletedTask, _ct);
        await job.RunAsync(_ => Task.CompletedTask, _ct);

        @lock.Timings.Count.ShouldBe(2);
        foreach (var timing in @lock.Timings)
        {
            timing.RenewalInterval.ShouldBeLessThan(timing.Duration);
            timing.CancelBeforeExpiry.ShouldBeLessThan(timing.Duration);
        }
    }

    [Fact]
    public async Task ReleaseAsync_during_a_run_keeps_the_lease_until_the_run_completes()
    {
        var lease = new FakeLease();
        var job = CreateJob(new FakeDistributedLock { AcquireResult = _ => Task.FromResult<ILockLease?>(lease) }, LeaseReleaseMode.KeepAcrossRuns);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = job.RunAsync(async _ =>
        {
            started.SetResult();
            await finish.Task;
        }, _ct);
        await started.Task;

        await job.ReleaseAsync(_ct);
        lease.Disposed.ShouldBeFalse();

        finish.SetResult();
        (await run).ShouldBe(SingleNodeJobRunResult.Ran);
        lease.Disposed.ShouldBeTrue();
    }

    private sealed class FakeDistributedLock : IDistributedLock
    {
        public bool IsDistributed { get; init; } = true;

        public Func<LockName, Task<ILockLease?>>? AcquireResult { get; init; }

        public List<LeaseTiming> Timings { get; } = [];

        public Task<ILockLease?> TryAcquireAsync(LockName name, TimeSpan waitTimeout, LeaseTiming timing, Ct ct)
        {
            Timings.Add(timing);
            return AcquireResult?.Invoke(name) ?? Task.FromResult<ILockLease?>(new FakeLease());
        }
    }

    private sealed class FakeLease : ILockLease
    {
        private readonly CancellationTokenSource _lost = new();

        public bool Disposed { get; private set; }

        public Ct Lost => _lost.Token;

        public void TriggerLost() => _lost.Cancel();

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
