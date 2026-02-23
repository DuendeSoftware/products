// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin;

internal class SamlIdpInitiatedEndpoint(
    SamlIdpInitiatedRequestProcessor requestProcessor,
    ILogger<SamlIdpInitiatedEndpoint> logger) : IEndpointHandler
{
    public async Task<IEndpointResult?> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("SamlIdpInitiatedEndpoint");

        if (!HttpMethods.IsGet(context.Request.Method))
        {
            return new StatusCodeResult(System.Net.HttpStatusCode.MethodNotAllowed);
        }

        var spEntityId = context.Request.Query["spEntityId"].ToString();
        var relayState = context.Request.Query["relayState"].ToString();

        if (string.IsNullOrWhiteSpace(spEntityId))
        {
            return new ValidationProblemResult("Missing required 'spEntityId' query parameter");
        }

        return await ProcessInternalAsync(
            spEntityId,
            string.IsNullOrEmpty(relayState) ? null : relayState,
            context.RequestAborted);
    }

    internal async Task<IEndpointResult> ProcessInternalAsync(
        string spEntityId,
        string? relayState,
        CT ct = default)
    {
        logger.StartIdpInitiatedRequest(LogLevel.Debug, spEntityId);

        var result = await requestProcessor.ProcessAsync(spEntityId, relayState, ct);

        if (!result.Success)
        {
            var error = result.Error;
            logger.IdpInitiatedRequestFailed(LogLevel.Information, error.ValidationMessage!);
            return new ValidationProblemResult(error.ValidationMessage!);
        }

        var success = result.Value;
        logger.IdpInitiatedRequestSuccess(LogLevel.Debug, success.RedirectUri);
        return new RedirectResult(success.RedirectUri);
    }
}
