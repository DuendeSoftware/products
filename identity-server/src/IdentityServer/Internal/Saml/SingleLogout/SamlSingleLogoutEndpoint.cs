// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleLogout.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.SingleLogout;

internal class SamlSingleLogoutEndpoint(
    SamlLogoutRequestExtractor extractor,
    SamlLogoutRequestProcessor processor,
    LogoutResponseBuilder responseBuilder,
    ILogger<SamlSingleLogoutEndpoint> logger) : IEndpointHandler
{
    public async Task<IEndpointResult?> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("SamlSingleLogoutEndpoint");

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsPost(context.Request.Method))
        {
            return new StatusCodeResult(System.Net.HttpStatusCode.MethodNotAllowed);
        }

        // Extract the SAML logout request from query string (GET/Redirect) or form (POST)
        var logoutRequest = await extractor.ExtractAsync(context);

        return await ProcessLogoutRequest(logoutRequest, context.RequestAborted);
    }

    internal async Task<IEndpointResult> ProcessLogoutRequest(SamlLogoutRequest logoutRequest, CancellationToken ct = default)
    {
        logger.ReceivedLogoutRequest(LogLevel.Debug, logoutRequest.LogoutRequest.Issuer, logoutRequest.LogoutRequest.Id, logoutRequest.LogoutRequest.SessionIndex);

        var result = await processor.ProcessAsync(logoutRequest, ct);

        if (!result.Success)
        {
            var error = result.Error;
            return error.Type switch
            {
                SamlRequestErrorType.Validation => HandleValidationError(error),
                SamlRequestErrorType.Protocol => await HandleProtocolError(error),
                _ => throw new InvalidOperationException($"Unexpected error type: {error.Type}")
            };
        }

        var success = result.Value;
        logger.SuccessfullyProcessedLogoutRequest(LogLevel.Information, logoutRequest.LogoutRequest.Id, logoutRequest.LogoutRequest.SessionIndex);

        return success.Result;
    }

    private ValidationProblemResult HandleValidationError(SamlRequestError<SamlLogoutRequest> error)
    {
        logger.SamlLogoutValidationError(LogLevel.Information, error.ValidationMessage!);
        return new ValidationProblemResult(error.ValidationMessage!);
    }

    private async Task<LogoutResponse> HandleProtocolError(SamlRequestError<SamlLogoutRequest> error)
    {
        var protocolError = error.ProtocolError!;
        logger.SamlLogoutProtocolError(LogLevel.Information,
            protocolError.Error.StatusCode,
            protocolError.Error.Message);

        return await responseBuilder.BuildErrorResponseAsync(
            protocolError.Request,
            protocolError.ServiceProvider,
            protocolError.Error);
    }
}
