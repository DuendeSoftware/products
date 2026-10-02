// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using System.Net;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Endpoints;

internal class EndSessionEndpoint : IEndpointHandler
{
    private readonly IEndSessionRequestValidator _endSessionRequestValidator;

    private readonly ILogger _logger;

    private readonly IUserSession _userSession;

    public EndSessionEndpoint(
        IEndSessionRequestValidator endSessionRequestValidator,
        IUserSession userSession,
        ILogger<EndSessionEndpoint> logger)
    {
        _endSessionRequestValidator = endSessionRequestValidator;
        _userSession = userSession;
        _logger = logger;
    }

    public async Task<IEndpointResult> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.EndSession + "Endpoint");

        try
        {
            return await ProcessEndSessionAsync(context);
        }
        catch (InvalidDataException ex)
        {
            _logger.InvalidHTTPRequestForEndSessionEndpoint(ex);
            return new StatusCodeResult(HttpStatusCode.BadRequest);
        }
    }


    private async Task<IEndpointResult> ProcessEndSessionAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.EndSession + "Endpoint");

        NameValueCollection parameters;
        if (HttpMethods.IsGet(context.Request.Method))
        {
            parameters = context.Request.Query.AsNameValueCollection();
        }
        else if (HttpMethods.IsPost(context.Request.Method))
        {
            parameters = (await context.Request.ReadFormAsync(context.RequestAborted)).AsNameValueCollection();
        }
        else
        {
            _logger.InvalidHTTPMethodForEndSessionEndpoint();
            return new StatusCodeResult(HttpStatusCode.MethodNotAllowed);
        }

        var user = await _userSession.GetUserAsync(context.RequestAborted);
        var subjectId = user?.GetSubjectId() ?? "anonymous";

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.ProcessingSignoutRequestFor(subjectId);
        }

        var result = await _endSessionRequestValidator.ValidateAsync(parameters, user, context.RequestAborted);

        if (result.IsError)
        {
            _logger.ErrorProcessingEndSessionRequest(result.Error);
        }
        else
        {
            _logger.SuccessValidatingEndSessionRequestFrom(result.ValidatedRequest?.Client?.ClientId);
        }

        return new EndSessionResult(result);
    }
}
