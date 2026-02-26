// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin;

internal class SamlSigninEndpoint(
    SamlSigninRequestExtractor extractor,
    SamlSigninRequestProcessor signinRequestProcessor,
    ILogger<SamlSigninEndpoint> logger,
    SamlResponseBuilder responseBuilder) : IEndpointHandler
{
    public async Task<IEndpointResult?> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("SamlSigninEndpoint");

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsPost(context.Request.Method))
        {
            return new StatusCodeResult(System.Net.HttpStatusCode.MethodNotAllowed);
        }

        // Extract the SAML request from query string (GET/Redirect) or form (POST)
        var signinRequest = await extractor.ExtractAsync(context);

        return await ProcessSpInitiatedSignin(signinRequest, context.RequestAborted);
    }

    internal async Task<IEndpointResult> ProcessSpInitiatedSignin(
        SamlSigninRequest signinRequest,
        Ct ct = default)
    {
        logger.StartSamlSigninRequest(LogLevel.Debug);

        var result = await signinRequestProcessor.ProcessAsync(signinRequest, ct);

        if (!result.Success)
        {
            var error = result.Error;
            return error.Type switch
            {
                SamlRequestErrorType.Validation => HandleValidationError(error),
                SamlRequestErrorType.Protocol => HandleProtocolError(error),
                _ => throw new InvalidOperationException($"Unexpected error type: {error.Type}")
            };
        }

        var success = result.Value;
        logger.SamlSigninSuccess(LogLevel.Debug, success.RedirectUri);
        return new RedirectResult(success.RedirectUri);
    }

    private ValidationProblemResult HandleValidationError(SamlRequestError<SamlSigninRequest> error)
    {
        logger.SamlSigninValidationError(LogLevel.Information, error.ValidationMessage!);
        return new ValidationProblemResult(error.ValidationMessage!);
    }

    private SamlErrorResponse HandleProtocolError(SamlRequestError<SamlSigninRequest> error)
    {
        var protocolError = error.ProtocolError!;
        logger.SamlSigninProtocolError(
            LogLevel.Information,
            protocolError.Error.StatusCode,
            protocolError.Error.Message);

        return responseBuilder.BuildErrorResponse(
            protocolError.ServiceProvider,
            protocolError.Request,
            protocolError.Error);
    }
}
