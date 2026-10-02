// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Endpoints;

internal class BackchannelAuthenticationEndpoint : IEndpointHandler
{
    private readonly IClientSecretValidator _clientValidator;
    private readonly IBackchannelAuthenticationRequestValidator _requestValidator;
    private readonly IBackchannelAuthenticationResponseGenerator _responseGenerator;
    private readonly IEventService _events;
    private readonly ILogger<BackchannelAuthenticationEndpoint> _logger;
    private readonly IdentityServerOptions _options;

    public BackchannelAuthenticationEndpoint(
        IClientSecretValidator clientValidator,
        IBackchannelAuthenticationRequestValidator requestValidator,
        IBackchannelAuthenticationResponseGenerator responseGenerator,
        IEventService events,
        ILogger<BackchannelAuthenticationEndpoint> logger,
        IdentityServerOptions options)
    {
        _clientValidator = clientValidator;
        _requestValidator = requestValidator;
        _responseGenerator = responseGenerator;
        _events = events;
        _logger = logger;
        _options = options;
    }

    public async Task<IEndpointResult> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.BackchannelAuthentication + "Endpoint");

        _logger.ProcessingBackchannelAuthenticationRequest();

        // validate HTTP
        if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.HasApplicationFormContentType())
        {
            _logger.InvalidHTTPRequestForBackchannelAuthenticationEndpoint();
            return Error(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest);
        }

        try
        {
            return await ProcessAuthenticationRequestAsync(context);
        }
        catch (InvalidDataException ex)
        {
            _logger.InvalidHTTPRequestForBackchannelAuthenticationEndpointBackchannelAuthenticationEndpoint(ex);
            return Error(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest);
        }
    }

    private async Task<IEndpointResult> ProcessAuthenticationRequestAsync(HttpContext context)
    {
        _logger.StartBackchannelAuthenticationRequest();

        // validate client
        var clientResult = await _clientValidator.ValidateAsync(context, context.RequestAborted);
        if (clientResult.IsError)
        {
            var error = clientResult.Error ?? OidcConstants.BackchannelAuthenticationRequestErrors.InvalidClient;
            _logger.ClientValidationFailedForBackchannelAuthenticationEndpoint(error);
            Telemetry.Metrics.BackChannelAuthenticationFailure(
                clientResult.Client?.ClientId, error);
            return Error(error);
        }

        // validate request
        var form = (await context.Request.ReadFormAsync(context.RequestAborted)).AsNameValueCollection();
        _logger.CallingIntoBackchannelAuthenticationRequestValidator(_requestValidator.GetType().FullName);
        var requestResult = await _requestValidator.ValidateRequestAsync(form, clientResult, context.RequestAborted);

        if (requestResult.IsError)
        {
            await _events.RaiseAsync(new BackchannelAuthenticationFailureEvent(requestResult), context.RequestAborted);
            Telemetry.Metrics.BackChannelAuthenticationFailure(clientResult.Client?.ClientId, requestResult.Error);
            return Error(requestResult.Error, requestResult.ErrorDescription);
        }

        // create response
        _logger.CallingIntoBackchannelAuthenticationRequestResponseGenerator(_responseGenerator.GetType().FullName);
        var response = await _responseGenerator.ProcessAsync(requestResult, context.RequestAborted);

        await _events.RaiseAsync(new BackchannelAuthenticationSuccessEvent(requestResult), context.RequestAborted);
        Telemetry.Metrics.BackChannelAuthentication(clientResult.Client.ClientId);
        LogResponse(response, requestResult);

        // return result
        _logger.BackchannelAuthenticationRequestSuccess();
        return new BackchannelAuthenticationResult(response);
    }

    private void LogResponse(BackchannelAuthenticationResponse response, BackchannelAuthenticationRequestValidationResult requestResult)
    {
        var subjectId = requestResult.ValidatedRequest.Subject.GetSubjectId();
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.BackchannelAuthenticationResponseForSubject(response, subjectId);
        }
    }

    private static BackchannelAuthenticationResult Error(string error, string errorDescription = null) => new BackchannelAuthenticationResult(new BackchannelAuthenticationResponse(error, errorDescription));
}
