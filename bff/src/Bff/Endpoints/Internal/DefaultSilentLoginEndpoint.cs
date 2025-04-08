// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Endpoints.Internal;

/// <summary>
/// Service for handling silent login requests
/// </summary>
internal class DefaultSilentLoginEndpoint(IOptions<BffOptions> options, ILogger<DefaultSilentLoginEndpoint> logger) : ISilentLoginEndpoint
{
    /// <summary>
    /// The BFF options
    /// </summary>
    private readonly BffOptions _options = options.Value;

    /// <inheritdoc />
    public async Task ProcessRequestAsync(HttpContext context, CT ct = default)
    {
        logger.LogDebug("Processing silent login request");

        context.CheckForBffMiddleware(_options);

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
