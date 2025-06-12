// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.Bff.DynamicFrontends.Internal;

internal class OpenIdConnectCallbackMiddleware(RequestDelegate next,
    SelectedFrontend selector)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!selector.TryGet(out var frontend))
        {
            await next(context);
            return;
        }

        var oidcOptionsFactory = context.RequestServices.GetRequiredService<IOptionsFactory<OpenIdConnectOptions>>();
        var options = oidcOptionsFactory.Create(frontend.OidcSchemeName);

        if (context.Request.Path.StartsWithSegments(options.CallbackPath))
        {
            var handlers = context.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
            if (await handlers.GetHandlerAsync(context, frontend.OidcSchemeName) is IAuthenticationRequestHandler handler)
            {
                await handler.HandleRequestAsync();
                return;
            }
        }
        if (context.Request.Path.StartsWithSegments(options.SignedOutCallbackPath))
        {
            var handlers = context.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
            if (await handlers.GetHandlerAsync(context, frontend.OidcSchemeName) is IAuthenticationRequestHandler handler)
            {
                await handler.HandleRequestAsync();
                return;
            }
        }

        await next(context);
    }
}
