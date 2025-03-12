// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff;

/// <summary>
/// Service for handling silent login requests
/// </summary>
public class DefaultSilentLoginService : ISilentLoginService
{
    /// <summary>
    /// The BFF options
    /// </summary>
    protected readonly BffOptions Options;

    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public DefaultSilentLoginService(IOptions<BffOptions> options, ILogger<DefaultSilentLoginService> logger)
    {
        Options = options.Value;
        Logger = logger;
    }

    /// <inheritdoc />
    public virtual async Task ProcessRequestAsync(HttpContext context)
    {
        Logger.LogDebug("Processing silent login request");

        context.CheckForBffMiddleware(Options);

        var pathBase = context.Request.PathBase;
        var redirectPath = pathBase + Options.SilentLoginCallbackPath;

        var props = new AuthenticationProperties
        {
            RedirectUri = redirectPath,
            Items =
            {
                { Constants.BffFlags.SilentLogin, "true" }
            },
        };

        Logger.LogDebug("Silent login endpoint triggering Challenge with returnUrl {redirectUri}", redirectPath);

        await context.ChallengeAsync(props);
    }
}
