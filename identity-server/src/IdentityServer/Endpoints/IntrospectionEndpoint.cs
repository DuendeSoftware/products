// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Endpoints;

/// <summary>
/// Introspection endpoint
/// </summary>
/// <seealso cref="IEndpointHandler" />
internal class IntrospectionEndpoint : IEndpointHandler
{
    private readonly IIntrospectionResponseGenerator _responseGenerator;
    private readonly IEventService _events;
    private readonly ILogger _logger;
    private readonly IIntrospectionRequestValidator _requestValidator;
    private readonly IApiSecretValidator _apiSecretValidator;
    private readonly IClientSecretValidator _clientValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntrospectionEndpoint" /> class.
    /// </summary>
    /// <param name="apiSecretValidator">The API secret validator.</param>
    /// <param name="clientValidator"></param>
    /// <param name="requestValidator">The request validator.</param>
    /// <param name="responseGenerator">The generator.</param>
    /// <param name="events">The events.</param>
    /// <param name="logger">The logger.</param>
    public IntrospectionEndpoint(
        IApiSecretValidator apiSecretValidator,
        IClientSecretValidator clientValidator,
        IIntrospectionRequestValidator requestValidator,
        IIntrospectionResponseGenerator responseGenerator,
        IEventService events,
        ILogger<IntrospectionEndpoint> logger)
    {
        _apiSecretValidator = apiSecretValidator;
        _clientValidator = clientValidator;
        _requestValidator = requestValidator;
        _responseGenerator = responseGenerator;
        _events = events;
        _logger = logger;
    }

    /// <summary>
    /// Processes the request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns></returns>
    public async Task<IEndpointResult> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.Introspection + "Endpoint");

        _logger.LogTrace("Processing introspection request.");

        // validate HTTP
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            _logger.LogWarning("Introspection endpoint only supports POST requests");
            return new StatusCodeResult(HttpStatusCode.MethodNotAllowed);
        }

        if (!context.Request.HasApplicationFormContentType())
        {
            _logger.LogWarning("Invalid media type for introspection endpoint");
            return new StatusCodeResult(HttpStatusCode.UnsupportedMediaType);
        }

        try
        {
            return await ProcessIntrospectionRequestAsync(context);
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Invalid HTTP request for introspection endpoint");
            return new StatusCodeResult(HttpStatusCode.BadRequest);
        }
    }

    private async Task<IEndpointResult> ProcessIntrospectionRequestAsync(HttpContext context)
    {
        _logger.LogDebug("Starting introspection request.");

        // caller validation
        ClientSecretValidationResult clientResult;

        ApiResource api = null;
        Client client = null;

        var apiResult = await _apiSecretValidator.ValidateAsync(context);
        if (apiResult.IsError)
        {
            clientResult = await _clientValidator.ValidateAsync(context);
            if (clientResult.IsError)
            {
                _logger.LogError("Unauthorized call introspection endpoint. aborting.");
                return new StatusCodeResult(HttpStatusCode.Unauthorized);
            }
            else
            {
                client = clientResult.Client;
                _logger.LogDebug("Client making introspection request: {clientId}", client.ClientId);
            }
        }
        else
        {
            api = apiResult.Resource;
            _logger.LogDebug("ApiResource making introspection request: {apiId}", api.Name);
        }

        var callerName = api?.Name ?? client.ClientId;

        var body = await context.Request.ReadFormAsync();
        if (body == null)
        {
            _logger.LogError("Malformed request body. aborting.");
            const string error = "Malformed request body";
            await _events.RaiseAsync(new TokenIntrospectionFailureEvent(callerName, error));
            Telemetry.Metrics.IntrospectionFailure(callerName, error);
            return new StatusCodeResult(HttpStatusCode.BadRequest);
        }

        // request validation
        _logger.LogTrace("Calling into introspection request validator: {type}", _requestValidator.GetType().FullName);
        var validationRequest = new IntrospectionRequestValidationContext
        {
            Parameters = body.AsNameValueCollection(),
            Api = api,
            Client = client,
        };
        var validationResult = await _requestValidator.ValidateAsync(validationRequest);
        if (validationResult.IsError)
        {
            LogFailure(validationResult.Error, callerName);
            await _events.RaiseAsync(new TokenIntrospectionFailureEvent(callerName, validationResult.Error));
            Telemetry.Metrics.IntrospectionFailure(callerName, validationResult.Error);
            return new BadRequestResult(validationResult.Error);
        }

        // response generation
        _logger.LogTrace("Calling into introspection response generator: {type}", _responseGenerator.GetType().FullName);
        var response = await _responseGenerator.ProcessAsync(validationResult);

        // render result
        LogSuccess(validationResult.IsActive, callerName);
        return new IntrospectionResult(response);
    }

    private void LogSuccess(bool tokenActive, string callerName)
    {
        _logger.LogInformation("Success token introspection. Token active: {tokenActive}, for caller: {callerName}", tokenActive, callerName);
    }

    private void LogFailure(string error, string callerName)
    {
        _logger.LogError("Failed token introspection: {error}, for caller: {callerName}", error, callerName);
    }
}
