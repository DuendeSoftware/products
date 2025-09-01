// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.Bff.DynamicFrontends.Internal;

/// <summary>
/// This middleware performs the openid connect callback processing. This happens in two situations:
/// 1. A frontend is selected.
/// 2. No frontend is selected, but there is a default BFF OIDC scheme configured.
/// </summary>
internal class OpenIdConnectCallbackMiddleware(RequestDelegate next,
    CurrentFrontendAccessor selector)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var oidcOptionsFactory = context.RequestServices.GetRequiredService<IOptionsFactory<OpenIdConnectOptions>>();

        var scheme = selector.TryGet(out var frontend)
                ? frontend.OidcSchemeName // A frontend is selected, use its scheme
                : BffAuthenticationSchemes.BffOpenIdConnect; // rely on the default scheme

        var options = oidcOptionsFactory.Create(scheme);

        if (options.ClientId == null || options.Authority == null)
        {
            // See if the scheme has been configured. if not, just resume as normally
            await next(context);
            return;
        }

        if (IsOidcCallbackPath(context, options))
        {
            // Currently processing a signin or signout callback
            var handlers = context.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
            if (await handlers.GetHandlerAsync(context, scheme) is IAuthenticationRequestHandler handler)
            {
                await handler.HandleRequestAsync();
                return;
            }
        }

        await next(context);
    }

    private static bool IsOidcCallbackPath(HttpContext context, OpenIdConnectOptions options)
    {
        var path = context.Request.Path;
        return path.StartsWithSegments(options.CallbackPath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments(options.SignedOutCallbackPath, StringComparison.OrdinalIgnoreCase);
    }
}
