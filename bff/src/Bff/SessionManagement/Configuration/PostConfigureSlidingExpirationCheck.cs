// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.Internal;
using Duende.Bff.Otel;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.SessionManagement.Configuration;

/// <summary>
/// Cookie configuration to suppress sliding the cookie on the ~/bff/user endpoint if requested.
/// </summary>
internal class PostConfigureSlidingExpirationCheck(
    ActiveCookieAuthenticationScheme activeCookieScheme,
    IOptions<BffOptions> bffOptions,
    ILogger<PostConfigureSlidingExpirationCheck> logger)
    : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly BffOptions _options = bffOptions.Value;

    /// <inheritdoc />
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (!activeCookieScheme.ShouldConfigureScheme(Scheme.ParseOrDefault(name)))
        {
            return;
        }

        options.Events.OnCheckSlidingExpiration = CreateCallback(options.Events.OnCheckSlidingExpiration);
    }

    private Func<CookieSlidingExpirationContext, Task> CreateCallback(Func<CookieSlidingExpirationContext, Task> inner)
    {
        Task Callback(CookieSlidingExpirationContext ctx)
        {
            var result = inner.Invoke(ctx);

            // disable sliding expiration
            if (ctx.HttpContext.Request.Path == _options.UserPath)
            {
                var slide = ctx.Request.Query[Constants.RequestParameters.SlideCookie];
                if (slide == "false")
                {
                    logger.SuppressingSlideBehaviorOnCheckSlidingExpiration(LogLevel.Debug);
                    ctx.ShouldRenew = false;
                }
            }

            return result;
        }

        return Callback;
    }
}
