// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Hosting.Coordination;
using Duende.IdentityServer.Infrastructure.DistributedLock;
using Duende.IdentityServer.Stores.Storage;
using Duende.Spaces.Internal.Storage;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Duende.IdentityServer.IntegrationTests.Hosting.Coordination;

/// <summary>
/// Exercises <see cref="StoragePurgeHost"/>'s cross-node coordination: while one node's purge
/// cycle is blocked, the other node skips rather than purging concurrently, and acquires the
/// lease at once (no waiting for lease expiry) once the blocked cycle completes, because
/// <see cref="LeaseReleaseMode.AfterEachRun"/> releases the lease after every run. Also proves a
/// real purge cycle leaves distributed-lock records untouched.
/// </summary>
public sealed class MultiNodePurgeTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task while_one_node_s_purge_is_blocked_the_other_skips_then_runs_once_it_completes()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var gate = new ManualGate();

        var nodeA = cluster.AddNode(services => ConfigureNode(services, cluster, gate.WaitAsync));
        var nodeB = cluster.AddNode(services => ConfigureNode(services, cluster));

        await cluster.MigrateAsync(_ct);

        await SeedExpiredEntityAsync(nodeA, TimeSpan.FromMinutes(1), _ct);
        cluster.TimeProvider.Advance(TimeSpan.FromMinutes(2));

        var hostA = GetPurgeHost(nodeA);
        var hostB = GetPurgeHost(nodeB);

        var runA = hostA.RunCycleAsync(_ct);
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(5), _ct);

        // A is blocked inside its purge cycle, holding the AfterEachRun lease: B must not be able
        // to run concurrently.
        var resultB = await hostB.RunCycleAsync(_ct);
        resultB.ShouldBe(SingleNodeJobRunResult.SkippedNotLeaseHolder);

        gate.Release();
        var resultA = await runA;
        resultA.ShouldBe(SingleNodeJobRunResult.Ran);

        // AfterEachRun releases the lease once A's run completes, so B acquires it at once,
        // without advancing time (no waiting for lease expiry).
        var resultB2 = await hostB.RunCycleAsync(_ct);
        resultB2.ShouldBe(SingleNodeJobRunResult.Ran);
    }

    [Fact]
    public async Task a_real_purge_cycle_does_not_remove_distributed_lock_records()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var nodeA = cluster.AddNode(services => ConfigureNode(services, cluster));

        await cluster.MigrateAsync(_ct);

        await SeedExpiredEntityAsync(nodeA, TimeSpan.FromMinutes(1), _ct);
        cluster.TimeProvider.Advance(TimeSpan.FromMinutes(2));

        // Seed a distributed-lock record: acquiring and releasing a lease for a name other than
        // the purge job's own leaves a (released, not deleted) DistributedLockDso row behind.
        var sessionCleanupLockName = LockName.ServerSideSessionCleanup;
        var @lock = nodeA.GetRequiredService<IDistributedLock>();
        await using (var lease = await @lock.TryAcquireAsync(sessionCleanupLockName, TimeSpan.Zero, _ct))
        {
            lease.ShouldNotBeNull();
        }

        var accessor = nodeA.GetRequiredService<ManagementStorageAccessor>();
        var before = await ReadLockRecordAsync(accessor, sessionCleanupLockName, _ct);
        before.Found.ShouldBeTrue("the lock record must exist before the purge runs");

        var hostA = GetPurgeHost(nodeA);
        var result = await hostA.RunCycleAsync(_ct);
        result.ShouldBe(SingleNodeJobRunResult.Ran);

        var after = await ReadLockRecordAsync(accessor, sessionCleanupLockName, _ct);
        after.Found.ShouldBeTrue("a real purge cycle must not remove distributed-lock records");
    }

    private static void ConfigureNode(
        IServiceCollection services,
        MultiNodeTestCluster cluster,
        Func<Ct, Task>? purgeGate = null)
    {
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var builder = services.AddIdentityServer(options =>
        {
            options.StoragePurge.EnablePurge = true;
            options.OutboxProcessor.EnableProcessor = false;
        });

        builder.AddStorage(s => s.AddSqlite(o => o.ConnectionString = cluster.ConnectionString));
        services.AddDsoRegistration<IdentityProviderDso.V1>();

        if (purgeGate is not null)
        {
            // A plain Add registered after AddStorage's TryAddTransient wins (last registration
            // for a non-enumerable service wins), so this decorator is what every purge cycle on
            // this node actually calls, without needing an internal storage seam: it only uses
            // the same public GetRequiredKeyedService<ICrossPartitionStorage> lookup Duende.Storage's
            // own internal CrossPartitionStorageFactory uses.
            services.AddSingleton<ICrossPartitionStorageFactory>(sp =>
                new GatingCrossPartitionStorageFactory(sp, purgeGate));
        }
    }

    private static StoragePurgeHost GetPurgeHost(ServiceProvider provider) =>
        provider.GetServices<IHostedService>().OfType<StoragePurgeHost>().Single();

    private static async Task SeedExpiredEntityAsync(ServiceProvider provider, TimeSpan lifetime, Ct ct)
    {
        var storageFactory = provider.GetRequiredService<IPartitionedStorageFactory>();
        var category = DataCategoryName.Create("multi-node-purge-test");
        var storage = await storageFactory.GetPartitionedStorageAsync(category, ct);

        var dso = new IdentityProviderDso.V1
        {
            Id = Guid.NewGuid(),
            Scheme = $"idp_{Guid.NewGuid():N}",
            Enabled = true,
            Type = "oidc"
        };

        var result = await storage.CreateAsync(
            UuidV7.New(),
            dso,
            [],
            [],
            Expiration.InRelative(lifetime),
            [],
            ct);

        result.ShouldBe(CreateResult.Success);
    }

    private static Task<StoreGetResult> ReadLockRecordAsync(ManagementStorageAccessor accessor, LockName name, Ct ct) =>
        accessor.GetManagementStorage().TryReadAsync(
            DistributedLockDso.EntityType,
            DataStorageKey.Create(LockNameDskV1.Create(name)),
            ct);

    /// <summary>
    /// Lets a test pause a purge cycle mid-flight: the first (and only) call to
    /// <see cref="WaitAsync"/> signals <see cref="Entered"/> and then blocks until the test calls
    /// <see cref="Release"/>.
    /// </summary>
    private sealed class ManualGate
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public async Task WaitAsync(Ct ct)
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(ct);
        }

        public void Release() => _release.TrySetResult();
    }

    /// <summary>
    /// Decorates <see cref="ICrossPartitionStorageFactory"/> so this node's purge cycle blocks on
    /// <paramref name="gate"/> just before each batch's <see cref="ICrossPartitionStorage.PurgeExpiredAsync"/>
    /// call, the same seam <c>StoragePurgeHostTests</c> already uses to fake the factory.
    /// </summary>
    private sealed class GatingCrossPartitionStorageFactory(IServiceProvider serviceProvider, Func<Ct, Task> gate) : ICrossPartitionStorageFactory
    {
        public Task<ICrossPartitionStorage> GetCrossPartitionStorageAsync(StorageInstanceId storageInstanceId, Ct ct)
        {
            var inner = serviceProvider.GetRequiredKeyedService<ICrossPartitionStorage>(storageInstanceId);
            return Task.FromResult<ICrossPartitionStorage>(new GatingCrossPartitionStorage(inner, gate));
        }
    }

    private sealed class GatingCrossPartitionStorage(ICrossPartitionStorage inner, Func<Ct, Task> gate) : ICrossPartitionStorage
    {
        public Task<OutboxEventsPage> GetOutboxEventsForSubscriptionAsync(SubscriberName subscriptionName, int count, Ct ct) =>
            inner.GetOutboxEventsForSubscriptionAsync(subscriptionName, count, ct);

        public Task DeleteOutboxEventsAsync(IReadOnlyList<OutboxEventId> ids, Ct ct) =>
            inner.DeleteOutboxEventsAsync(ids, ct);

        public async Task<int> PurgeExpiredAsync(int batchSize, Ct ct)
        {
            await gate(ct);
            return await inner.PurgeExpiredAsync(batchSize, ct);
        }

        public Task<PurgeResult> PurgePoolAsync(PoolId poolId, Ct ct) => inner.PurgePoolAsync(poolId, ct);

        public Task<PurgeResult> PurgePoolAsync(PoolId poolId, int batchSize, Ct ct) => inner.PurgePoolAsync(poolId, batchSize, ct);
    }
}
