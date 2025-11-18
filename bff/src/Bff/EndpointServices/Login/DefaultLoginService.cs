// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff;

/// <summary>
/// Service for handling login requests
/// </summary>
public class DefaultLoginService : ILoginService
{
    /// <summary>
    /// The BFF options
    /// </summary>
    protected readonly BffOptions Options;

    /// <summary>
    /// The return URL validator
    /// </summary>
    protected readonly IReturnUrlValidator ReturnUrlValidator;

    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="options"></param>
    /// <param name="returnUrlValidator"></param>
    /// <param name="logger"></param>
    public DefaultLoginService(IOptions<BffOptions> options, IReturnUrlValidator returnUrlValidator, ILogger<DefaultLoginService> logger)
    {
        Options = options.Value;
        ReturnUrlValidator = returnUrlValidator;
        Logger = logger;
    }

    /// <inheritdoc />
    public virtual async Task ProcessRequestAsync(HttpContext context)
    {
        Logger.LogDebug("Processing login request");

        context.CheckForBffMiddleware(Options);

        var returnUrl = context.Request.Query[Constants.RequestParameters.ReturnUrl].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            if (!await ReturnUrlValidator.IsValidAsync(returnUrl))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new HttpValidationProblemDetails()
                {
                    Title = "ReturnUrl is not valid",
                });

                return;
            }
        }

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

        Logger.LogDebug("Login endpoint triggering Challenge with returnUrl {returnUrl}", returnUrl);

        await context.ChallengeAsync(props);
    }
}
