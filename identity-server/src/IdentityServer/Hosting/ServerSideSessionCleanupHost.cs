// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Helper to clean up expired server side sessions.
/// </summary>
public class ServerSideSessionCleanupHost(
    IServiceProvider serviceProvider,
    IdentityServerOptions options,
    ILogger<ServerSideSessionCleanupHost> logger) : BackgroundService
{
    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken) =>
        !options.ServerSideSessions.RemoveExpiredSessions
            ? Task.CompletedTask
            : base.StartAsync(cancellationToken);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug("Starting server-side session removal");

        var removalFrequencySeconds = (int)options.ServerSideSessions.RemoveExpiredSessionsFrequency.TotalSeconds;

        // Start the first run at a random interval.
        var delay = options.ServerSideSessions.FuzzExpiredSessionRemovalStart
#pragma warning disable CA5394 // Randomness for security does not apply here
            ? TimeSpan.FromSeconds(Random.Shared.Next(removalFrequencySeconds))
#pragma warning restore CA5394
            : options.ServerSideSessions.RemoveExpiredSessionsFrequency;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                logger.LogDebug("TaskCanceledException. Exiting.");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError("Task.Delay exception: {ExceptionMessage}. Exiting.", ex.Message);
                break;
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await RunAsync(stoppingToken);

            delay = options.ServerSideSessions.RemoveExpiredSessionsFrequency;
        }

        logger.LogDebug("Stopping server-side session removal");
    }

    private async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // this is here for testing
        if (!options.ServerSideSessions.RemoveExpiredSessions)
        {
            return;
        }

        try
        {
            await using var serviceScope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            var scopedLogger = serviceScope.ServiceProvider.GetRequiredService<ILogger<ServerSideSessionCleanupHost>>();
            var scopedOptions = serviceScope.ServiceProvider.GetRequiredService<IdentityServerOptions>();
            var serverSideTicketStore = serviceScope.ServiceProvider.GetRequiredService<IServerSideTicketStore>();
            var sessionCoordinationService = serviceScope.ServiceProvider.GetRequiredService<ISessionCoordinationService>();

            var found = int.MaxValue;

            while (found > 0)
            {
                var sessions = await serverSideTicketStore.GetAndRemoveExpiredSessionsAsync(scopedOptions.ServerSideSessions.RemoveExpiredSessionsBatchSize, cancellationToken);
                found = sessions.Count;

                if (found <= 0)
                {
                    continue;
                }

                scopedLogger.LogDebug("Processing expiration for {count} expired server-side sessions.", found);

                foreach (var session in sessions)
                {
                    await sessionCoordinationService.ProcessExpirationAsync(session);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception removing expired sessions");
        }
    }
}
