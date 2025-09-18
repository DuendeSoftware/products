// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.Otel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.DynamicFrontends.Internal;

/// <summary>
/// This middleware performs the openid connect callback processing. This happens in two situations:
/// 1. A frontend is selected.
/// 2. No frontend is selected, but there is a default BFF OIDC scheme configured.
/// </summary>
internal class OpenIdConnectCallbackMiddleware(
    ILogger<OpenIdConnectCallbackMiddleware> logger,
    IOptions<BffOptions> bffOptions,
    RequestDelegate next,
    CurrentFrontendAccessor selector,
    IFrontendCollection frontends)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var oidcOptionsFactory = context.RequestServices.GetRequiredService<IOptionsFactory<OpenIdConnectOptions>>();

        var scheme = selector.TryGet(out var selectedFrontend)
            ? selectedFrontend.OidcSchemeName // A frontend is selected, use its scheme
            : BffAuthenticationSchemes.BffOpenIdConnect; // rely on the default scheme

        var options = oidcOptionsFactory.Create(scheme);

        if (options.ClientId == null || options.Authority == null)
        {
            // See if the scheme has been configured. if not, just resume as normally
            await next(context);
            return;
        }

        // Now, check if the implicit frontend (used when there is no frontend selected) should be enabled.
        if (selectedFrontend == null && frontends.Count != 0)
        {
            // Check if the user is calling one of the oidc or management routes.
            // These should be disabled. 
            if (IsOidcCallbackPath(context, options) || IsBffManagementPath(context, options))
            {
                logger.ImplicitFrontendDisabled(LogLevel.Warning, context.Request.Path.Sanitize());
                context.Response.StatusCode = 404;
                return;
            }

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

    private bool IsBffManagementPath(HttpContext context, OpenIdConnectOptions options)
    {
        var path = context.Request.Path;
        return
            path.StartsWithSegments(bffOptions.Value.LoginPath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments(bffOptions.Value.DiagnosticsPath, StringComparison.OrdinalIgnoreCase)
#pragma warning disable CS0618 // Type or member is obsolete
            || path.StartsWithSegments(bffOptions.Value.SilentLoginPath, StringComparison.OrdinalIgnoreCase)
#pragma warning restore CS0618 // Type or member is obsolete
            || path.StartsWithSegments(bffOptions.Value.BackChannelLogoutPath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments(bffOptions.Value.SilentLoginCallbackPath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments(bffOptions.Value.UserPath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments(bffOptions.Value.LogoutPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOidcCallbackPath(HttpContext context, OpenIdConnectOptions options)
    {
        var path = context.Request.Path;
        return path.StartsWithSegments(options.CallbackPath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments(options.SignedOutCallbackPath, StringComparison.OrdinalIgnoreCase);
    }
}
