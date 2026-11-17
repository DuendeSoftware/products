// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Hosting.Coordination;
using Duende.IdentityServer.Hosting.OutboxProcessor;
using Duende.IdentityServer.Infrastructure.DistributedLock;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.Spaces.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Duende.IdentityServer.IntegrationTests.Hosting.Coordination;

/// <summary>
/// Exercises <see cref="ServerSideSessionCleanupHost"/>'s cross-node coordination: without Duende
/// Storage, every node runs cleanup independently, exactly as before; with Duende Storage (the
/// default instance) as the distributed lock but non-storage-backed sessions (the mixed case),
/// only one node runs a cycle at a time, releasing the lease after each run
/// (<see cref="LeaseReleaseMode.AfterEachRun"/>); and with storage-backed sessions
/// (<see cref="IStorageBackedSessionsMarker"/> registered), a cycle never creates or touches a
/// distributed-lock record, because the host is a no-op in that configuration.
/// </summary>
public sealed class MultiNodeSessionCleanupTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task without_storage_both_nodes_run_cleanup_independently()
    {
        await using var cluster = MultiNodeTestCluster.Create();

        var nodeA = cluster.AddNode(services => ConfigureInMemoryNode(services));
        var nodeB = cluster.AddNode(services => ConfigureInMemoryNode(services));

        var keyA = await SeedExpiredSessionAsync(nodeA, cluster, _ct);
        var keyB = await SeedExpiredSessionAsync(nodeB, cluster, _ct);

        cluster.TimeProvider.Advance(TimeSpan.FromMinutes(2));

        var resultA = await GetCleanupHost(nodeA).RunCycleAsync(_ct);
        var resultB = await GetCleanupHost(nodeB).RunCycleAsync(_ct);

        resultA.ShouldBe(SingleNodeJobRunResult.Ran);
        resultB.ShouldBe(SingleNodeJobRunResult.Ran);

        (await SessionExistsAsync(nodeA, keyA, _ct)).ShouldBeFalse("node A must have removed its own expired session");
        (await SessionExistsAsync(nodeB, keyB, _ct)).ShouldBeFalse("node B must have removed its own expired session");
    }

    [Fact]
    public async Task mixed_case_while_one_node_s_cleanup_is_blocked_the_other_skips_then_runs_once_it_completes()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var gate = new ManualGate();

        var nodeA = cluster.AddNode(services => ConfigureStorageLockNode(services, cluster, gate.WaitAsync));
        var nodeB = cluster.AddNode(services => ConfigureStorageLockNode(services, cluster, _ => Task.CompletedTask));

        await cluster.MigrateAsync(_ct);

        var hostA = GetCleanupHost(nodeA);
        var hostB = GetCleanupHost(nodeB);

        var runA = hostA.RunCycleAsync(_ct);
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(5), _ct);

        // A is blocked inside its cleanup cycle, holding the AfterEachRun lease: B must not be
        // able to run concurrently.
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
    public async Task with_in_memory_sessions_a_node_cleans_up_even_while_another_node_holds_the_cleanup_lease()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var nodeA = cluster.AddNode(services => ConfigureStorageLockNode(services, cluster));
        var nodeB = cluster.AddNode(services => ConfigureStorageLockNode(services, cluster));
        await cluster.MigrateAsync(_ct);

        var keyB = await SeedExpiredSessionAsync(nodeB, cluster, _ct);
        cluster.TimeProvider.Advance(TimeSpan.FromMinutes(2));

        await using var heldLease = await nodeA.GetRequiredService<IDistributedLock>()
            .TryAcquireAsync(LockName.ServerSideSessionCleanup, TimeSpan.Zero, _ct);
        heldLease.ShouldNotBeNull();

        var result = await GetCleanupHost(nodeB).RunCycleAsync(_ct);

        result.ShouldBe(SingleNodeJobRunResult.Ran);
        (await SessionExistsAsync(nodeB, keyB, _ct)).ShouldBeFalse("in-memory sessions are per node, so each node must clean its own");
    }

    [Fact]
    public async Task with_a_shared_store_every_removed_session_is_notified_even_when_the_run_is_cancelled_mid_batch()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        using var cancelRun = new CancellationTokenSource();
        var recorder = new CancellingExpirationRecorder(cancelRun);
        var node = cluster.AddNode(services =>
        {
            ConfigureStorageLockNode(services, cluster, _ => Task.CompletedTask);
            services.AddSingleton<ISessionCoordinationService>(recorder);
        });
        await cluster.MigrateAsync(_ct);

        for (var i = 0; i < 3; i++)
        {
            await SeedExpiredSessionAsync(node, cluster, _ct);
        }

        cluster.TimeProvider.Advance(TimeSpan.FromMinutes(2));

        await GetCleanupHost(node).RunCycleAsync(cancelRun.Token);

        recorder.Notified.ShouldBe(3, "sessions already removed from the store must all get their expiration processed");
    }

    [Fact]
    public async Task with_storage_backed_sessions_no_lock_record_is_ever_created()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var nodeA = cluster.AddNode(services => ConfigureStorageBackedSessionsNode(services, cluster));

        await cluster.MigrateAsync(_ct);

        var hostA = GetCleanupHost(nodeA);
        var result = await hostA.RunCycleAsync(_ct);
        result.ShouldBe(SingleNodeJobRunResult.Ran);

        var accessor = nodeA.GetRequiredService<ManagementStorageAccessor>();
        var read = await ReadLockRecordAsync(accessor, LockName.ServerSideSessionCleanup, _ct);
        read.Found.ShouldBeFalse("storage-backed sessions mean the host is a no-op; a cycle must never create a lock record");
    }

    private static void ConfigureInMemoryNode(IServiceCollection services)
    {
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var builder = services.AddIdentityServer(options => options.ServerSideSessions.RemoveExpiredSessions = true);
        builder.AddServerSideSessions();
    }

    private static void ConfigureStorageLockNode(
        IServiceCollection services,
        MultiNodeTestCluster cluster,
        Func<Ct, Task>? cleanupGate = null)
    {
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var builder = services.AddIdentityServer(options =>
        {
            options.ServerSideSessions.RemoveExpiredSessions = true;
            options.OutboxProcessor.EnableProcessor = false;
            options.StoragePurge.EnablePurge = false;
        });

        // Storage default instance only (no AddOperationalStorage): this registers the
        // Storage-backed distributed lock, but server-side sessions remain in-memory, so
        // IStorageBackedSessionsMarker is never registered. This is the "mixed" configuration.
        builder.AddStorage(s => s.AddSqlite(o => o.ConnectionString = cluster.ConnectionString));
        builder.AddServerSideSessions();

        if (cleanupGate is not null)
        {
            // A plain Add registered after AddServerSideSessions' TryAddTransient wins (last
            // registration for a non-enumerable service wins), so this decorator is what every
            // cleanup cycle on this node actually calls.
            // Wrapping the store makes it a shared store as far as the host can tell, so the cleanup
            // stays single-node.
            services.AddSingleton<IServerSideSessionStore>(sp =>
                new WrappedSessionStore(new InMemoryServerSideSessionStore(sp.GetRequiredService<TimeProvider>())));
            services.AddSingleton<IServerSideTicketStore>(sp =>
                new GatingServerSideTicketStore(sp.GetRequiredService<IServerSideSessionStore>(), sp.GetRequiredService<TimeProvider>(), cleanupGate));
        }
    }

    private static void ConfigureStorageBackedSessionsNode(IServiceCollection services, MultiNodeTestCluster cluster)
    {
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var builder = services.AddIdentityServer(options =>
        {
            options.ServerSideSessions.RemoveExpiredSessions = true;
            options.OutboxProcessor.EnableProcessor = false;
            options.StoragePurge.EnablePurge = false;
        });

        builder.AddStorage(s => s.AddSqlite(o => o.ConnectionString = cluster.ConnectionString));
        builder.AddOperationalStorage();
        builder.AddServerSideSessions();
    }

    private static ServerSideSessionCleanupHost GetCleanupHost(ServiceProvider provider) =>
        provider.GetServices<IHostedService>().OfType<ServerSideSessionCleanupHost>().Single();

    private static async Task<string> SeedExpiredSessionAsync(ServiceProvider provider, MultiNodeTestCluster cluster, Ct ct)
    {
        var store = provider.GetRequiredService<IServerSideSessionStore>();
        var key = $"multi-node-session-cleanup_{Guid.NewGuid():N}";

        await store.CreateSessionAsync(
            new ServerSideSession
            {
                Key = key,
                Scheme = "idsrv",
                SubjectId = "alice",
                SessionId = "sid-1",
                Created = cluster.TimeProvider.GetUtcNow().UtcDateTime,
                Renewed = cluster.TimeProvider.GetUtcNow().UtcDateTime,
                Expires = cluster.TimeProvider.GetUtcNow().AddMinutes(1).UtcDateTime,
                Ticket = "unused"
            },
            ct);

        return key;
    }

    private static async Task<bool> SessionExistsAsync(ServiceProvider provider, string key, Ct ct) =>
        await provider.GetRequiredService<IServerSideSessionStore>().GetSessionAsync(key, ct) is not null;

    private static Task<StoreGetResult> ReadLockRecordAsync(ManagementStorageAccessor accessor, LockName name, Ct ct) =>
        accessor.GetManagementStorage().TryReadAsync(
            DistributedLockDso.EntityType,
            DataStorageKey.Create(LockNameDskV1.Create(name)),
            ct);

    private sealed class CancellingExpirationRecorder(CancellationTokenSource cancelRun) : ISessionCoordinationService
    {
        public int Notified { get; private set; }

        public Task ProcessExpirationAsync(UserSession session, Ct ct)
        {
            ct.ThrowIfCancellationRequested();
            Notified++;
            cancelRun.Cancel();
            return Task.CompletedTask;
        }

        public Task ProcessLogoutAsync(UserSession session, Ct ct) => Task.CompletedTask;

        public Task<bool> ValidateSessionAsync(SessionValidationRequest request, Ct ct) => Task.FromResult(true);
    }

    private sealed class WrappedSessionStore(IServerSideSessionStore inner) : IServerSideSessionStore
    {
        public Task<ServerSideSession?> GetSessionAsync(string key, Ct ct) => inner.GetSessionAsync(key, ct);

        public Task CreateSessionAsync(ServerSideSession session, Ct ct) => inner.CreateSessionAsync(session, ct);

        public Task<bool> UpdateSessionAsync(ServerSideSession session, Ct ct) => inner.UpdateSessionAsync(session, ct);

        public Task DeleteSessionAsync(string key, Ct ct) => inner.DeleteSessionAsync(key, ct);

        public Task<IReadOnlyCollection<ServerSideSession>> GetSessionsAsync(SessionFilter filter, Ct ct) => inner.GetSessionsAsync(filter, ct);

        public Task DeleteSessionsAsync(SessionFilter filter, Ct ct) => inner.DeleteSessionsAsync(filter, ct);

        public Task<IReadOnlyCollection<ServerSideSession>> GetAndRemoveExpiredSessionsAsync(int count, Ct ct) => inner.GetAndRemoveExpiredSessionsAsync(count, ct);

        public Task<QueryResult<ServerSideSession>> QuerySessionsAsync(Ct ct, SessionQuery? filter = null) => inner.QuerySessionsAsync(ct, filter);
    }

    /// <summary>
    /// Lets a test pause a cleanup cycle mid-flight: the first (and only) call to
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
    /// Decorates <see cref="IServerSideTicketStore"/> so this node's cleanup cycle blocks on
    /// <paramref name="gate"/> just before <see cref="GetAndRemoveExpiredSessionsAsync"/>, mirroring
    /// <c>GatingCrossPartitionStorage</c> in <c>MultiNodePurgeTests</c>.
    /// </summary>
    private sealed class GatingServerSideTicketStore(IServerSideSessionStore sessionStore, TimeProvider timeProvider, Func<Ct, Task> gate) : IServerSideTicketStore
    {
        public async Task<string> StoreAsync(AuthenticationTicket ticket)
        {
            var key = Guid.NewGuid().ToString("N");
            await sessionStore.CreateSessionAsync(
                new ServerSideSession
                {
                    Key = key,
                    Scheme = ticket.AuthenticationScheme,
                    SubjectId = "unused",
                    SessionId = "unused",
                    Created = timeProvider.GetUtcNow().UtcDateTime,
                    Renewed = timeProvider.GetUtcNow().UtcDateTime,
                    Expires = ticket.Properties.ExpiresUtc?.UtcDateTime,
                    Ticket = "unused"
                },
                Ct.None);
            return key;
        }

        public Task RenewAsync(string key, AuthenticationTicket ticket) => Task.CompletedTask;

        public Task<AuthenticationTicket?> RetrieveAsync(string key) => Task.FromResult<AuthenticationTicket?>(null);

        public Task RemoveAsync(string key) => sessionStore.DeleteSessionAsync(key, Ct.None);

        public Task<IReadOnlyCollection<UserSession>> GetSessionsAsync(SessionFilter filter, Ct ct) =>
            Task.FromResult<IReadOnlyCollection<UserSession>>([]);

        public Task<QueryResult<UserSession>> QuerySessionsAsync(SessionQuery filter, Ct ct) =>
            Task.FromResult(new QueryResult<UserSession>());

        public async Task<IReadOnlyCollection<UserSession>> GetAndRemoveExpiredSessionsAsync(int count, Ct ct)
        {
            await gate(ct);
            var sessions = await sessionStore.GetAndRemoveExpiredSessionsAsync(count, ct);
            return sessions.Select(s => new UserSession
            {
                SubjectId = s.SubjectId,
                SessionId = s.SessionId,
                DisplayName = s.DisplayName,
                Created = s.Created,
                Renewed = s.Renewed,
                Expires = s.Expires
            }).ToArray();
        }
    }
}
