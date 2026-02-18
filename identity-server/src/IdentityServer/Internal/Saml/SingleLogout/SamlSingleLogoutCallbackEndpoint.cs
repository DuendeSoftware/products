// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Net;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.SingleLogout;

/// <summary>
/// Endpoint for completing SAML Single Logout and sending the LogoutResponse back to the initiating Service Provider.
/// This is called after the user completes logout and all front-channel logout notifications have been sent.
/// </summary>
internal class SamlSingleLogoutCallbackEndpoint(
    SamlLogoutCallbackProcessor processor,
    ILogger<SamlSingleLogoutCallbackEndpoint> logger) : IEndpointHandler
{
    public async Task<IEndpointResult?> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("SamlSingleLogoutCallbackEndpoint");

        logger.ProcessingSamlLogoutCallbackRequest(LogLevel.Debug);

        var logoutId = context.Request.Query["logoutId"].ToString();
        if (string.IsNullOrWhiteSpace(logoutId))
        {
            logger.MissingLogoutIdParameter(LogLevel.Warning);
            return new StatusCodeResult(HttpStatusCode.BadRequest);
        }

        var result = await processor.ProcessAsync(logoutId, context.RequestAborted);

        if (!result.Success)
        {
            logger.ErrorProcessingLogoutCallback(LogLevel.Error, result.Error.Message);
            return new StatusCodeResult(HttpStatusCode.BadRequest);
        }

        logger.SuccessfullyProcessedLogoutCallback(LogLevel.Information);
        return result.Value;
    }
}
