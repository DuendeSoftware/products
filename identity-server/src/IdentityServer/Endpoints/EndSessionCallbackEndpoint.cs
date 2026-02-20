// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Endpoints;

internal class EndSessionCallbackEndpoint : IEndpointHandler
{
    private readonly IEndSessionRequestValidator _endSessionRequestValidator;
    private readonly ILogger _logger;

    public EndSessionCallbackEndpoint(
        IEndSessionRequestValidator endSessionRequestValidator,
        ILogger<EndSessionCallbackEndpoint> logger)
    {
        _endSessionRequestValidator = endSessionRequestValidator;
        _logger = logger;
    }

    public async Task<IEndpointResult> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.EndSession + "CallbackEndpoint");

        if (!HttpMethods.IsGet(context.Request.Method))
        {
            _logger.LogWarning("Invalid HTTP method for end session callback endpoint.");
            return new StatusCodeResult(HttpStatusCode.MethodNotAllowed);
        }

        _logger.LogDebug("Processing signout callback request");

        var parameters = context.Request.Query.AsNameValueCollection();
        var result = await _endSessionRequestValidator.ValidateCallbackAsync(parameters, context.RequestAborted);

        if (!result.IsError)
        {
            _logger.LogInformation("Successful signout callback.");
        }
        else
        {
            _logger.LogError("Error validating signout callback: {error}", result.Error);
        }

        return new EndSessionCallbackResult(result);
    }
}
