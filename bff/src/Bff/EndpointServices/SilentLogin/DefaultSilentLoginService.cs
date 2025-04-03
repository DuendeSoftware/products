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
public class DefaultSilentLoginService : ISilentLoginService
{
    /// <summary>
    /// The BFF options
    /// </summary>
    protected readonly BffOptions Options = options.Value;

    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger = logger;

    /// <inheritdoc />
    public virtual async Task ProcessRequestAsync(HttpContext context)
    {
        Logger.LogDebug("Processing silent login request");

        context.CheckForBffMiddleware(Options);

        var props = new AuthenticationProperties
        {
            Items =
            {
                { Constants.BffFlags.Prompt, "none" }
            },
        };

        Logger.LogWarning("Using deprecated silentlogin endpoint. This endpoint will be removed in future versions. Consider calling the BFF Login endpoint with prompt=none.");

        await context.ChallengeAsync(props);

    }
}
