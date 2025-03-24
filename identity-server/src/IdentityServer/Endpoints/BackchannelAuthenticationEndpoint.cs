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

        _logger.LogTrace("Processing backchannel authentication request.");

        // validate HTTP
        if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.HasApplicationFormContentType())
        {
            _logger.LogWarning("Invalid HTTP request for backchannel authentication endpoint");
            return Error(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest);
        }

        try
        {
            return await ProcessAuthenticationRequestAsync(context);
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Invalid HTTP request for backchannel authentication endpoint");
            return Error(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest);
        }
    }

    private async Task<IEndpointResult> ProcessAuthenticationRequestAsync(HttpContext context)
    {
        _logger.LogDebug("Start backchannel authentication request.");

        // validate client
        var clientResult = await _clientValidator.ValidateAsync(context);
        if (clientResult.IsError)
        {
            var error = clientResult.Error ?? OidcConstants.BackchannelAuthenticationRequestErrors.InvalidClient;
            Telemetry.Metrics.BackChannelAuthenticationFailure(
                clientResult.Client?.ClientId, error);
            return Error(error);
        }

        // validate request
        var form = (await context.Request.ReadFormAsync()).AsNameValueCollection();
        _logger.LogTrace("Calling into backchannel authentication request validator: {type}", _requestValidator.GetType().FullName);
        var requestResult = await _requestValidator.ValidateRequestAsync(form, clientResult);

        if (requestResult.IsError)
        {
            await _events.RaiseAsync(new BackchannelAuthenticationFailureEvent(requestResult));
            Telemetry.Metrics.BackChannelAuthenticationFailure(clientResult.Client?.ClientId, requestResult.Error);
            return Error(requestResult.Error, requestResult.ErrorDescription);
        }

        // create response
        _logger.LogTrace("Calling into backchannel authentication request response generator: {type}", _responseGenerator.GetType().FullName);
        var response = await _responseGenerator.ProcessAsync(requestResult);

        await _events.RaiseAsync(new BackchannelAuthenticationSuccessEvent(requestResult));
        Telemetry.Metrics.BackChannelAuthentication(clientResult.Client.ClientId);
        LogResponse(response, requestResult);

        // return result
        _logger.LogDebug("Backchannel authentication request success.");
        return new BackchannelAuthenticationResult(response);
    }

    private void LogResponse(BackchannelAuthenticationResponse response, BackchannelAuthenticationRequestValidationResult requestResult)
    {
        _logger.LogTrace("BackchannelAuthenticationResponse: {@response} for subject {subjectId}", response, requestResult.ValidatedRequest.Subject.GetSubjectId());
    }

    private BackchannelAuthenticationResult Error(string error, string errorDescription = null)
    {
        return new BackchannelAuthenticationResult(new BackchannelAuthenticationResponse(error, errorDescription));
    }
}
