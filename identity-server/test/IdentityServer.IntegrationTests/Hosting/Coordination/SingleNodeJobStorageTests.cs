// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.Coordination;
using Duende.IdentityServer.Infrastructure.DistributedLock;
using Duende.IdentityServer.Stores.Storage;
using Duende.Spaces.Internal.Storage;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.Hosting.Coordination;

/// <summary>
/// Exercises <see cref="SingleNodeJob"/> against a real <see cref="StorageDistributedLock"/> backed
/// by SQLite, across two nodes sharing one database and a single <see cref="FakeTimeProvider"/>
/// (<see cref="MultiNodeTestCluster"/>).
/// </summary>
public sealed class SingleNodeJobStorageTests
{
    private static readonly LockName Name = LockName.OutboxProcessor;

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static void ConfigureNode(IServiceCollection services, MultiNodeTestCluster cluster)
    {
        services.AddStorageInternal(storage => storage.AddSqlite(o => o.ConnectionString = cluster.ConnectionString));
        services.AddDsoRegistration<DistributedLockDso.V1>();
        services.TryAddTransient<DefaultPartitionedStorageFactory>();
        services.TryAddTransient<ManagementStorageAccessor>();
    }

    private static async Task<StorageDistributedLock> BuildLockAsync(
        ServiceProvider node, FakeTimeProvider time, IDistributedLockStorage? storageOverride = null)
    {
        var accessor = node.GetRequiredService<ManagementStorageAccessor>();
        var storage = storageOverride ?? new DefaultDistributedLockStorage(accessor);
        return new StorageDistributedLock(storage, time, NullLogger<StorageDistributedLock>.Instance);
    }

    private static SingleNodeJob BuildJob(
        StorageDistributedLock @lock,
        FakeTimeProvider time,
        LeaseReleaseMode mode = LeaseReleaseMode.AfterEachRun,
        TimeSpan? leaseDuration = null) =>
        new(
            Name,
            mode,
            () => TimeSpan.FromSeconds(10),
            () => new BackgroundJobLeaseOptions { LeaseDuration = leaseDuration },
            @lock,
            time,
            NullLogger.Instance);

    [Fact]
    public async Task Acquire_race_across_two_nodes_yields_exactly_one_Ran()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var nodeA = cluster.AddNode(services => ConfigureNode(services, cluster));
        var nodeB = cluster.AddNode(services => ConfigureNode(services, cluster));
        await cluster.MigrateAsync(_ct);

        await using var lockA = await BuildLockAsync(nodeA, cluster.TimeProvider);
        await using var lockB = await BuildLockAsync(nodeB, cluster.TimeProvider);

        var jobA = BuildJob(lockA, cluster.TimeProvider);
        var jobB = BuildJob(lockB, cluster.TimeProvider);

        // With LeaseReleaseMode.AfterEachRun, a winner that finishes (and releases) its work before the
        // loser's RunAsync even attempts to acquire lets the loser legitimately win a second, separate
        // acquisition, also yielding Ran (two non-overlapping runs, not two simultaneous holders). Whichever
        // job wins must keep its lease held, inside its work, until the sibling's RunAsync has actually
        // settled -- bounded by a real-time window so a genuine double-acquire bug still fails the test
        // instead of hanging.
        Task<SingleNodeJobRunResult>? runA = null;
        Task<SingleNodeJobRunResult>? runB = null;
        var window = TimeSpan.FromSeconds(5);

