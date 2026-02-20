// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Threading.Channels;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff.Otel;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.DynamicFrontends.Internal;

/// <summary>
/// Takes care of clearing caches when a frontend changes.
/// </summary>
internal class BffCacheClearingHostedService(
    FrontendCollection frontendCollection,
    IOptionsMonitorCache<OpenIdConnectOptions> oidcOptionsMonitor,
    IOptionsMonitorCache<CookieAuthenticationOptions> cookieOptionsMonitor,
    IOptionsMonitorCache<ClientCredentialsClient> clientCredentialsCache,
    [FromKeyedServices(ServiceProviderKeys.ClientCredentialsTokenCache)] HybridCache hybridCache,
    ILogger<BffCacheClearingHostedService> logger) : BackgroundService
{
    private readonly Channel<BffFrontend> _channel = Channel.CreateUnbounded<BffFrontend>();
    private ChannelWriter<BffFrontend> Writer => _channel.Writer;
    private ChannelReader<BffFrontend> Reader => _channel.Reader;

    protected override async Task ExecuteAsync(CT ct)
    {
        // Subscribe to frontend changes and publish messages to the channel
        frontendCollection.OnFrontendChanged += changedFrontend =>
        {
            // When the frontend changes, we need to clear the cached options
            // This makes sure the (potentially) new OpenID Connect configuration
            // and cookie config is loaded
            cookieOptionsMonitor.TryRemove(changedFrontend.CookieSchemeName);
            oidcOptionsMonitor.TryRemove(changedFrontend.OidcSchemeName);

            // Duende.AccessTokenManagement also stores options. It's stored under the client name. 
            var clientCredentialsClientName = OpenIdConnectTokenManagementDefaults.ToClientName(changedFrontend.OidcSchemeName);
            clientCredentialsCache.TryRemove(clientCredentialsClientName);

            if (!Writer.TryWrite(changedFrontend))
            {
                logger.FailedToAddFrontendToQueue(LogLevel.Error, changedFrontend.Name);
            }
        };

        // Start the message processing loop
        await ProcessFrontendChangesAsync(ct);
    }

    private async Task ProcessFrontendChangesAsync(CT ct)
    {
        try
        {
            await foreach (var changedFrontend in Reader.ReadAllAsync(ct))
            {
                await ProcessFrontendChangeAsync(changedFrontend, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected when the service is stopping
        }
        // Have to catch all exceptions here to prevent the background service from crashing
#pragma warning disable CA1031 // Don't catch general exceptions
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.ErrorWhileProcessingFrontendChanges(LogLevel.Error, ex);
        }
    }

    private async Task ProcessFrontendChangeAsync(BffFrontend changedFrontend, CT ct)
    {
        try
        {
            logger.ChangedFrontendDetected_ClearingCaches(LogLevel.Debug, changedFrontend.Name);

            // Clear all cached entries for the client credentials cache
            // This is necessary to ensure that the new frontend's client credentials are used
            var clientCredentialsClientName = OpenIdConnectTokenManagementDefaults.ToClientName(changedFrontend.OidcSchemeName);
            await hybridCache.RemoveByTagAsync(clientCredentialsClientName, ct);

            // Also clear the index.html cache for the frontend
            await hybridCache.RemoveAsync(StaticFilesHttpClient.BuildCacheKey(changedFrontend), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected when the service is stopping
            throw;
        }
        // Have to catch all exceptions here to prevent the background service from crashing
#pragma warning disable CA1031 // Don't catch general exceptions
        catch (Exception e)
#pragma warning restore CA1031
        {
            logger.FailedToClearSchemeCache(LogLevel.Error, changedFrontend.Name, e);
        }
    }

    public override void Dispose()
    {
        Writer.Complete();
        base.Dispose();
    }
}
