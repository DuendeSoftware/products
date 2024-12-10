// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Logging.Models;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Validation;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Specialized;
using System.Net;
using System.Threading.Tasks;
using Duende.IdentityServer.Licensing.v2;

namespace Duende.IdentityServer.Endpoints;
internal class PushedAuthorizationEndpoint : IEndpointHandler
{
    private readonly IClientSecretValidator _clientValidator;
    private readonly IPushedAuthorizationRequestValidator _parValidator;
    private readonly IPushedAuthorizationResponseGenerator _responseGenerator;
    private readonly LicenseUsageTracker _features;
    private readonly IdentityServerOptions _options;
    private readonly ILogger<PushedAuthorizationEndpoint> _logger;

    public PushedAuthorizationEndpoint(
        IClientSecretValidator clientValidator,
        IPushedAuthorizationRequestValidator parValidator,
        IPushedAuthorizationResponseGenerator responseGenerator,
        LicenseUsageTracker features,
        IdentityServerOptions options,
        ILogger<PushedAuthorizationEndpoint> logger
        )
    {
        _clientValidator = clientValidator;
        _parValidator = parValidator;
        _responseGenerator = responseGenerator;
        _features = features;
        _options = options;
        _logger = logger;
    }

    public async Task<IEndpointResult> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.PushedAuthorization);

        _logger.LogDebug("Start pushed authorization request");

        _features.FeatureUsed(LicenseFeature.PAR);

        NameValueCollection values;
        IFormCollection form;
        if(HttpMethods.IsPost(context.Request.Method))
        {
            form = await context.Request.ReadFormAsync();
            values = form.AsNameValueCollection();
        }
        else
        {
            return new StatusCodeResult(HttpStatusCode.MethodNotAllowed);
        }

        // Authenticate Client
        var client = await _clientValidator.ValidateAsync(context);
        if(client.IsError)
        {
            return CreateErrorResult(
                logMessage: "Client secret validation failed",
                request: null,
                client.Error,
                client.ErrorDescription);
        }

        // Perform validations specific to PAR, as well as validation of the pushed parameters
        var parValidationResult = await _parValidator.ValidateAsync(new PushedAuthorizationRequestValidationContext(values, client.Client));
        if (parValidationResult.IsError)
        {
            return CreateErrorResult(
                logMessage: "Pushed authorization validation failed",
                request: parValidationResult.ValidatedRequest,
                parValidationResult.Error,
                parValidationResult.ErrorDescription);
        }

        var response = await _responseGenerator.CreateResponseAsync(parValidationResult.ValidatedRequest);

        switch(response)
        {
            case PushedAuthorizationSuccess success:
                Telemetry.Metrics.PushedAuthorizationRequest(parValidationResult.ValidatedRequest.Client.ClientId);
                return new PushedAuthorizationResult(success);
            case PushedAuthorizationFailure fail:
                Telemetry.Metrics.PushedAuthorizationRequestFailure(parValidationResult.ValidatedRequest.ClientId, fail.Error);
                return new PushedAuthorizationErrorResult(fail);
            default:
                throw new Exception("Unexpected pushed authorization response. The result of the pushed authorization response generator should be either a PushedAuthorizationSuccess or PushedAuthorizationFailure.");
        }
    }

    private PushedAuthorizationErrorResult CreateErrorResult(
        string logMessage,
        ValidatedPushedAuthorizationRequest request = null,
        string error = OidcConstants.AuthorizeErrors.ServerError,
        string errorDescription = null,
        bool logError = true)
    {
        if (logError)
        {
            _logger.LogError(logMessage);
        }

        if (request != null)
        {
            var details = new AuthorizeRequestValidationLog(request, _options.Logging.PushedAuthorizationSensitiveValuesFilter);
            _logger.LogInformation("{@validationDetails}", details);
        }

        Telemetry.Metrics.PushedAuthorizationRequestFailure(request?.Client.ClientId, logMessage);

        return new PushedAuthorizationErrorResult(new PushedAuthorizationFailure
        {
            Error = error,
            ErrorDescription = errorDescription,
        });
    }
}

