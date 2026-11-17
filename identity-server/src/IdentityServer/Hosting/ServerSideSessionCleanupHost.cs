// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.Coordination;
using Duende.IdentityServer.Hosting.OutboxProcessor;
using Duende.IdentityServer.Infrastructure.DistributedLock;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.Spaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Helper to clean up expired server side sessions.
/// </summary>
/// <remarks>
/// With the in-memory session store and Spaces enabled, every space is cleaned. This only works when the
/// registered <see cref="IServerSideSessionStore"/> is the <see cref="InMemoryServerSideSessionStore"/> itself;
/// if it is wrapped (decorated), only the Default space is cleaned.
/// </remarks>
/// <remarks>
/// With Duende Storage (the default storage instance) or Spaces registered, at most one node in a
/// cluster runs a cleanup cycle at a time. Without either, every node runs cleanup independently, as
/// before. This also means the lock is never touched when <see cref="IStorageBackedSessionsMarker"/>
/// is registered, because the outbox processor and storage purge handle expiry in that case. The lock is
/// likewise skipped when the registered <see cref="IServerSideSessionStore"/> is the
/// <see cref="InMemoryServerSideSessionStore"/> itself: its sessions live in each node's own memory, so
/// every node must clean up its own. Shared stores (Entity Framework, custom) stay single-node.
/// </remarks>
public class ServerSideSessionCleanupHost(
    IServiceProvider serviceProvider,
    IdentityServerOptions options,
    ILogger<ServerSideSessionCleanupHost> logger) : BackgroundService
{
    private readonly TimeProvider _timeProvider = serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;

    private readonly SingleNodeJob _job = new(
        LockName.ServerSideSessionCleanup,
        LeaseReleaseMode.AfterEachRun,
        () => options.ServerSideSessions.RemoveExpiredSessionsFrequency,
        () => options.ServerSideSessions.Lease,
        serviceProvider.GetRequiredService<IDistributedLock>(),
        serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System,
        logger);

    /// <inheritdoc />
    public override Task StartAsync(Ct ct) =>
        !options.ServerSideSessions.RemoveExpiredSessions
            ? Task.CompletedTask
            : base.StartAsync(ct);

    /// <inheritdoc />
    public override async Task StopAsync(Ct ct)
    {
        await base.StopAsync(ct);
        await _job.ReleaseAsync(ct);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(Ct stoppingToken)
    {
        logger.StartingServerSideSessionRemoval();

        var removalFrequencySeconds = (int)options.ServerSideSessions.RemoveExpiredSessionsFrequency.TotalSeconds;

        // Start the first run at a random interval.
        var delay = options.ServerSideSessions.FuzzExpiredSessionRemovalStart
#pragma warning disable CA5394 // Randomness for security does not apply here
            ? TimeSpan.FromSeconds(Random.Shared.Next(Math.Max(1, removalFrequencySeconds)))
#pragma warning restore CA5394
            : options.ServerSideSessions.RemoveExpiredSessionsFrequency;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, _timeProvider, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                logger.TaskCanceledExceptionExiting();
                break;
            }
            catch (Exception ex)
            {
                logger.TaskDelayExceptionExceptionMessageExiting(ex.Message);
                break;
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var result = await RunCycleAsync(stoppingToken);
            switch (result)
            {
                case SingleNodeJobRunResult.SkippedNotLeaseHolder:
                    logger.SkippedNotLeaseHolder();
                    break;
                case SingleNodeJobRunResult.LeaseLost:
                    logger.LeaseLost();
                    break;
                case SingleNodeJobRunResult.LockUnavailable:
                    logger.LockUnavailable();
                    break;
                case SingleNodeJobRunResult.Ran:
                    break;
            }

            delay = _job.NextDelay(options.ServerSideSessions.RemoveExpiredSessionsFrequency);
        }

        logger.StoppingServerSideSessionRemoval();
    }

    /// <summary>
    /// Runs one cleanup cycle under the single-node job lock, unless storage-backed sessions make
    /// this host a no-op, in which case the lock is never touched.
    /// </summary>
    internal async Task<SingleNodeJobRunResult> RunCycleAsync(Ct ct)
    {
        // Checked here, before the lock is taken, so a no-op cycle (the outbox processor and
        // storage purge already handle expiry in this configuration) never creates or renews a
        // lock record.
        if (await UsesStorageBackedSessionsAsync(ct))
        {
            return SingleNodeJobRunResult.Ran;
        }

        if (await UsesInMemorySessionStoreAsync())
        {
            await RunAsync(ct);
            return SingleNodeJobRunResult.Ran;
        }

        return await _job.RunAsync(ctx => RunAsync(ctx.CancellationToken), ct);
    }

    private async Task<bool> UsesInMemorySessionStoreAsync()
    {
        await using var serviceScope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        return serviceScope.ServiceProvider.GetService<IServerSideSessionStore>() is InMemoryServerSideSessionStore;
    }

    private async Task<bool> UsesStorageBackedSessionsAsync(Ct ct)
    {
        await using var serviceScope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        return serviceScope.ServiceProvider.GetService<IStorageBackedSessionsMarker>() is not null;
    }

    internal async Task RunAsync(Ct ct)
    {
        // this is here for testing
        if (!options.ServerSideSessions.RemoveExpiredSessions)
        {
            return;
        }

        try
        {
            await using var serviceScope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            var scopedProvider = serviceScope.ServiceProvider;

            // When the storage layer handles session expiration via the outbox processor,
            // skip synchronous cleanup to avoid race conditions with StoragePurgeHost.
            if (scopedProvider.GetService<IStorageBackedSessionsMarker>() is not null)
            {
                return;
            }

            var serverSideTicketStore = scopedProvider.GetRequiredService<IServerSideTicketStore>();
            var sessionCoordinationService = scopedProvider.GetRequiredService<ISessionCoordinationService>();

            // Only the in-memory session store is partitioned by space, and only when Spaces is enabled.
            // Other stores (EF, custom) and the in-memory store without Spaces run once, outside of any space.
            var accessor = scopedProvider.GetService<ISpaceContextAccessor>();
            if (accessor is null || scopedProvider.GetRequiredService<IServerSideSessionStore>() is not InMemoryServerSideSessionStore inMemoryStore)
            {
                await RemoveExpiredSessionsAsync(serverSideTicketStore, sessionCoordinationService, ct);
                return;
            }

            // Expired sessions in every partition must be cleaned up. Snapshot the spaces first.
            // The Default space is set too: with Spaces on, data protection needs a resolved space.
            foreach (var spaceId in inMemoryStore.SpaceIds.ToArray())
            {
                try
                {
                    using (accessor.SetSpace(spaceId))
                    {
                        await RemoveExpiredSessionsAsync(serverSideTicketStore, sessionCoordinationService, ct);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    logger.ExceptionRemovingExpiredSessions(ex);
                }
            }
        }
        catch (Exception ex)
        {
            logger.ExceptionRemovingExpiredSessions(ex);
        }
    }

    private async Task RemoveExpiredSessionsAsync(
        IServerSideTicketStore serverSideTicketStore,
        ISessionCoordinationService sessionCoordinationService,
        Ct ct)
    {
        var found = int.MaxValue;

        while (found > 0)
        {
            var sessions = await serverSideTicketStore.GetAndRemoveExpiredSessionsAsync(options.ServerSideSessions.RemoveExpiredSessionsBatchSize, ct);
            found = sessions.Count;

            if (found <= 0)
            {
                continue;
            }

            logger.ProcessingExpirationForCountExpiredServerSideSessions(found);

            foreach (var session in sessions)
            {
                // Ct.None: these sessions are already removed from the store, so skipping them on
                // cancellation would leave their users signed in at clients.
                await sessionCoordinationService.ProcessExpirationAsync(session, Ct.None);
            }

            if (ct.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
