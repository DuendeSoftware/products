// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.Otel;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Endpoints.Internal;

/// <summary>
/// Service for handling logout requests
/// </summary>
internal class DefaultLogoutEndpoint(IOptions<BffOptions> options,
    IAuthenticationSchemeProvider authenticationSchemeProvider,
    IReturnUrlValidator returnUrlValidator,
    ILogger<DefaultLogoutEndpoint> logger)

    : ILogoutEndpoint
{
    /// <inheritdoc />
    public async Task ProcessRequestAsync(HttpContext context, CT ct = default)
    {
        logger.LogDebug("Processing logout request");

        context.CheckForBffMiddleware(options.Value);

        var result = await context.AuthenticateAsync();
        if (result.Succeeded && result.Principal?.Identity?.IsAuthenticated == true)
        {
            var userSessionId = result.Principal.FindFirst(JwtClaimTypes.SessionId)?.Value;
            if (!string.IsNullOrWhiteSpace(userSessionId))
            {
                var passedSessionId = context.Request.Query[JwtClaimTypes.SessionId].FirstOrDefault();
                // for an authenticated user, if they have a session id claim,
                // we require the logout request to pass that same value to
                // prevent unauthenticated logout requests (similar to OIDC front channel)
                if (options.Value.RequireLogoutSessionId && userSessionId != passedSessionId)
                {
                    logger.InvalidSid(LogLevel.Information, userSessionId);
                    context.ReturnHttpProblem("Invalid Session ID", (JwtClaimTypes.SessionId, [$"SessionId '{userSessionId}' was invalid"]));
                    return;
                }
            }
        }

        var returnUrl = context.Request.Query[Constants.RequestParameters.ReturnUrl].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            if (!returnUrlValidator.IsValidAsync(returnUrl))
            {
                logger.InvalidReturnUrl(LogLevel.Information, returnUrl.Sanitize());
                context.ReturnHttpProblem("Invalid return url", (Constants.RequestParameters.ReturnUrl, [$"ReturnUrl '{returnUrl}' was invalid"]));
                return;
            }
        }

        // get rid of local cookie first
        var signInScheme = await authenticationSchemeProvider.GetDefaultSignInSchemeAsync();
        await context.SignOutAsync(signInScheme?.Name);

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            if (context.Request.PathBase.HasValue)
            {
                returnUrl = context.Request.PathBase;
            }
            else
            {
                returnUrl = "/";
            }
        }

        var props = new AuthenticationProperties
        {
            RedirectUri = returnUrl
        };

        logger.LogDebug("Logout endpoint triggering SignOut with returnUrl {returnUrl}", returnUrl.Sanitize());

        // trigger idp logout
        await context.SignOutAsync(props);
    }
}
