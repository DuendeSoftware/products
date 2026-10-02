// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using StorageOutboxProcessor = Duende.Storage.Internal.Outbox.OutboxProcessor;

namespace Duende.IdentityServer.Hosting.OutboxProcessor;

/// <summary>
/// Background service that drives Duende.Storage's <see cref="StorageOutboxProcessor"/> on a timer.
/// This host owns only hosting concerns: whether the processor runs at all, how often it runs,
/// and startup jitter. All processing semantics (batching, subscription isolation, ambient context,
/// retry/backoff, and drop behaviour) belong to the Storage processor.
/// </summary>
internal sealed class OutboxProcessorHost(
    StorageOutboxProcessor processor,
    IdentityServerOptions options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessorHost> logger) : BackgroundService
{
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    public override Task StartAsync(Ct ct) =>
        !options.OutboxProcessor.EnableProcessor
            ? Task.CompletedTask
            : base.StartAsync(ct);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(Ct stoppingToken)
    {
        logger.StartingProcessor(LogLevel.Debug);

        var interval = options.OutboxProcessor.ProcessInterval < MinInterval
            ? MinInterval
            : options.OutboxProcessor.ProcessInterval;

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
                await processor.RunProcessorAsync(stoppingToken);
            }

            delay = interval;
        }

        logger.StoppingProcessor(LogLevel.Debug);
    }
}
