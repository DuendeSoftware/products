// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
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
    /// Authentication scheme provider
    /// </summary>
    protected readonly IAuthenticationSchemeProvider AuthenticationSchemeProvider;

    /// <summary>
    /// The OIDC options monitor
    /// </summary>
    protected readonly IOptionsMonitor<OpenIdConnectOptions> OptionsMonitor;
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
    /// <param name="optionsMonitor"></param>
    /// <param name="options"></param>
    /// <param name="returnUrlValidator"></param>
    /// <param name="logger"></param>
    /// <param name="authenticationSchemeProvider"></param>
    public DefaultLoginService(
        IAuthenticationSchemeProvider authenticationSchemeProvider,
        IOptionsMonitor<OpenIdConnectOptions> optionsMonitor,
        IOptions<BffOptions> options,
        IReturnUrlValidator returnUrlValidator,
        ILogger<DefaultLoginService> logger)
    {
        Options = options.Value;
        AuthenticationSchemeProvider = authenticationSchemeProvider;
        OptionsMonitor = optionsMonitor;
        ReturnUrlValidator = returnUrlValidator;
        Logger = logger;
    }

    /// <inheritdoc />
    public virtual async Task ProcessRequestAsync(HttpContext context)
    {
        Logger.LogDebug("Processing login request");

        context.CheckForBffMiddleware(Options);

        var returnUrl = context.Request.Query[Constants.RequestParameters.ReturnUrl].FirstOrDefault();

        var prompt = context.Request.Query["prompt"].FirstOrDefault();

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
        var scheme = await AuthenticationSchemeProvider.GetDefaultChallengeSchemeAsync();
        if (scheme == null)
        {
            throw new Exception("Failed to obtain default challenge scheme");
        }

        var options = OptionsMonitor.Get(scheme.Name);
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
