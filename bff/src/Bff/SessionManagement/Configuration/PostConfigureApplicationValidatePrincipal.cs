// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.Internal;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.SessionManagement.Configuration;

/// <summary>
/// Cookie configuration to suppress sliding the cookie on the ~/bff/user endpoint if requested.
/// </summary>
internal class PostConfigureApplicationValidatePrincipal(
    ActiveCookieAuthenticationScheme activeCookieScheme,
    IOptions<BffOptions> bffOptions,
    ILogger<PostConfigureApplicationValidatePrincipal> logger) : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly BffOptions _options = bffOptions.Value;

    /// <inheritdoc />
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (activeCookieScheme.ShouldConfigureScheme(Scheme.ParseOrDefault(name)))
        {
            options.Events.OnValidatePrincipal = CreateCallback(options.Events.OnValidatePrincipal);
        }
    }

    private Func<CookieValidatePrincipalContext, Task> CreateCallback(Func<CookieValidatePrincipalContext, Task> inner)
    {
        Task Callback(CookieValidatePrincipalContext ctx)
        {
            var result = inner.Invoke(ctx);

            // allows the client-side app to request that the cookie does not slide on the user endpoint
            // we must add this logic in the OnValidatePrincipal because it's a code path that can trigger the 
            // cookie to slide regardless of the CookieOption's sliding feature
            // we suppress the behavior by setting ShouldRenew on the validate principal context
            if (ctx.HttpContext.Request.Path == _options.UserPath)
            {
                var slide = ctx.Request.Query[Constants.RequestParameters.SlideCookie];
                if (slide == "false")
                {
                    logger.LogDebug("Explicitly setting ShouldRenew=false in OnValidatePrincipal due to query param suppressing slide behavior.");
                    ctx.ShouldRenew = false;
                }
            }

            return result;
        }

        return Callback;
    }
}
