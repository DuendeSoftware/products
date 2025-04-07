// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Endpoints;

/// <summary>
/// Service for handling login requests
/// </summary>
internal class DefaultLoginService(
    IAuthenticationSchemeProvider authenticationSchemeProvider,
    IOptionsMonitor<OpenIdConnectOptions> openIdConnectOptionsMonitor,
    IOptions<BffOptions> bffOptions,
    IReturnUrlValidator returnUrlValidator,
    ILogger<DefaultLoginService> logger)
    : ILoginService
{
    /// <summary>
    /// The BFF options
    /// </summary>
    protected readonly BffOptions Options = bffOptions.Value;

    private readonly IOptionsMonitor<OpenIdConnectOptions> _openIdConnectOptionsMonitor = openIdConnectOptionsMonitor;

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
        Logger.LogDebug("Processing login request");

        context.CheckForBffMiddleware(Options);

        var returnUrl = context.Request.Query[Constants.RequestParameters.ReturnUrl].FirstOrDefault();

        var prompt = context.Request.Query[Constants.RequestParameters.Prompt].FirstOrDefault();

        var supportedPromptValues = await GetPromptValuesAsync();
        if (prompt != null && !supportedPromptValues.Contains(prompt))
        {
            context.ReturnHttpProblem("Invalid prompt value", ("prompt", [$"prompt '{prompt}' is not supported"]));
            return;
        }

        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            if (!await ReturnUrlValidator.IsValidAsync(returnUrl))
            {
                throw new Exception("returnUrl is not valid: " + returnUrl);
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
            RedirectUri = returnUrl,
        };

        if (prompt != null)
        {
            props.Items.Add(Constants.BffFlags.Prompt, prompt);
        }

        Logger.LogDebug("Login endpoint triggering Challenge with returnUrl {returnUrl}", returnUrl);

        await context.ChallengeAsync(props);
    }

    private async Task<ICollection<string>> GetPromptValuesAsync()
    {
        var scheme = await authenticationSchemeProvider.GetDefaultChallengeSchemeAsync();
        if (scheme == null)
        {
            throw new Exception("Failed to obtain default challenge scheme");
        }

        var options = _openIdConnectOptionsMonitor.Get(scheme.Name);
        if (options == null)
        {
            throw new Exception("Failed to obtain OIDC options for default challenge scheme");
        }

        var config = options.Configuration;
        if (config == null)
        {
            config = await options.ConfigurationManager?.GetConfigurationAsync(CancellationToken.None)!;
        }

        if (config == null)
        {
            throw new Exception("Failed to obtain OIDC configuration");
        }

        return config.PromptValuesSupported;
    }
}
