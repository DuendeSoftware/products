// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics;

internal class DiagnosticHostedService(DiagnosticSummary diagnosticSummary, IOptions<IdentityServerOptions> options, ILogger<DiagnosticHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Diagnostics.LogFrequency);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await diagnosticSummary.PrintSummary();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while logging the diagnostic summary: {Message}", ex.Message);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await diagnosticSummary.PrintSummary();

        await base.StopAsync(cancellationToken);
    }
}
