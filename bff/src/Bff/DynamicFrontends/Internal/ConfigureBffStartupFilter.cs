// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.SessionManagement.Configuration;
using Duende.Private.Licensing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

            var license = app.ApplicationServices.GetRequiredService<LicenseAccessor<BffLicense>>().Current;

            if (!license.IsConfigured)
            {
                app.UseMiddleware<TrialModeMiddleware>();
            }

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

internal class TrialModeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (TrialModeSessionLimitExceededException ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync(ex.Message);
        }
    }
}
