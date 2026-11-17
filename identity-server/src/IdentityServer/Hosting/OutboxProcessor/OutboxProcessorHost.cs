// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.Coordination;
using Duende.IdentityServer.Infrastructure.DistributedLock;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.OutboxProcessor;

/// <summary>
/// Background service that drives Duende.Storage's <see cref="StorageOutboxProcessor"/> on a timer.
/// This host owns hosting concerns: whether the processor runs at all, how often it runs, startup
/// jitter, and (with Duende Storage or Spaces) coordinating a single owner across nodes. All
/// processing semantics (batching, subscription isolation, ambient context, retry/backoff, and drop
/// behaviour) belong to the Storage processor.
/// </summary>
internal sealed class OutboxProcessorHost(
    OutboxProcessorFactory processorFactory,
    IdentityServerOptions options,
    IDistributedLock @lock,
    TimeProvider timeProvider,
    ILogger<OutboxProcessorHost> logger) : BackgroundService
{
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);

    private readonly SingleNodeJob _job = new(
        LockName.OutboxProcessor,
        LeaseReleaseMode.KeepAcrossRuns,
        () => ResolveInterval(options),
        () => options.OutboxProcessor.Lease,
        @lock,
        timeProvider,
        logger);

    private StorageOutboxProcessor? _processor;

    /// <inheritdoc />
    public override Task StartAsync(Ct ct) =>
        !options.OutboxProcessor.EnableProcessor
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
        logger.StartingProcessor(LogLevel.Debug);

        var interval = ResolveInterval(options);
        var intervalSeconds = (int)interval.TotalSeconds;

        // Start the first run at a random interval.
        var delay = options.OutboxProcessor.FuzzStartup
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
                logger.DelayException(LogLevel.Error, ex);
                break;
            }

            // Re-checked every cycle rather than only at StartAsync: EnableProcessor can be
            // flipped after the host has already started.
            if (options.OutboxProcessor.EnableProcessor)
            {
                await RunCycleAsync(stoppingToken);
                delay = _job.NextDelay(interval);
            }
            else
            {
                // A node that stops processing must not keep holding the lease: another node
                // (or this one, if re-enabled later) needs to be able to take over at once.
                await _job.ReleaseAsync(stoppingToken);
                delay = interval;
            }
        }

        logger.StoppingProcessor(LogLevel.Debug);
    }

    private async Task RunCycleAsync(Ct stoppingToken)
    {
        var result = await _job.RunAsync(
            async ctx =>
            {
                if (ctx.LeaseNewlyAcquired || _processor is null)
                {
                    // A newly acquired lease means some other node (or nobody) owned the outbox
                    // before this run; a fresh processor starts with clean in-memory retry state.
                    logger.NewLeaseFreshProcessor(LogLevel.Debug);
                    _processor = processorFactory.Create();
                }

                await _processor.RunProcessorAsync(ctx.CancellationToken);
            },
            stoppingToken);

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
    }

    private static TimeSpan ResolveInterval(IdentityServerOptions options) =>
        options.OutboxProcessor.ProcessInterval < MinInterval
            ? MinInterval
            : options.OutboxProcessor.ProcessInterval;
}
