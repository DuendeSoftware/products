// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.Coordination;
using Duende.IdentityServer.Infrastructure.DistributedLock;
using Duende.Storage.Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting;

/// <summary>
/// Background service that periodically purges expired entities from the storage layer.
/// </summary>
internal sealed class StoragePurgeHost(
    IStorageInstanceRouter storageInstanceRouter,
    ICrossPartitionStorageFactory crossPartitionStorageFactory,
    IdentityServerOptions options,
    IDistributedLock @lock,
    TimeProvider timeProvider,
    ILogger<StoragePurgeHost> logger) : BackgroundService
{
    // IPartitionedStorage.PurgeExpiredAsync enforces [1, 1000]; clamp here to avoid noisy exceptions.
    private const int MinBatchSize = 1;
    private const int MaxBatchSize = 1000;
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);

    private readonly SingleNodeJob _job = new(
        LockName.StoragePurge,
        LeaseReleaseMode.AfterEachRun,
        () => ResolveInterval(options),
        () => options.StoragePurge.Lease,
        @lock,
        timeProvider,
        logger);

    /// <inheritdoc />
    public override Task StartAsync(Ct ct) =>
        !options.StoragePurge.EnablePurge
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
        logger.StartingPurge(LogLevel.Debug);

        var interval = ResolveInterval(options);
        var intervalSeconds = (int)interval.TotalSeconds;

        // Start the first run at a random interval.
        var delay = options.StoragePurge.FuzzStartup
#pragma warning disable CA5394 // Randomness for security does not apply here
            ? TimeSpan.FromSeconds(Random.Shared.Next(Math.Max(1, intervalSeconds)))
#pragma warning restore CA5394
            : interval;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.CancellationRequested(LogLevel.Debug);
                break;
            }
            catch (Exception ex)
            {
                logger.DelayException(LogLevel.Error, ex.Message);
                break;
            }

            var result = await RunCycleAsync(stoppingToken);
            switch (result)
            {
                case SingleNodeJobRunResult.SkippedNotLeaseHolder:
                    logger.SkippedNotLeaseHolder(LogLevel.Debug);
                    break;
                case SingleNodeJobRunResult.LeaseLost:
                    logger.LeaseLost(LogLevel.Warning);
                    break;
                case SingleNodeJobRunResult.LockUnavailable:
                    logger.LockUnavailable(LogLevel.Warning);
                    break;
                case SingleNodeJobRunResult.Ran:
                    break;
            }

            delay = _job.NextDelay(interval);
        }

        logger.StoppingPurge(LogLevel.Debug);
    }

    internal Task<SingleNodeJobRunResult> RunCycleAsync(Ct ct) =>
        _job.RunAsync(ctx => RunPurgeAsync(ctx.CancellationToken), ct);

    private static TimeSpan ResolveInterval(IdentityServerOptions options) =>
        options.StoragePurge.PurgeInterval < MinInterval
            ? MinInterval
            : options.StoragePurge.PurgeInterval;

    internal async Task RunPurgeAsync(Ct ct)
    {
        // This guard is here for testability (can disable mid-run).
        if (!options.StoragePurge.EnablePurge)
        {
            return;
        }

        foreach (var storageInstanceId in storageInstanceRouter.GetAll())
        {
            try
            {
                var crossPartitionStorage = await crossPartitionStorageFactory.GetCrossPartitionStorageAsync(storageInstanceId, ct);

                // Purge every registered storage instance/pool in turn (not just a single
                // operational-category instance), so a multi-instance/separate-database deployment
                // purges expired entities everywhere they may live.
                var batchSize = Math.Clamp(options.StoragePurge.BatchSize, MinBatchSize, MaxBatchSize);

                var deleted = batchSize;
                while (deleted >= batchSize)
                {
                    ct.ThrowIfCancellationRequested();
                    deleted = await crossPartitionStorage.PurgeExpiredAsync(batchSize, ct);
                    logger.PurgedBatch(LogLevel.Debug, deleted);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Expected during shutdown — not an error.
                logger.PurgeCancelled(LogLevel.Debug);
                return;
            }
            catch (Exception ex)
            {
                // A failing instance must not stop purging of the others. This also catches an
                // OperationCanceledException that did not originate from the caller-supplied ct
                // (e.g. a store-internal timeout token), which must not be mistaken for a
                // requested shutdown and silently abort purging of the remaining instances.
                logger.PurgeException(LogLevel.Error, ex, storageInstanceId);
            }
        }
    }
}
