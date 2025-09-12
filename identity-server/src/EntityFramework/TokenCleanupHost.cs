// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework;
using Duende.IdentityServer.EntityFramework.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Helper to cleanup expired persisted grants.
/// </summary>
public class TokenCleanupHost : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly OperationalStoreOptions _options;
    private readonly ILogger<TokenCleanupHost> _logger;

    private TimeSpan CleanupInterval => TimeSpan.FromSeconds(_options.TokenCleanupInterval);

    private CancellationTokenSource _source;

    /// <summary>
    /// Constructor for TokenCleanupHost.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public TokenCleanupHost(IServiceProvider serviceProvider, OperationalStoreOptions options, ILogger<TokenCleanupHost> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <summary>
    /// Starts the token cleanup polling.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.EnableTokenCleanup)
        {
            if (_source != null)
            {
                throw new InvalidOperationException("Already started. Call Stop first.");
            }

            _logger.LogDebug("Starting grant removal");

            _source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _ = Task.Factory.StartNew(() => StartInternalAsync(_source.Token), cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the token cleanup polling.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_options.EnableTokenCleanup)
        {
            if (_source == null)
            {
                throw new InvalidOperationException("Not started. Call Start first.");
            }

            _logger.LogDebug("Stopping grant removal");

            await _source.CancelAsync();
            _source = null;
        }
    }

    private async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        // Start the first run at a random interval.
        var delay = _options.FuzzTokenCleanupStart
#pragma warning disable CA5394 // Randomness for security does not apply here
            ? TimeSpan.FromSeconds(Random.Shared.Next(_options.TokenCleanupInterval))
#pragma warning restore CA5394
            : CleanupInterval;

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("CancellationRequested. Exiting.");
                break;
            }

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogDebug("TaskCanceledException. Exiting.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError("Task.Delay exception: {ExceptionMessage}. Exiting.", ex.Message);
                break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("CancellationRequested. Exiting.");
                break;
            }

            await RemoveExpiredGrantsAsync(cancellationToken);

            // For all subsequent runs use the configured interval.
            delay = CleanupInterval;
        }
    }

    private async Task RemoveExpiredGrantsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var serviceScope = _serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            var tokenCleanupService = serviceScope.ServiceProvider.GetRequiredService<ITokenCleanupService>();
            await tokenCleanupService.CleanupGrantsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception removing expired grants: {exception}", ex.Message);
        }
    }
}
