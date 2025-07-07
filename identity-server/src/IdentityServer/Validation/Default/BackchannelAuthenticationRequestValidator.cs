// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Logging.Models;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;
using static Duende.IdentityServer.Constants;

namespace Duende.IdentityServer.Validation;

internal class BackchannelAuthenticationRequestValidator : IBackchannelAuthenticationRequestValidator
{
    private readonly IdentityServerOptions _options;
    private readonly IResourceValidator _resourceValidator;
    private readonly ITokenValidator _tokenValidator;
    private readonly IBackchannelAuthenticationUserValidator _backchannelAuthenticationUserValidator;
    private readonly IJwtRequestValidator _jwtRequestValidator;
    private readonly ICustomBackchannelAuthenticationValidator _customValidator;
    private readonly LicenseUsageTracker _licenseUsage;
    private readonly ILogger<BackchannelAuthenticationRequestValidator> _logger;

    private ValidatedBackchannelAuthenticationRequest _validatedRequest;

    public BackchannelAuthenticationRequestValidator(
        IdentityServerOptions options,
        IResourceValidator resourceValidator,
        ITokenValidator tokenValidator,
        IBackchannelAuthenticationUserValidator backchannelAuthenticationUserValidator,
        IJwtRequestValidator jwtRequestValidator,
        ICustomBackchannelAuthenticationValidator customValidator,
        LicenseUsageTracker licenseUsage,
        ILogger<BackchannelAuthenticationRequestValidator> logger)
    {
        _options = options;
        _resourceValidator = resourceValidator;
        _tokenValidator = tokenValidator;
        _backchannelAuthenticationUserValidator = backchannelAuthenticationUserValidator;
        _jwtRequestValidator = jwtRequestValidator;
        _customValidator = customValidator;
        _logger = logger;
        _licenseUsage = licenseUsage;
    }

    public async Task<BackchannelAuthenticationRequestValidationResult> ValidateRequestAsync(NameValueCollection parameters, ClientSecretValidationResult clientValidationResult)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("BackchannelAuthenticationRequestValidator.ValidateRequest");

        ArgumentNullException.ThrowIfNull(clientValidationResult);

        _logger.LogDebug("Start backchannel authentication request validation");

        _validatedRequest = new ValidatedBackchannelAuthenticationRequest
        {
            Raw = parameters ?? throw new ArgumentNullException(nameof(parameters)),
            Options = _options
        };
        _validatedRequest.SetClient(clientValidationResult.Client, clientValidationResult.Secret, clientValidationResult.Confirmation);

