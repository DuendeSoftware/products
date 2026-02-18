// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin;

internal class SamlSigninCallbackEndpoint(SamlResponseBuilder responseBuilder, SamlSigninCallbackRequestProcessor samlSigninCallbackRequestProcessor, ILogger<SamlSigninEndpoint> logger) : IEndpointHandler
{
    public async Task<IEndpointResult?> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("SamlSigninCallbackEndpoint");

        if (!HttpMethods.IsGet(context.Request.Method))
        {
            return new StatusCodeResult(System.Net.HttpStatusCode.MethodNotAllowed);
        }

        return await Process(context.RequestAborted);
    }

    internal async Task<IEndpointResult> Process(CancellationToken ct)
    {
        logger.StartSamlSigninCallbackRequest(LogLevel.Debug);

        var result = await samlSigninCallbackRequestProcessor.ProcessAsync(ct);

        if (!result.Success)
        {
            var error = result.Error;
            return error.Type switch
            {
                SamlRequestErrorType.Validation =>
                    new ValidationProblemResult(error.ValidationMessage!),

                SamlRequestErrorType.Protocol =>
                    responseBuilder.BuildErrorResponse(
                        error.ProtocolError!.ServiceProvider,
                        error.ProtocolError.Request,
                        error.ProtocolError.Error),

                _ => throw new InvalidOperationException($"Unexpected error type: {error.Type}")
            };
        }

        return result.Value.SuccessType switch
        {
            SamlSigninSuccessType.Redirect => new RedirectResult(result.Value.RedirectUri),
            SamlSigninSuccessType.Response => result.Value.SamlResponse,
            _ => throw new InvalidOperationException($"Unexpected success type: {result.Value.SuccessType}")
        };
    }
}
