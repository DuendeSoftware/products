// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
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

            foreach (var loader in bffOptions.MiddlewareLoaders)
            {
                loader(app);
            }
            if (bffOptions.AutomaticallyRegisterBffMiddleware)
            {
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

        frontendStore.OnFrontendChanged +=
            changedFrontend =>
            {
                // When the frontend changes, we need to clear the cached options
                // This make sure the (potentially) new OpenID Connect configuration
                // and cookie config is loaded
                cookieOptionsMonitor.TryRemove(changedFrontend.CookieSchemeName);
                oidcOptionsMonitor.TryRemove(changedFrontend.OidcSchemeName);
            };
    }
}