        //////////////////////////////////////////////////////////
        // Client must be configured for CIBA
        //////////////////////////////////////////////////////////
        if (!clientValidationResult.Client.AllowedGrantTypes.Contains(OidcConstants.GrantTypes.Ciba))
        {
            LogError("Client {clientId} not configured with the CIBA grant type.", clientValidationResult.Client.ClientId);
            return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.UnauthorizedClient, "Unauthorized client");
        }

        _licenseUsage.FeatureUsed(LicenseFeature.CIBA);
        IdentityServerLicenseValidator.Instance.ValidateCiba();

        //////////////////////////////////////////////////////////
        // load request object
        //////////////////////////////////////////////////////////
        var jwtRequest = _validatedRequest.Raw.Get(OidcConstants.BackchannelAuthenticationRequest.Request);

        // check length restrictions
        if (jwtRequest.IsPresent())
        {
            if (jwtRequest.Length >= _options.InputLengthRestrictions.Jwt)
            {
                LogError("request value is too long");
                return Invalid(OidcConstants.AuthorizeErrors.InvalidRequestObject, "Invalid request value");
            }
        }

        _validatedRequest.RequestObject = jwtRequest;

        //////////////////////////////////////////////////////////
        // validate request object
        //////////////////////////////////////////////////////////
        var roValidationResult = await TryValidateRequestObjectAsync();
        if (!roValidationResult.Success)
        {
            return roValidationResult.ErrorResult;
        }

        if (_validatedRequest.Client.RequireRequestObject &&
            !_validatedRequest.RequestObjectValues.Any())
        {
            LogError("Client is configured for RequireRequestObject but none present");
            return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest);
        }

        //////////////////////////////////////////////////////////
        // scope must be present
        //////////////////////////////////////////////////////////
        var scope = _validatedRequest.Raw.Get(OidcConstants.BackchannelAuthenticationRequest.Scope);
        if (scope.IsMissing())
        {
            LogError("Missing scope");
            return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest, "Missing scope");
        }

        if (scope.Length > _options.InputLengthRestrictions.Scope)
        {
            LogError("scopes too long.");
            return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest, "Invalid scope");
        }

        _validatedRequest.RequestedScopes = scope.FromSpaceSeparatedString().Distinct().ToList();

        //////////////////////////////////////////////////////////
        // openid scope required
        //////////////////////////////////////////////////////////
        if (!_validatedRequest.RequestedScopes.Contains(IdentityServerConstants.StandardScopes.OpenId))
        {
            LogError("openid scope missing.");
            return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest, "Missing the openid scope");
        }

        //////////////////////////////////////////////////////////
        // check for resource indicators and valid format
        //////////////////////////////////////////////////////////
        var resourceIndicators = _validatedRequest.Raw.GetValues(OidcConstants.AuthorizeRequest.Resource);
        if (resourceIndicators == null)
        {
            _validatedRequest.RequestedResourceIndicators = [];
        }
        else
        {
            if (resourceIndicators.Any(x => x.Length > _options.InputLengthRestrictions.ResourceIndicatorMaxLength))
            {
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidTarget, "Resource indicator maximum length exceeded");
            }

            if (!resourceIndicators.AreValidResourceIndicatorFormat(_logger))
            {
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidTarget, "Invalid resource indicator format");
            }

            _validatedRequest.RequestedResourceIndicators = resourceIndicators;
        }

        //////////////////////////////////////////////////////////
        // check if scopes are valid/supported and check for resource scopes
        //////////////////////////////////////////////////////////
        var validatedResources = await _resourceValidator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _validatedRequest.Client,
            Scopes = _validatedRequest.RequestedScopes,
            ResourceIndicators = resourceIndicators,
        });

        if (!validatedResources.Succeeded)
        {
            if (validatedResources.InvalidResourceIndicators.Any())
            {
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidTarget, "Invalid resource indicator");
            }

            if (validatedResources.InvalidScopes.Any())
            {
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidScope, "Invalid scope");
            }
        }

        _licenseUsage.ResourceIndicatorsUsed(resourceIndicators);
        IdentityServerLicenseValidator.Instance.ValidateResourceIndicators(resourceIndicators);
        _validatedRequest.ValidatedResources = validatedResources;


        //////////////////////////////////////////////////////////
        // check requested_expiry
        //////////////////////////////////////////////////////////
        var requestLifetime = _validatedRequest.Client.CibaLifetime ?? _options.Ciba.DefaultLifetime;
        var requestedExpiry = _validatedRequest.Raw.Get(OidcConstants.BackchannelAuthenticationRequest.RequestedExpiry);
        if (requestedExpiry.IsPresent())
        {
            // Int32.MaxValue == 2147483647, which is 10 characters in length
            // so using 9 so we don't overflow below on the Int32.Parse
            if (requestedExpiry.Length > 9)
            {
                LogError("requested_expiry too long");
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest, "Invalid requested_expiry");
            }

            if (int.TryParse(requestedExpiry, out var expiryValue) &&
                expiryValue > 0 &&
                expiryValue <= requestLifetime)
            {
                _validatedRequest.Expiry = expiryValue;
            }
            else
            {
                LogError("requested_expiry value out of valid range");
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest, "Invalid requested_expiry");
            }
        }
        else
        {
            _validatedRequest.Expiry = requestLifetime;
        }


        //////////////////////////////////////////////////////////
        // check acr_values
        //////////////////////////////////////////////////////////
        var acrValues = _validatedRequest.Raw.Get(OidcConstants.BackchannelAuthenticationRequest.AcrValues);
        if (acrValues.IsPresent())
        {
            if (acrValues.Length > _options.InputLengthRestrictions.AcrValues)
            {
                LogError("Acr values too long");
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest, "Invalid acr_values");
            }

            _validatedRequest.AuthenticationContextReferenceClasses = acrValues.FromSpaceSeparatedString().Distinct().ToList();

            //////////////////////////////////////////////////////////
            // check custom acr_values: idp and tenant
            //////////////////////////////////////////////////////////
            var tenant = _validatedRequest.AuthenticationContextReferenceClasses.FirstOrDefault(x => x.StartsWith(KnownAcrValues.Tenant, StringComparison.Ordinal));
            if (tenant != null)
            {
                _validatedRequest.AuthenticationContextReferenceClasses.Remove(tenant);
                tenant = tenant.Substring(KnownAcrValues.Tenant.Length);
                _validatedRequest.Tenant = tenant;
            }

            var idp = _validatedRequest.AuthenticationContextReferenceClasses.FirstOrDefault(x => x.StartsWith(KnownAcrValues.HomeRealm, StringComparison.Ordinal));
            if (idp != null)
            {
                _validatedRequest.AuthenticationContextReferenceClasses.Remove(idp);
                idp = idp.Substring(KnownAcrValues.HomeRealm.Length);

                // check if idp is present but client does not allow it, and then ignore it
                if (_validatedRequest.Client.IdentityProviderRestrictions != null &&
                    _validatedRequest.Client.IdentityProviderRestrictions.Any())
                {
                    if (!_validatedRequest.Client.IdentityProviderRestrictions.Contains(idp))
                    {
                        _logger.LogWarning("idp requested ({idp}) is not in client restriction list.", idp);
                        idp = null;
                    }
                }

                _validatedRequest.IdP = idp;
            }
        }


        //////////////////////////////////////////////////////////
        // login hints
        //////////////////////////////////////////////////////////
        var loginHint = _validatedRequest.Raw.Get(OidcConstants.BackchannelAuthenticationRequest.LoginHint);
        var loginHintToken = _validatedRequest.Raw.Get(OidcConstants.BackchannelAuthenticationRequest.LoginHintToken);
        var idTokenHint = _validatedRequest.Raw.Get(OidcConstants.BackchannelAuthenticationRequest.IdTokenHint);

        var loginHintCount = 0;
        if (loginHint.IsPresent())
        {
            loginHintCount++;
        }

        if (loginHintToken.IsPresent())
        {
            loginHintCount++;
        }

        if (idTokenHint.IsPresent())
        {
            loginHintCount++;
        }

        if (loginHintCount == 0)
        {
            LogError("Missing login_hint_token, id_token_hint, or login_hint");
            return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest, "Missing login_hint_token, id_token_hint, or login_hint");
        }
        else if (loginHintCount > 1)
        {
            LogError("Too many of login_hint_token, id_token_hint, or login_hint");
            return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest, "Too many of login_hint_token, id_token_hint, or login_hint");
        }

        //////////////////////////////////////////////////////////
        // check login_hint
        //////////////////////////////////////////////////////////
        if (loginHint.IsPresent())
        {
            if (loginHint.Length > _options.InputLengthRestrictions.LoginHint)
            {
                LogError("Login hint too long");
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest, "Invalid login_hint");
            }

            _validatedRequest.LoginHint = loginHint;
        }

        //////////////////////////////////////////////////////////
        // check login_hint_token
        //////////////////////////////////////////////////////////
        if (loginHintToken.IsPresent())
        {
            if (loginHintToken.Length > _options.InputLengthRestrictions.LoginHintToken)
            {
                LogError("Login hint token too long");
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest, "Invalid login_hint_token");
            }

            _validatedRequest.LoginHintToken = loginHintToken;
        }

        //////////////////////////////////////////////////////////
        // check id_token_hint
        //////////////////////////////////////////////////////////
        if (idTokenHint.IsPresent())
        {
            if (idTokenHint.Length > _options.InputLengthRestrictions.IdTokenHint)
            {
                LogError("id token hint too long");
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest, "Invalid id_token_hint");
            }

            var idTokenHintValidationResult = await _tokenValidator.ValidateIdentityTokenAsync(idTokenHint, _validatedRequest.ClientId, false);
            if (idTokenHintValidationResult.IsError)
            {
                LogError("id token hint failed to validate: " + idTokenHintValidationResult.Error, idTokenHintValidationResult.ErrorDescription);
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest, "Invalid id_token_hint");
            }

            _validatedRequest.IdTokenHint = idTokenHint;
            _validatedRequest.IdTokenHintClaims = idTokenHintValidationResult.Claims;
        }

        //////////////////////////////////////////////////////////
        // check user_code
        //////////////////////////////////////////////////////////
        var userCode = _validatedRequest.Raw.Get(OidcConstants.BackchannelAuthenticationRequest.UserCode);
        if (userCode.IsPresent())
        {
            if (userCode.Length > _options.InputLengthRestrictions.UserCode)
            {
                LogError("user_code too long");
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest, "Invalid user_code");
            }

            _validatedRequest.UserCode = userCode;
        }

        //////////////////////////////////////////////////////////
        // check binding_message
        //////////////////////////////////////////////////////////
        var bindingMessage = _validatedRequest.Raw.Get(OidcConstants.BackchannelAuthenticationRequest.BindingMessage);
        if (bindingMessage.IsPresent())
        {
            if (bindingMessage.Length > _options.InputLengthRestrictions.BindingMessage)
            {
                LogError("binding_message too long");
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidBindingMessage, "Invalid binding_message");
            }

            _validatedRequest.BindingMessage = bindingMessage;
        }

        //////////////////////////////////////////////////////////
        // validate the login hint w/ custom validator
        //////////////////////////////////////////////////////////
        var userResult = await _backchannelAuthenticationUserValidator.ValidateRequestAsync(new BackchannelAuthenticationUserValidatorContext
        {
            Client = _validatedRequest.Client,
            IdTokenHint = _validatedRequest.IdTokenHint,
            LoginHint = _validatedRequest.LoginHint,
            LoginHintToken = _validatedRequest.LoginHintToken,
            IdTokenHintClaims = _validatedRequest.IdTokenHintClaims,
            UserCode = _validatedRequest.UserCode,
            BindingMessage = _validatedRequest.BindingMessage
        });

        if (userResult.IsError)
        {
            if (userResult.Error == OidcConstants.BackchannelAuthenticationRequestErrors.AccessDenied)
            {
                LogError("Request was denied access for that user");
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.AccessDenied, userResult.ErrorDescription);
            }
            if (userResult.Error == OidcConstants.BackchannelAuthenticationRequestErrors.ExpiredLoginHintToken)
            {
                LogError("Expired login_hint_token");
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.ExpiredLoginHintToken, userResult.ErrorDescription ?? "Expired login_hint_token");
            }
            if (userResult.Error == OidcConstants.BackchannelAuthenticationRequestErrors.UnknownUserId)
            {
                LogError("Unknown user id");
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.UnknownUserId, userResult.ErrorDescription);
            }
            if (userResult.Error == OidcConstants.BackchannelAuthenticationRequestErrors.MissingUserCode)
            {
                LogError("Missing user_code");
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.MissingUserCode, userResult.ErrorDescription);
            }
            if (userResult.Error == OidcConstants.BackchannelAuthenticationRequestErrors.InvalidUserCode)
            {
                LogError("Invalid user_code");
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidUserCode, userResult.ErrorDescription);
            }
            if (userResult.Error == OidcConstants.BackchannelAuthenticationRequestErrors.InvalidBindingMessage)
            {
                LogError("Invalid binding_message");
                return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidBindingMessage, userResult.ErrorDescription);
            }

            LogError("Unexpected error from IBackchannelAuthenticationUserValidator: {error}", userResult.Error);
            return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.UnknownUserId);
        }

        if (userResult.Subject == null || !userResult.Subject.HasClaim(x => x.Type == JwtClaimTypes.Subject))
        {
            LogError("No subject or subject id returned from IBackchannelAuthenticationUserValidator");
            return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.UnknownUserId);
        }

        _validatedRequest.Subject = userResult.Subject;
        var result = new BackchannelAuthenticationRequestValidationResult(_validatedRequest);

        var customValidationContext = new CustomBackchannelAuthenticationRequestValidationContext(result);
        await _customValidator.ValidateAsync(customValidationContext);
        if (customValidationContext.ValidationResult.IsError)
        {
            LogError("Custom validation of backchannel authorize request failed");
            return Invalid(OidcConstants.BackchannelAuthenticationRequestErrors.InvalidRequest);
        }

        LogSuccess();
        return result;
    }

    private async Task<(bool Success, BackchannelAuthenticationRequestValidationResult ErrorResult)> TryValidateRequestObjectAsync()
    {
        //////////////////////////////////////////////////////////
        // validate request object
        /////////////////////////////////////////////////////////
        if (_validatedRequest.RequestObject.IsPresent())
        {
            // validate the request JWT for this client
            var jwtRequestValidationResult = await _jwtRequestValidator.ValidateAsync(new JwtRequestValidationContext
            {
                Client = _validatedRequest.Client,
                JwtTokenString = _validatedRequest.RequestObject,
                StrictJarValidation = false,
                IncludeJti = true
            });
            if (jwtRequestValidationResult.IsError)
            {
                LogError("request JWT validation failure", jwtRequestValidationResult.Error);
                return (false, Invalid(OidcConstants.AuthorizeErrors.InvalidRequestObject, "Invalid JWT request"));
            }

            // client_id not required in JWT, but just in case we will validate it
            var payloadClientId = jwtRequestValidationResult.Payload.SingleOrDefault(x => x.Type == JwtClaimTypes.ClientId)?.Value;
            if (payloadClientId.IsPresent() && _validatedRequest.Client.ClientId != payloadClientId)
            {
                LogError("client_id found in the JWT request object does not match client_id used to authenticate, {@values}", new { invalidClientId = payloadClientId, clientId = _validatedRequest.Client.ClientId });
                return (false, Invalid(OidcConstants.AuthorizeErrors.InvalidRequestObject, "Invalid client_id in JWT request"));
            }

            // validate jti in request token
            var jti = jwtRequestValidationResult.Payload.SingleOrDefault(x => x.Type == JwtClaimTypes.JwtId)?.Value;
            if (jti.IsMissing())
            {
                LogError("Missing jti in JWT request object");
                return (false, Invalid(OidcConstants.AuthorizeErrors.InvalidRequestObject, "Missing jti in JWT request object"));
            }

            // validate that no request params are in body, and merge them into the request collection
            foreach (var claim in jwtRequestValidationResult.Payload)
            {
                // we already checked client_id above
                if (claim.Type != JwtClaimTypes.ClientId)
                {
                    if (_validatedRequest.Raw.AllKeys.Contains(claim.Type))
                    {
                        LogError("Parameter from JWT request object also found in request body: {name}", claim.Type);
                        return (false, Invalid(OidcConstants.AuthorizeErrors.InvalidRequestObject, "Parameter from JWT request object also found in request body"));
                    }
                    else if (claim.Type != JwtClaimTypes.JwtId)
                    {
                        _validatedRequest.Raw.Add(claim.Type, claim.Value);
                    }
                }
            }

            _validatedRequest.RequestObjectValues = jwtRequestValidationResult.Payload;
        }

        return (true, null);
    }



    private BackchannelAuthenticationRequestValidationResult Invalid(string error, string errorDescription = null) => new BackchannelAuthenticationRequestValidationResult(_validatedRequest, error, errorDescription);

    private void LogError(string message = null, object values = null) => LogWithRequestDetails(LogLevel.Error, message, values);

    private void LogWithRequestDetails(LogLevel logLevel, string message = null, object values = null)
    {
        var details = new BackchannelAuthenticationRequestValidationLog(_validatedRequest, _options.Logging.BackchannelAuthenticationRequestSensitiveValuesFilter);

        if (message.IsPresent())
        {
            try
            {
                if (values == null)
                {
                    _logger.Log(logLevel, message + ", {@details}", details);
                }
                else
                {
                    _logger.Log(logLevel, message + ", details: {@details}", values, details);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError("Error logging {exception}, request details: {@details}", ex.Message, details);
            }
        }
        else
        {
            _logger.Log(logLevel, "{@details}", details);
        }
    }

    private void LogSuccess() => LogWithRequestDetails(LogLevel.Information, "Backchannel authentication request validation success");
}
