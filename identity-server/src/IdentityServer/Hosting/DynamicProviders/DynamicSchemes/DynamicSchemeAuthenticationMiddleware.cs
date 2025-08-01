// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Hosting.DynamicProviders;

internal class DynamicSchemeAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly DynamicProviderOptions _options;

    public DynamicSchemeAuthenticationMiddleware(RequestDelegate next, DynamicProviderOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task Invoke(HttpContext context)
    {
        string scheme = null;
        if (_options.PathMatchingCallback is not null)
        {
            scheme = await _options.PathMatchingCallback(context);
        }
        // this is needed to dynamically load the handler if this load balanced server
        // was not the one that initiated the call out to the provider
        else if (context.Request.Path.StartsWithSegments(_options.PathPrefix, StringComparison.InvariantCulture))
        {
            var startIndex = _options.PathPrefix.ToString().Length;
            if (context.Request.Path.Value?.Length > startIndex)
            {
                scheme = context.Request.Path.Value.Substring(startIndex + 1);
                var idx = scheme.IndexOf('/', StringComparison.InvariantCulture);
                if (idx > 0)
                {
                    // this assumes the path is: /<PathPrefix>/<scheme>/<extra>
                    // e.g.: /federation/my-oidc-provider/signin
                    scheme = scheme.Substring(0, idx);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(scheme))
        {
            var handlers = context.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
            if (await handlers.GetHandlerAsync(context, scheme) is IAuthenticationRequestHandler handler && await handler.HandleRequestAsync())
            {
                return;
            }
        }

        await _next(context);
    }
}
