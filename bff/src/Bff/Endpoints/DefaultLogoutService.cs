// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Endpoints;

/// <summary>
/// Service for handling logout requests
/// </summary>
internal class DefaultLogoutService(IOptions<BffOptions> options,
    IAuthenticationSchemeProvider authenticationAuthenticationSchemeProviderProvider,
    IReturnUrlValidator returnUrlValidator,
    ILogger<DefaultLogoutService> logger)
    : ILogoutService
{
    /// <summary>
    /// The BFF options
    /// </summary>
    protected readonly BffOptions Options = options.Value;

    /// <summary>
    /// The scheme provider
    /// </summary>
    protected readonly IAuthenticationSchemeProvider AuthenticationSchemeProvider = authenticationAuthenticationSchemeProviderProvider;

    /// <summary>
    /// The return URL validator
    /// </summary>
    protected readonly IReturnUrlValidator ReturnUrlValidator = returnUrlValidator;

    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger = logger;

    /// <inheritdoc />
    public virtual async Task ProcessRequestAsync(HttpContext context)
    {
        Logger.LogDebug("Processing logout request");

        context.CheckForBffMiddleware(Options);

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
                if (Options.RequireLogoutSessionId && userSessionId != passedSessionId)
                {
                    throw new Exception("Invalid Session Id");
                }
            }
        }

        var returnUrl = context.Request.Query[Constants.RequestParameters.ReturnUrl].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            if (!await ReturnUrlValidator.IsValidAsync(returnUrl))
            {
                throw new Exception("returnUrl is not valid: " + returnUrl);
            }
        }

        // get rid of local cookie first
        var signInScheme = await AuthenticationSchemeProvider.GetDefaultSignInSchemeAsync();
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

        Logger.LogDebug("Logout endpoint triggering SignOut with returnUrl {returnUrl}", returnUrl);

        // trigger idp logout
        await context.SignOutAsync(props);
    }
}
