// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff;

/// <summary>
/// Helper to cleanup expired sessions.
/// </summary>
public class SessionCleanupHost : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly BffOptions _options;
    private readonly ILogger<SessionCleanupHost> _logger;

    private TimeSpan CleanupInterval => _options.SessionCleanupInterval;

    private CancellationTokenSource? _source;

    /// <summary>
    /// Constructor for SessionCleanupHost.
    /// </summary>
    public SessionCleanupHost(IServiceProvider serviceProvider, IOptions<BffOptions> options, ILogger<SessionCleanupHost> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Starts the token cleanup polling.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.EnableSessionCleanup)
        {
            if (_source != null) throw new InvalidOperationException("Already started. Call Stop first.");

            if (IsIUserSessionStoreCleanupRegistered())
            {
                _logger.LogDebug("Starting BFF session cleanup");

                _source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                Task.Factory.StartNew(() => StartInternalAsync(_source.Token));
            }
            else
            {
                _logger.LogWarning("BFF session cleanup is enabled, but no IUserSessionStoreCleanup is registered in DI. BFF session cleanup will not run.");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the token cleanup polling.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_options.EnableSessionCleanup && _source != null)
        {
            _logger.LogDebug("Stopping BFF session cleanup");

            _source.Cancel();
            _source = null;
        }

        return Task.CompletedTask;
    }

    private async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("CancellationRequested. Exiting.");
                break;
            }

            try
            {
                await Task.Delay(CleanupInterval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogDebug("TaskCanceledException. Exiting.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError("Task.Delay exception: {0}. Exiting.", ex.Message);
                break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("CancellationRequested. Exiting.");
                break;
            }

            await RunAsync(cancellationToken);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using (var serviceScope = _serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var tokenCleanupService = serviceScope.ServiceProvider.GetRequiredService<IUserSessionStoreCleanup>();
                await tokenCleanupService.DeleteExpiredSessionsAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception deleting expired sessions: {exception}", ex.Message);
        }
    }

    private bool IsIUserSessionStoreCleanupRegistered()
    {
        var isService = _serviceProvider.GetRequiredService<IServiceProviderIsService>();
        return isService.IsService(typeof(IUserSessionStoreCleanup));
    }
}
