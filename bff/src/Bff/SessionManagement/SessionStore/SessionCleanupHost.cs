// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.Otel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.SessionManagement.SessionStore;

/// <summary>
/// Helper to cleanup expired sessions.
/// </summary>
internal class SessionCleanupHost(
    BffMetrics metrics,
    IServiceProvider serviceProvider,
    IOptions<BffOptions> options,
    ILogger<SessionCleanupHost> logger) : BackgroundService
{
    private readonly BffOptions _options = options.Value;

    private TimeSpan CleanupInterval => _options.SessionCleanupInterval;

    public override Task StartAsync(Ct ct)
    {
        if (!IsIUserSessionStoreCleanupRegistered())
        {
            logger.SessionCleanupNotRegistered(LogLevel.Error);
            throw new InvalidOperationException("No IUserSessionStoreCleanup is registered. Did you add session storage, such as EntityFramework?");
        }

        return base.StartAsync(ct);
    }

    protected override async Task ExecuteAsync(Ct ct)
    {
        while (true)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(CleanupInterval, ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }

#pragma warning disable CA1031// Do not catch general exception types
            // Catching general exceptions here to prevent the host from crashing if an exception occurs during the delay.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.FailedToCleanupSession(LogLevel.Error, ex);
                break;
            }

            if (ct.IsCancellationRequested)
            {
                break;
            }

            await RunAsync(ct);
        }
    }

    internal async Task RunAsync(Ct ct = default)
    {
        try
        {
            using var serviceScope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var tokenCleanupService = serviceScope.ServiceProvider.GetRequiredService<IUserSessionStoreCleanup>();
            var removed = await tokenCleanupService.DeleteExpiredSessionsAsync(ct);
            metrics.SessionsEnded(removed);
        }
#pragma warning disable CA1031// Do not catch general exception types
        // Catching general exceptions here to prevent the host from crashing if an exception occurs during the cleanup.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.FailedToCleanupExpiredSessions(LogLevel.Error, ex);
        }
    }

    private bool IsIUserSessionStoreCleanupRegistered()
    {
        var isService = serviceProvider.GetRequiredService<IServiceProviderIsService>();
        return isService.IsService(typeof(IUserSessionStoreCleanup));
    }
}
