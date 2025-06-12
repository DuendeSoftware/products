// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
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
                app.UseBffIndexPages();
            }

            ConfigureOpenIdConfigurationCacheExpiration(app);
        };

    private static void ConfigureOpenIdConfigurationCacheExpiration(IApplicationBuilder app)
    {
        var frontendStore = app.ApplicationServices.GetRequiredService<FrontendCollection>();
        var optionsMonitor = app.ApplicationServices.GetRequiredService<IOptionsMonitorCache<OpenIdConnectOptions>>();

        frontendStore.OnFrontendChanged +=
            changedFrontend => optionsMonitor.TryRemove(changedFrontend.OidcSchemeName);
    }
}
