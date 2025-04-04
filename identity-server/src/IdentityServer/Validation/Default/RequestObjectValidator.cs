// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Logging.Models;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Validation;

internal class RequestObjectValidator : IRequestObjectValidator
{
    private readonly IJwtRequestValidator _jwtRequestValidator;
    private readonly IJwtRequestUriHttpClient _jwtRequestUriHttpClient;
    private readonly IPushedAuthorizationService _pushedAuthorizationService;
    private readonly IdentityServerOptions _options;
    private readonly ILogger<RequestObjectValidator> _logger;

    public RequestObjectValidator(
        IJwtRequestValidator jwtRequestValidator,
        IJwtRequestUriHttpClient jwtRequestUriHttpClient,
        IPushedAuthorizationService pushedAuthorizationService,
        IdentityServerOptions options,
        ILogger<RequestObjectValidator> logger)
    {
        _jwtRequestValidator = jwtRequestValidator;
        _jwtRequestUriHttpClient = jwtRequestUriHttpClient;
        _pushedAuthorizationService = pushedAuthorizationService;
        _options = options;
        _logger = logger;
    }


    public async Task<AuthorizeRequestValidationResult> LoadRequestObjectAsync(ValidatedAuthorizeRequest request)
    {
        var requestObject = request.Raw.Get(OidcConstants.AuthorizeRequest.Request);
        var requestUri = request.Raw.Get(OidcConstants.AuthorizeRequest.RequestUri);

        if (requestObject.IsPresent() && requestUri.IsPresent())
        {
            LogError("Both request and request_uri are present", request);
            return Invalid(request, description: "Only one request parameter is allowed");
        }

        // If we're on the authorize endpoint, make sure that we use PAR when it
        // is required (either globally or by this client).
        if (request.AuthorizeRequestType != AuthorizeRequestType.PushedAuthorization)
        {
            var parRequired = _options.PushedAuthorization.Required || request.Client.RequirePushedAuthorization;
            var parMissing = requestUri.IsMissing() || !requestUri.StartsWith(IdentityServerConstants.PushedAuthorizationRequestUri);
            if (parRequired && parMissing)
            {
                LogError("Pushed authorization is required", request);
                return Invalid(request, error: OidcConstants.AuthorizeErrors.InvalidRequest, "Pushed authorization is required.");
            }
        }

        if (requestUri.IsPresent())
        {
            if (IsParRequestUri(requestUri))
            {
                var validationError = await ValidatePushedAuthorizationRequest(request);
                if (validationError != null)
                {
                    return validationError;
                }

                request.AuthorizeRequestType = AuthorizeRequestType.AuthorizeWithPushedParameters;
                requestObject = LoadRequestObjectFromPushedAuthorizationRequest(request);
            }
            else if (_options.Endpoints.EnableJwtRequestUri)
            {
                // 512 is from the spec
                if (requestUri.Length > 512)
                {
                    LogError("request_uri is too long", request);
                    return Invalid(request, error: OidcConstants.AuthorizeErrors.InvalidRequestUri, description: "request_uri is too long");
                }

                var jwt = await _jwtRequestUriHttpClient.GetJwtAsync(requestUri, request.Client);
                if (jwt.IsMissing())
                {
                    LogError("no value returned from request_uri", request);
                    return Invalid(request, error: OidcConstants.AuthorizeErrors.InvalidRequestUri, description: "no value returned from request_uri");
                }

                requestObject = jwt;
            }
            else
            {
                LogError("request_uri present but config prohibits", request);
                return Invalid(request, error: OidcConstants.AuthorizeErrors.RequestUriNotSupported);
            }
        }

        // check length restrictions
        if (requestObject.IsPresent())
        {
            if (requestObject.Length >= _options.InputLengthRestrictions.Jwt)
            {
                LogError("request value is too long", request);
                return Invalid(request, error: OidcConstants.AuthorizeErrors.InvalidRequestObject, description: "Invalid request value");
            }
        }

        request.RequestObject = requestObject;
        return Valid(request);
    }

    private static bool IsParRequestUri(string requestUri) => requestUri.StartsWith(IdentityServerConstants.PushedAuthorizationRequestUri);

    private string? LoadRequestObjectFromPushedAuthorizationRequest(ValidatedAuthorizeRequest request) => request.Raw.Get(OidcConstants.AuthorizeRequest.Request);

