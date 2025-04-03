// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff;

/// <summary>
/// Cookie configuration to suppress sliding the cookie on the ~/bff/user endpoint if requested.
/// </summary>
[Obsolete(Constants.ObsoleteMessages.ImplementationWillBeMadeInternal)]
public class PostConfigureSlidingExpirationCheck(
    IOptions<BffOptions> bffOptions,
    IOptions<AuthenticationOptions> authOptions,
    ILogger<PostConfigureSlidingExpirationCheck> logger) 
    : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly BffOptions _options = bffOptions.Value;
    private readonly string? _scheme = authOptions.Value.DefaultAuthenticateScheme ?? authOptions.Value.DefaultScheme;

    /// <inheritdoc />
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (name == _scheme)
        {
            options.Events.OnCheckSlidingExpiration = CreateCallback(options.Events.OnCheckSlidingExpiration);
        }
    }

    private Func<CookieSlidingExpirationContext, Task> CreateCallback(Func<CookieSlidingExpirationContext, Task> inner)
    {
        Task Callback(CookieSlidingExpirationContext ctx)
        {
            var result = inner?.Invoke(ctx) ?? Task.CompletedTask;

            // disable sliding expiration
            if (ctx.HttpContext.Request.Path == _options.UserPath)
            {
                var slide = ctx.Request.Query[Constants.RequestParameters.SlideCookie];
                if (slide == "false")
                {
                    logger.LogDebug("Explicitly setting ShouldRenew=false in OnCheckSlidingExpiration due to query param suppressing slide behavior.");
                    ctx.ShouldRenew = false;
                }
            }

            return result;
        }

        return Callback;
    }
}
