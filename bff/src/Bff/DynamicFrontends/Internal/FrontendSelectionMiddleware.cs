// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Otel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.DynamicFrontends.Internal;

internal class FrontendSelectionMiddleware(
    RequestDelegate next,
    ILogger<FrontendSelectionMiddleware> logger,
    FrontendSelector frontendSelector)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var selector = context.RequestServices.GetRequiredService<CurrentFrontendAccessor>();

        // Find out if there is a frontend registered.
        if (frontendSelector.TrySelectFrontend(context.Request, out var selectedFrontend))
        {
            selector.Set(selectedFrontend);
            logger.SelectedFrontend(LogLevel.Debug, selectedFrontend.Name);
            using var scope = logger.BeginScope(new Dictionary<string, string>()
            {
                [OTelParameters.Frontend] = selectedFrontend.Name.ToString(),
            });
            await next(context);
        }
        else
        {
            logger.NoFrontendSelected(LogLevel.Debug);
            await next(context);
        }
    }
}