    public async Task<AuthorizeRequestValidationResult?> ValidatePushedAuthorizationRequest(ValidatedAuthorizeRequest request)
    {
        // Check that the endpoint is still enabled at the time of validation, in case an existing PAR record 
        // is used after PAR is disabled.
        if (!_options.Endpoints.EnablePushedAuthorizationEndpoint)
        {
            {
                return Invalid(request, error: OidcConstants.AuthorizeErrors.InvalidRequestUri,
                    description: "Pushed authorization is disabled.");
            }
        }
        var pushedAuthorizationRequest = await GetPushedAuthorizationRequestAsync(request);
        if (pushedAuthorizationRequest == null)
        {
            {
                return Invalid(request, error: OidcConstants.AuthorizeErrors.InvalidRequestUri,
                    description: "invalid or reused PAR request uri");
            }
        }

        // Record the reference value, so we can know that PAR did happen
        request.PushedAuthorizationReferenceValue = GetReferenceValue(request);
        // Copy the PAR into the raw request so that validation will use the pushed parameters
        // But keep the query parameters we add that indicate that we have processed 
        // prompt and max_age, as those are not pushed
        var processedPrompt = request.Raw[Constants.ProcessedPrompt];
        var processedMaxAge = request.Raw[Constants.ProcessedMaxAge];

        request.Raw = pushedAuthorizationRequest.PushedParameters;
        if (processedPrompt != null)
        {
            request.Raw[Constants.ProcessedPrompt] = processedPrompt;
        }
        if (processedMaxAge != null)
        {
            request.Raw[Constants.ProcessedMaxAge] = processedMaxAge;
        }

        var bindingError = ValidatePushedAuthorizationBindingToClient(pushedAuthorizationRequest, request);
        if (bindingError != null)
        {
            return bindingError;
        }
        var expirationError = ValidatePushedAuthorizationExpiration(pushedAuthorizationRequest, request);
        if (expirationError != null)
        {
            return expirationError;
        }

        return null;
    }

    public AuthorizeRequestValidationResult? ValidatePushedAuthorizationBindingToClient(DeserializedPushedAuthorizationRequest pushedAuthorizationRequest, ValidatedAuthorizeRequest authorizeRequest)
    {
        var parClientId = pushedAuthorizationRequest.PushedParameters.Get(OidcConstants.AuthorizeRequest.ClientId);
        if (parClientId != authorizeRequest.ClientId)
        {
            {
                return Invalid(authorizeRequest, error: OidcConstants.AuthorizeErrors.InvalidRequestUri,
                    description: "invalid client for pushed authorization request");
            }
        }
        return null;
    }

    public AuthorizeRequestValidationResult? ValidatePushedAuthorizationExpiration(DeserializedPushedAuthorizationRequest pushedAuthorizationRequest, ValidatedAuthorizeRequest authorizeRequest)
    {
        if (DateTime.UtcNow > pushedAuthorizationRequest.ExpiresAtUtc)
        {
            {
                return Invalid(authorizeRequest, error: OidcConstants.AuthorizeErrors.InvalidRequestUri,
                    description: "expired pushed authorization request");
            }
        }
        return null;
    }

    private async Task<DeserializedPushedAuthorizationRequest?> GetPushedAuthorizationRequestAsync(ValidatedAuthorizeRequest request)
    {
        var referenceValue = GetReferenceValue(request);
        if (referenceValue != null)
        {
            return await _pushedAuthorizationService.GetPushedAuthorizationRequestAsync(referenceValue);
        }
        return null;
    }

    private string? GetReferenceValue(ValidatedAuthorizeRequest request)
    {
        var requestUri = request.Raw.Get(OidcConstants.AuthorizeRequest.RequestUri);
        if (requestUri.IsPresent())
        {
            var index = IdentityServerConstants.PushedAuthorizationRequestUri.Length + 1;// +1 for the separator ':'
            if (requestUri.Length > index)
            {
                return requestUri.Substring(index);
            }
        }
        return null;
    }

