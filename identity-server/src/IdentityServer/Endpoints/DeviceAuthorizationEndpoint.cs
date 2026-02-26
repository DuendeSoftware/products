// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
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

/// <summary>
/// The device authorization endpoint
/// </summary>
/// <seealso cref="IEndpointHandler" />
internal class DeviceAuthorizationEndpoint : IEndpointHandler
{
    private readonly IClientSecretValidator _clientValidator;
    private readonly IDeviceAuthorizationRequestValidator _requestValidator;
    private readonly IDeviceAuthorizationResponseGenerator _responseGenerator;
    private readonly IEventService _events;
    private readonly IServerUrls _urls;
    private readonly ILogger<DeviceAuthorizationEndpoint> _logger;

    public DeviceAuthorizationEndpoint(
        IClientSecretValidator clientValidator,
        IDeviceAuthorizationRequestValidator requestValidator,
        IDeviceAuthorizationResponseGenerator responseGenerator,
        IEventService events,
        IServerUrls urls,
        ILogger<DeviceAuthorizationEndpoint> logger)
    {
        _clientValidator = clientValidator;
        _requestValidator = requestValidator;
        _responseGenerator = responseGenerator;
        _events = events;
        _urls = urls;
        _logger = logger;
    }

    /// <summary>
    /// Processes the request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns></returns>
    /// <exception cref="System.NotImplementedException"></exception>
    public async Task<IEndpointResult> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.DeviceAuthorization + "Endpoint");

        _logger.LogTrace("Processing device authorize request.");

        // validate HTTP
        if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.HasApplicationFormContentType())
        {
            _logger.LogWarning("Invalid HTTP request for device authorize endpoint");
            return Error(OidcConstants.TokenErrors.InvalidRequest);
        }

        try
        {
            return await ProcessDeviceAuthorizationRequestAsync(context);
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Invalid HTTP request for device endpoint");
            return Error(OidcConstants.TokenErrors.InvalidRequest);
        }
    }

    private async Task<IEndpointResult> ProcessDeviceAuthorizationRequestAsync(HttpContext context)
    {
        _logger.LogDebug("Start device authorize request.");

        // validate client
        var clientResult = await _clientValidator.ValidateAsync(context, context.RequestAborted);
        if (clientResult.IsError)
        {
            var error = clientResult.Error ?? OidcConstants.TokenErrors.InvalidClient;
            Telemetry.Metrics.DeviceAuthenticationFailure(clientResult.Client?.ClientId, error);
            return Error(error);
        }

        // validate request
        var form = (await context.Request.ReadFormAsync(context.RequestAborted)).AsNameValueCollection();
        var requestResult = await _requestValidator.ValidateAsync(form, clientResult, context.RequestAborted);

        if (requestResult.IsError)
        {
            await _events.RaiseAsync(new DeviceAuthorizationFailureEvent(requestResult), context.RequestAborted);
            Telemetry.Metrics.DeviceAuthenticationFailure(clientResult.Client.ClientId, requestResult.Error);
            return Error(requestResult.Error, requestResult.ErrorDescription);
        }

        // create response
        _logger.LogTrace("Calling into device authorize response generator: {type}", _responseGenerator.GetType().FullName);
        var response = await _responseGenerator.ProcessAsync(requestResult, _urls.BaseUrl, context.RequestAborted);

        await _events.RaiseAsync(new DeviceAuthorizationSuccessEvent(response, requestResult), context.RequestAborted);
        Telemetry.Metrics.DeviceAuthentication(clientResult.Client.ClientId);

        // return result
        _logger.LogDebug("Device authorize request success.");
        return new DeviceAuthorizationResult(response);
    }

    private TokenErrorResult Error(string error, string errorDescription = null, Dictionary<string, object> custom = null)
    {
        var response = new TokenErrorResponse
        {
            Error = error,
            ErrorDescription = errorDescription,
            Custom = custom
        };

        _logger.LogError("Device authorization error: {error}:{errorDescriptions}", error, errorDescription ?? "-no message-");

        return new TokenErrorResult(response);
    }

    private void LogResponse(DeviceAuthorizationResponse response, DeviceAuthorizationRequestValidationResult requestResult)
    {
        var clientId = $"{requestResult.ValidatedRequest.Client.ClientId} ({requestResult.ValidatedRequest.Client?.ClientName ?? "no name set"})";

        if (response.DeviceCode != null)
        {
            _logger.LogTrace("Device code issued for {clientId}: {deviceCode}", clientId, response.DeviceCode);
        }
        if (response.UserCode != null)
        {
            _logger.LogTrace("User code issued for {clientId}: {userCode}", clientId, response.UserCode);
        }
        if (response.VerificationUri != null)
        {
            _logger.LogTrace("Verification URI issued for {clientId}: {verificationUri}", clientId, response.VerificationUri);
        }
        if (response.VerificationUriComplete != null)
        {
            _logger.LogTrace("Verification URI (Complete) issued for {clientId}: {verificationUriComplete}", clientId, response.VerificationUriComplete);
        }
    }
}
