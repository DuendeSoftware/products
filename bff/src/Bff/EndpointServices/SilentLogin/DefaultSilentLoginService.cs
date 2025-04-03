// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Duende.Bff;

/// <summary>
/// Service for handling silent login requests
/// </summary>
[Obsolete("This endpoint will be removed in a future version. Use /login?prompt=create")]
public class DefaultSilentLoginService(IOptions<BffOptions> options, ILogger<DefaultSilentLoginService> logger) : ISilentLoginService
{
    /// <summary>
    /// The BFF options
    /// </summary>
    protected readonly BffOptions Options = options.Value;

    /// <inheritdoc />
    public virtual async Task ProcessRequestAsync(HttpContext context)
    {
        logger.LogDebug("Processing silent login request");

        context.CheckForBffMiddleware(Options);

        var props = new AuthenticationProperties
        {
            Items =
            {
                { Constants.BffFlags.Prompt, "none" }
            },
        };

        logger.LogWarning("Using deprecated silentlogin endpoint. This endpoint will be removed in future versions. Consider calling the BFF Login endpoint with prompt=none.");

        await context.ChallengeAsync(props);
    }
}
