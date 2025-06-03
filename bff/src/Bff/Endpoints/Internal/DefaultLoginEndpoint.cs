// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Otel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Endpoints.Internal;

/// <summary>
/// Service for handling login requests
/// </summary>
internal class DefaultLoginEndpoint(
    SelectedFrontend selectedFrontend,
    IAuthenticationSchemeProvider authenticationSchemeProvider,
    IOptionsMonitor<OpenIdConnectOptions> openIdConnectOptionsMonitor,
    IOptions<BffOptions> bffOptions,
    IReturnUrlValidator returnUrlValidator,
    ILogger<DefaultLoginEndpoint> logger)
    : ILoginEndpoint
{
    /// <inheritdoc />
    public async Task ProcessRequestAsync(HttpContext context, CT ct = default)
    {
        logger.LogDebug("Processing login request");

        context.CheckForBffMiddleware(bffOptions.Value);

        var returnUrl = context.Request.Query[Constants.RequestParameters.ReturnUrl].FirstOrDefault();

        var prompt = context.Request.Query[Constants.RequestParameters.Prompt].FirstOrDefault();

        var supportedPromptValues = await GetPromptValuesAsync(ct);

        if (supportedPromptValues == null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return;
        }

        if (prompt != null && !supportedPromptValues.Contains(prompt))
        {
            logger.InvalidPromptValue(LogLevel.Information, prompt.Sanitize());
            context.ReturnHttpProblem("Invalid prompt value", (Constants.RequestParameters.Prompt, [$"prompt '{prompt}' is not supported"]));
            return;
        }

        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            if (!returnUrlValidator.IsValidAsync(returnUrl))
            {
                logger.InvalidReturnUrl(LogLevel.Information, returnUrl.Sanitize());
                context.ReturnHttpProblem("Invalid return url", (Constants.RequestParameters.ReturnUrl, [$"ReturnUrl '{returnUrl}' was invalid"]));
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            returnUrl = context.Request.PathBase.HasValue
                ? context.Request.PathBase
                : "/";
        }

        var props = new AuthenticationProperties
        {
            RedirectUri = returnUrl
        };

        if (prompt != null)
        {
            props.Items.Add(Constants.BffFlags.Prompt, prompt);
        }

        logger.LogDebug("Login endpoint triggering Challenge with returnUrl {returnUrl}", returnUrl.Sanitize());

        await context.ChallengeAsync(props);
    }

    private async Task<ICollection<string>?> GetPromptValuesAsync(CT ct = default)
    {
        Scheme scheme;

        if (selectedFrontend.TryGet(out var frontEnd))
        {
            scheme = frontEnd.OidcSchemeName;
        }
        else
        {
            var defaultScheme = await authenticationSchemeProvider.GetDefaultChallengeSchemeAsync();
            if (defaultScheme == null)
            {
                throw new InvalidOperationException("DefaultScheme is null.");
            }

            scheme = Scheme.Parse(defaultScheme.Name);
        }

        var openIdConnectOptions = openIdConnectOptionsMonitor.Get(scheme);
        if (openIdConnectOptions == null)
        {
            throw new Exception("Failed to obtain OIDC options for scheme: " + scheme);
        }

        var config = openIdConnectOptions.Configuration;
        if (config == null && openIdConnectOptions.ConfigurationManager != null)
        {
            config = await openIdConnectOptions.ConfigurationManager.GetConfigurationAsync(ct);
        }

        if (config != null)
        {
            return config.PromptValuesSupported;
        }

        logger.NoOpenIdConfigurationFoundForScheme(LogLevel.Error, scheme);

        return null;
    }
}
