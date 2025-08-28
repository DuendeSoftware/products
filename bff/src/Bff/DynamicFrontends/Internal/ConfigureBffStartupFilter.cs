// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.SessionManagement.Configuration;
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
                app.UseForwardedHeaders();
                app.UseBffPreProcessing();
            }

            next(app);

            if (bffOptions.AutomaticallyRegisterBffMiddleware)
            {
                app.UseBffPostProcessing();

            }
        };
}