    public async Task<AuthorizeRequestValidationResult> ValidateRequestObjectAsync(ValidatedAuthorizeRequest request)
    {
        //////////////////////////////////////////////////////////
        // validate request object
        /////////////////////////////////////////////////////////
        if (request.RequestObject.IsPresent())
        {
            // validate the request JWT for this client
            var jwtRequestValidationResult = await _jwtRequestValidator.ValidateAsync(new JwtRequestValidationContext
            {
                Client = request.Client,
                JwtTokenString = request.RequestObject
            });
            if (jwtRequestValidationResult.IsError)
            {
                LogError("request JWT validation failure", request);
                return Invalid(request, error: OidcConstants.AuthorizeErrors.InvalidRequestObject, description: "Invalid JWT request");
            }

            if (jwtRequestValidationResult.Payload == null)
            {
                throw new Exception("JwtRequestValidation succeeded but did not return a payload");
            }

            // validate response_type match
            var responseType = request.Raw.Get(OidcConstants.AuthorizeRequest.ResponseType);
            if (responseType != null)
            {
                var payloadResponseType =
                    jwtRequestValidationResult.Payload.SingleOrDefault(c =>
                        c.Type == OidcConstants.AuthorizeRequest.ResponseType)?.Value;

                if (!string.IsNullOrEmpty(payloadResponseType))
                {
                    if (payloadResponseType != responseType)
                    {
                        LogError("response_type in JWT payload does not match response_type in request", request);
                        return Invalid(request, description: "Invalid JWT request");
                    }
                }
            }

            // validate client_id mismatch
            var payloadClientId =
                jwtRequestValidationResult.Payload.SingleOrDefault(c =>
                    c.Type == OidcConstants.AuthorizeRequest.ClientId)?.Value;

            if (!string.IsNullOrEmpty(payloadClientId))
            {
                if (!string.Equals(request.Client.ClientId, payloadClientId, StringComparison.Ordinal))
                {
                    LogError("client_id in JWT payload does not match client_id in request", request);
                    return Invalid(request, description: "Invalid JWT request");
                }
            }
            else
            {
                LogError("client_id is missing in JWT payload", request);
                return Invalid(request, error: OidcConstants.AuthorizeErrors.InvalidRequestObject, description: "Invalid JWT request");
            }

            var ignoreKeys = new[]
            {
                JwtClaimTypes.Issuer,
                JwtClaimTypes.Audience
            };

            // merge jwt payload values into original request parameters
            // 1. clear the keys in the raw collection for every key found in the request object
            foreach (var claimType in jwtRequestValidationResult.Payload.Select(c => c.Type).Distinct())
            {
                var qsValue = request.Raw.Get(claimType);
                if (qsValue != null)
                {
                    request.Raw.Remove(claimType);
                }
            }

            // 2. copy over the value
            foreach (var claim in jwtRequestValidationResult.Payload)
            {
                request.Raw.Add(claim.Type, claim.Value);
            }

            if (request.AuthorizeRequestType == AuthorizeRequestType.Authorize)
            {
                var ruri = request.Raw.Get(OidcConstants.AuthorizeRequest.RequestUri);
                if (ruri != null)
                {
                    request.Raw.Remove(OidcConstants.AuthorizeRequest.RequestUri);
                    request.Raw.Add(OidcConstants.AuthorizeRequest.Request, request.RequestObject);
                }
            }


            request.RequestObjectValues = jwtRequestValidationResult.Payload;
        }

        return Valid(request);
    }

    private AuthorizeRequestValidationResult Invalid(ValidatedAuthorizeRequest request, string error = OidcConstants.AuthorizeErrors.InvalidRequest, string? description = null) => new AuthorizeRequestValidationResult(request, error, description);

    private AuthorizeRequestValidationResult Valid(ValidatedAuthorizeRequest request) => new AuthorizeRequestValidationResult(request);

    private void LogError(string message, ValidatedAuthorizeRequest request)
    {
        var requestDetails = new AuthorizeRequestValidationLog(request, _options.Logging.AuthorizeRequestSensitiveValuesFilter);
        _logger.LogError(message + "\n{@requestDetails}", requestDetails);
    }

    private void LogError(string message, string detail, ValidatedAuthorizeRequest request)
    {
        var requestDetails = new AuthorizeRequestValidationLog(request, _options.Logging.AuthorizeRequestSensitiveValuesFilter);
        _logger.LogError(message + ": {detail}\n{@requestDetails}", detail, requestDetails);
    }
}
