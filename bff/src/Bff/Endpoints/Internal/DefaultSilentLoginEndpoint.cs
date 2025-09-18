// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.Otel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Endpoints.Internal;

/// <summary>
/// Service for handling silent login requests
/// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
internal class DefaultSilentLoginEndpoint(IOptions<BffOptions> options, ILogger<DefaultSilentLoginEndpoint> logger) : ISilentLoginEndpoint
#pragma warning restore CS0618 // Type or member is obsolete
{
    /// <summary>
    /// The BFF options
    /// </summary>
    private readonly BffOptions _options = options.Value;

    /// <inheritdoc />
    public async Task ProcessRequestAsync(HttpContext context, CT ct = default)
    {
        logger.ProcessingSilentLoginRequest(LogLevel.Debug);

        context.CheckForBffMiddleware(_options);

        var props = new AuthenticationProperties
        {
            Items =
            {
                { Constants.BffFlags.Prompt, "none" }
            },
        };

        logger.UsingDeprecatedSilentLoginEndpoint(LogLevel.Warning);

        await context.ChallengeAsync(props);
    }
}
