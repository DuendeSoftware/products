// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Diagnostics;

internal class DiagnosticHostedService(
    IOptions<BffOptions> options,
    DiagnosticSummary diagnosticsSummary,
    ILogger<DiagnosticHostedService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Diagnostics.LogFrequency, timeProvider);
        try
        {
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await diagnosticsSummary.PrintSummaryAsync(stoppingToken);
                }
#pragma warning disable CA1031
                // Catching general exceptions here to prevent the host from crashing.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    logger.FailedToLogDiagnosticsSummary(ex.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // When stopping this hosted service, "await timer.WaitForNextTickAsync(stoppingToken)" can throw an OperationCanceledException.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await diagnosticsSummary.PrintSummaryAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }
}