        async Task WaitForSiblingAsync(Func<Task<SingleNodeJobRunResult>?> getSibling, Ct ct)
        {
            var deadline = DateTime.UtcNow + window;
            Task<SingleNodeJobRunResult>? sibling;
            while ((sibling = getSibling()) is null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5), ct);
            }

            if (sibling is not null)
            {
                await Task.WhenAny(sibling, Task.Delay(window, ct));
            }
        }

        runA = jobA.RunAsync(_ => WaitForSiblingAsync(() => runB, _ct), _ct);
        runB = jobB.RunAsync(_ => WaitForSiblingAsync(() => runA, _ct), _ct);

        var results = await Task.WhenAll(runA, runB);

        results.Count(r => r == SingleNodeJobRunResult.Ran).ShouldBe(1);
        results.Count(r => r == SingleNodeJobRunResult.SkippedNotLeaseHolder).ShouldBe(1);
    }

    [Fact]
    public async Task Takeover_after_expiry_cancels_As_work_token_and_lets_B_acquire()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var nodeA = cluster.AddNode(services => ConfigureNode(services, cluster));
        var nodeB = cluster.AddNode(services => ConfigureNode(services, cluster));
        await cluster.MigrateAsync(_ct);

        // A's storage fails every renewal (UpdateAsync) after the initial acquire (CreateAsync), so
        // its lease can never be renewed and must be cancelled once CancelBeforeExpiry is reached.
        var accessorA = nodeA.GetRequiredService<ManagementStorageAccessor>();
        var failingStorage = new RenewalFailingDistributedLockStorage(new DefaultDistributedLockStorage(accessorA));
        await using var lockA = await BuildLockAsync(nodeA, cluster.TimeProvider, failingStorage);
        await using var lockB = await BuildLockAsync(nodeB, cluster.TimeProvider);

        var duration = TimeSpan.FromSeconds(6);
        var jobA = BuildJob(lockA, cluster.TimeProvider, LeaseReleaseMode.KeepAcrossRuns, duration);
        var jobB = BuildJob(lockB, cluster.TimeProvider, LeaseReleaseMode.KeepAcrossRuns, duration);

        var workCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runA = jobA.RunAsync(async ctx =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ctx.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                workCancelled.TrySetResult();
                throw;
            }
        }, _ct);

        await PumpTimeUntilAsync(cluster.TimeProvider, () => workCancelled.Task.IsCompleted, TimeSpan.FromSeconds(15), _ct);

        var resultA = await runA;
        resultA.ShouldBe(SingleNodeJobRunResult.LeaseLost);

        // A's lease is cancelled CancelBeforeExpiry before it actually expires in storage; advance
        // past the full Duration so B's single-attempt (waitTimeout == 0) acquire sees it as expired.
        cluster.TimeProvider.Advance(duration);
        await Task.Delay(TimeSpan.FromMilliseconds(50), _ct);

        var resultB = await jobB.RunAsync(_ => Task.CompletedTask, _ct);
        resultB.ShouldBe(SingleNodeJobRunResult.Ran);
    }

    [Fact]
    public async Task AfterEachRun_lets_B_acquire_right_after_As_run()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var nodeA = cluster.AddNode(services => ConfigureNode(services, cluster));
        var nodeB = cluster.AddNode(services => ConfigureNode(services, cluster));
        await cluster.MigrateAsync(_ct);

        await using var lockA = await BuildLockAsync(nodeA, cluster.TimeProvider);
        await using var lockB = await BuildLockAsync(nodeB, cluster.TimeProvider);

        var jobA = BuildJob(lockA, cluster.TimeProvider, LeaseReleaseMode.AfterEachRun);
        var jobB = BuildJob(lockB, cluster.TimeProvider, LeaseReleaseMode.AfterEachRun);

        var resultA = await jobA.RunAsync(_ => Task.CompletedTask, _ct);
        resultA.ShouldBe(SingleNodeJobRunResult.Ran);

        var resultB = await jobB.RunAsync(_ => Task.CompletedTask, _ct);
        resultB.ShouldBe(SingleNodeJobRunResult.Ran, "AfterEachRun must release immediately, letting another node acquire without waiting for expiry");
    }

    [Fact]
    public async Task ReleaseAsync_while_holding_lets_B_acquire_at_once()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var nodeA = cluster.AddNode(services => ConfigureNode(services, cluster));
        var nodeB = cluster.AddNode(services => ConfigureNode(services, cluster));
        await cluster.MigrateAsync(_ct);

        await using var lockA = await BuildLockAsync(nodeA, cluster.TimeProvider);
        await using var lockB = await BuildLockAsync(nodeB, cluster.TimeProvider);

        var jobA = BuildJob(lockA, cluster.TimeProvider, LeaseReleaseMode.KeepAcrossRuns);
        var jobB = BuildJob(lockB, cluster.TimeProvider, LeaseReleaseMode.KeepAcrossRuns);

        var resultA = await jobA.RunAsync(_ => Task.CompletedTask, _ct);
        resultA.ShouldBe(SingleNodeJobRunResult.Ran);

        var skippedB = await jobB.RunAsync(_ => Task.CompletedTask, _ct);
        skippedB.ShouldBe(SingleNodeJobRunResult.SkippedNotLeaseHolder, "A still holds the lease across runs");

        await jobA.ReleaseAsync(_ct);

        var resultB = await jobB.RunAsync(_ => Task.CompletedTask, _ct);
        resultB.ShouldBe(SingleNodeJobRunResult.Ran);
    }

    [Fact]
    public async Task Lease_timing_whose_renewal_falls_after_the_cutoff_falls_back_and_the_lease_is_not_lost()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var node = cluster.AddNode(services => ConfigureNode(services, cluster));
        await cluster.MigrateAsync(_ct);

        await using var @lock = await BuildLockAsync(node, cluster.TimeProvider);
        var job = new SingleNodeJob(
            Name,
            LeaseReleaseMode.AfterEachRun,
            () => TimeSpan.FromSeconds(10),
            () => new BackgroundJobLeaseOptions
            {
                LeaseDuration = TimeSpan.FromSeconds(30),
                RenewalInterval = TimeSpan.FromSeconds(20),
                CancelBeforeExpiry = TimeSpan.FromSeconds(15),
            },
            @lock,
            cluster.TimeProvider,
            NullLogger.Instance);

        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = job.RunAsync(_ => finish.Task, _ct);

        for (var i = 0; i < 25; i++)
        {
            cluster.TimeProvider.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(TimeSpan.FromMilliseconds(10), _ct);
        }

        finish.SetResult();
        (await run).ShouldBe(SingleNodeJobRunResult.Ran);
    }

    /// <summary>
    /// Advances the shared <see cref="FakeTimeProvider"/> in small real-time steps while waiting for
    /// a condition, so timer-driven background loops (renewal, backoff) get a chance to run between
    /// advances, matching the pump pattern used by multi-node tests with a shared fake clock.
    /// </summary>
    private static async Task PumpTimeUntilAsync(FakeTimeProvider time, Func<bool> condition, TimeSpan timeout, Ct ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            time.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(TimeSpan.FromMilliseconds(10), ct);
        }

        condition().ShouldBeTrue($"condition was not reached within {timeout}");
    }

    /// <summary>Fails every renewal (<see cref="UpdateAsync"/>) while letting the initial acquire (<see cref="CreateAsync"/>) through.</summary>
    private sealed class RenewalFailingDistributedLockStorage(IDistributedLockStorage inner) : IDistributedLockStorage
    {
        public Task<StoreGetResult> TryReadAsync(DataStorageKey key, Ct ct) => inner.TryReadAsync(key, ct);

        public Task<CreateResult> CreateAsync(UuidV7 id, DistributedLockDso.V1 dso, DataStorageKey key, Ct ct) =>
            inner.CreateAsync(id, dso, key, ct);

        public Task<UpdateResult> UpdateAsync(UuidV7 id, DistributedLockDso.V1 dso, int expectedVersion, DataStorageKey key, Ct ct) =>
            throw new InvalidOperationException("Simulated renewal failure.");
    }
}
