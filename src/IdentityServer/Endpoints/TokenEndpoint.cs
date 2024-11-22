// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using System.IO;
using Duende.IdentityServer.Configuration;
using System.Linq;
using Duende.IdentityServer.Licensing;
using Duende.IdentityServer.Licensing.v2;

namespace Duende.IdentityServer.Endpoints;

/// <summary>
/// The token endpoint
/// </summary>
/// <seealso cref="IEndpointHandler" />
internal class TokenEndpoint : IEndpointHandler
{
    private readonly IdentityServerOptions _identityServerOptions;
    private readonly IClientSecretValidator _clientValidator;
    private readonly ITokenRequestValidator _requestValidator;
    private readonly ITokenResponseGenerator _responseGenerator;
    private readonly IProtocolRequestCounter _protocolRequestCounter;
    private readonly IEventService _events;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenEndpoint" /> class.
    /// </summary>
    /// <param name="identityServerOptions"></param>
    /// <param name="clientValidator">The client validator.</param>
    /// <param name="requestValidator">The request validator.</param>
    /// <param name="responseGenerator">The response generator.</param>
    /// <param name="protocolRequestCounter"></param>
    /// <param name="events">The events.</param>
    /// <param name="logger">The logger.</param>
    public TokenEndpoint(
        IdentityServerOptions identityServerOptions,
        IClientSecretValidator clientValidator, 
        ITokenRequestValidator requestValidator, 
        ITokenResponseGenerator responseGenerator,
        IProtocolRequestCounter protocolRequestCounter,
        IEventService events, 
        ILogger<TokenEndpoint> logger)
    {
        _identityServerOptions = identityServerOptions;
        _clientValidator = clientValidator;
        _requestValidator = requestValidator;
        _responseGenerator = responseGenerator;
        _protocolRequestCounter = protocolRequestCounter;
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
        using var activity = Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.Token + "Endpoint");
        
        _logger.LogTrace("Processing token request.");

        // validate HTTP
        if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.HasApplicationFormContentType())
        {
            _logger.LogWarning("Invalid HTTP request for token endpoint");
            return Error(OidcConstants.TokenErrors.InvalidRequest);
        }

        try
        {
            return await ProcessTokenRequestAsync(context);
        }
        catch(InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Invalid HTTP request for token endpoint");
            return Error(OidcConstants.TokenErrors.InvalidRequest);
        }
    }

    private async Task<IEndpointResult> ProcessTokenRequestAsync(HttpContext context)
    {
        _logger.LogDebug("Start token request.");

        // validate client
        var clientResult = await _clientValidator.ValidateAsync(context);
        if (clientResult.IsError)
        {
            var errorMsg = clientResult.Error ?? OidcConstants.TokenErrors.InvalidClient;
            Telemetry.Metrics.TokenIssuedFailure(clientResult.Client?.ClientId, null, null, errorMsg);
            return Error(errorMsg);
        }

        // validate request
        var form = (await context.Request.ReadFormAsync()).AsNameValueCollection();
        _logger.LogTrace("Calling into token request validator: {type}", _requestValidator.GetType().FullName);

        var requestContext = new TokenRequestValidationContext
        {
            RequestParameters = form,
            ClientValidationResult = clientResult,
        };
        
        var error = await TryReadProofTokens(context, requestContext);
        if (error != null)
        {
            Telemetry.Metrics.TokenIssuedFailure(clientResult.Client.ClientId, null, null, error.Response.Error);
            return error;
        }

        var requestResult = await _requestValidator.ValidateRequestAsync(requestContext);
        if (requestResult.IsError)
        {
            await _events.RaiseAsync(new TokenIssuedFailureEvent(requestResult));
            Telemetry.Metrics.TokenIssuedFailure(
                clientResult.Client.ClientId, requestResult.ValidatedRequest?.GrantType, null, requestResult.Error);
            var err = Error(requestResult.Error, requestResult.ErrorDescription, requestResult.CustomResponse);
            err.Response.DPoPNonce = requestResult.DPoPNonce;
            return err;
        }

        // create response
        _logger.LogTrace("Calling into token request response generator: {type}", _responseGenerator.GetType().FullName);
        var response = await _responseGenerator.ProcessAsync(requestResult);

        await _events.RaiseAsync(new TokenIssuedSuccessEvent(response, requestResult));
        Telemetry.Metrics.TokenIssued(clientResult.Client.ClientId, requestResult.ValidatedRequest.GrantType, null);
        LogTokens(response, requestResult);
        _protocolRequestCounter.Increment();
        
        // return result
        _logger.LogDebug("Token request success.");
        return new TokenResult(response);
    }

    private async Task<TokenErrorResult> TryReadProofTokens(HttpContext context, TokenRequestValidationContext tokenRequest)
    {
        // mTLS cert
        tokenRequest.ClientCertificate = await context.Connection.GetClientCertificateAsync();

        // DPoP header value
        if (context.Request.Headers.TryGetValue(OidcConstants.HttpHeaders.DPoP, out var dpopHeader))
        {
            if (dpopHeader.Count() > 1)
            {
                _logger.LogDebug("Too many DPoP headers provided.");
                return Error(OidcConstants.TokenErrors.InvalidDPoPProof, "Too many DPoP headers provided.");
            }

            tokenRequest.DPoPProofToken = dpopHeader.First();
        }

        return null;
    }

    private TokenErrorResult Error(string error, string errorDescription = null, Dictionary<string, object> custom = null)
    {
        var response = new TokenErrorResponse
        {
            Error = error,
            ErrorDescription = errorDescription,
            Custom = custom
        };

        return new TokenErrorResult(response);
    }

    private void LogTokens(TokenResponse response, TokenRequestValidationResult requestResult)
    {
        var clientId = $"{requestResult.ValidatedRequest.Client.ClientId} ({requestResult.ValidatedRequest.Client?.ClientName ?? "no name set"})";
        var subjectId = requestResult.ValidatedRequest.Subject?.GetSubjectId() ?? "no subject";

        if (response.IdentityToken != null)
        {
            _logger.LogTrace("Identity token issued for {clientId} / {subjectId}: {token}", clientId, subjectId, response.IdentityToken);
        }
        if (response.RefreshToken != null)
        {
            _logger.LogTrace("Refresh token issued for {clientId} / {subjectId}: {token}", clientId, subjectId, response.RefreshToken);
        }
        if (response.AccessToken != null)
        {
            _logger.LogTrace("Access token issued for {clientId} / {subjectId}: {token}", clientId, subjectId, response.AccessToken);
        }
    }
}