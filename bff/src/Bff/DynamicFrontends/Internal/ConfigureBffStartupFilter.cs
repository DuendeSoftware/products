// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff.Configuration;
using Duende.Bff.Otel;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.DynamicFrontends.Internal;

internal class ConfigureBffStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            var bffOptions = app.ApplicationServices.GetRequiredService<IOptions<BffOptions>>()
                .Value;

            if (bffOptions.AutomaticallyRegisterBffMiddleware)
            {
                app.UseBffFrontendSelection();
                app.UseBffPathMapping();
                app.UseBffOpenIdCallbacks();
            }

            next(app);

            if (bffOptions.AutomaticallyRegisterBffMiddleware)
            {
                foreach (var loader in bffOptions.MiddlewareLoaders)
                {
                    loader(app);
                }
                app.UseEndpoints(endpoints =>
                {
                    if (!endpoints.AlreadyMappedManagementEndpoint(bffOptions.LoginPath, "Login"))
                    {
                        endpoints.MapBffManagementEndpoints();
                    }
                });
                app.UseBffIndexPages();

            }

            ConfigureOpenIdConfigurationCacheExpiration(app);
        };

    private static void ConfigureOpenIdConfigurationCacheExpiration(IApplicationBuilder app)
    {
        var frontendStore = app.ApplicationServices.GetRequiredService<FrontendCollection>();
        var oidcOptionsMonitor = app.ApplicationServices.GetRequiredService<IOptionsMonitorCache<OpenIdConnectOptions>>();
        var cookieOptionsMonitor = app.ApplicationServices.GetRequiredService<IOptionsMonitorCache<CookieAuthenticationOptions>>();
        var clientCredentialsCache = app.ApplicationServices.GetRequiredService<IOptionsMonitorCache<ClientCredentialsClient>>();
        var hybridCache = app.ApplicationServices.GetRequiredKeyedService<HybridCache>(ServiceProviderKeys.ClientCredentialsTokenCache);
        var logger = app.ApplicationServices.GetRequiredService<ILogger<ConfigureBffStartupFilter>>();

        frontendStore.OnFrontendChanged +=
            changedFrontend =>
            {
                logger.ChangedFrontendDetected_ClearingCaches(LogLevel.Debug, changedFrontend.Name);

                // When the frontend changes, we need to clear the cached options
                // This make sure the (potentially) new OpenID Connect configuration
                // and cookie config is loaded
                cookieOptionsMonitor.TryRemove(changedFrontend.CookieSchemeName);
                oidcOptionsMonitor.TryRemove(changedFrontend.OidcSchemeName);

                // Duende.AccessTokenManagement also stores options. It's stored under the client name. 
                var clientCredentialsClientName = OpenIdConnectTokenManagementDefaults.ToClientName(changedFrontend.OidcSchemeName);
                clientCredentialsCache.TryRemove(clientCredentialsClientName);

                // Clearing all cached entries for the client credentials cache as fire-and-forget (since it's async).
                // This is necessary to ensure that the new frontend's client credentials are used
                var _ = Task.Run(async () =>
                {
                    try
                    {
                        await hybridCache.RemoveByTagAsync(clientCredentialsClientName);
                    }
#pragma warning disable CA1031 // Ignore exceptions here, as this is a fire-and-forget operation
                    catch (Exception e)
#pragma warning restore CA1031
                    {
                        logger.FailedToClearSchemeCache(LogLevel.Error, changedFrontend.Name, e);
                    }
                });
            };
    }
}
