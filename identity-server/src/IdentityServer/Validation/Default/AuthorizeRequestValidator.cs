// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Specialized;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Licensing.V2.Diagnostics;
using Duende.IdentityServer.Logging;
using Duende.IdentityServer.Logging.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using static Duende.IdentityServer.IdentityServerConstants;

namespace Duende.IdentityServer.Validation;

internal class AuthorizeRequestValidator : IAuthorizeRequestValidator
{
    private readonly IdentityServerOptions _options;
    private readonly IIssuerNameService _issuerNameService;
    private readonly IClientStore _clients;
    private readonly ICustomAuthorizeRequestValidator _customValidator;
    private readonly IRedirectUriValidator _uriValidator;
    private readonly IResourceValidator _resourceValidator;
    private readonly IUserSession _userSession;
    private readonly IRequestObjectValidator _requestObjectValidator;
    private readonly LicenseUsageTracker _licenseUsage;
    private readonly ClientLoadedTracker _clientLoadedTracker;
    private readonly ResourceLoadedTracker _resourceLoadedTracker;
    private readonly SanitizedLogger<AuthorizeRequestValidator> _sanitizedLogger;

    private readonly ResponseTypeEqualityComparer
        _responseTypeEqualityComparer = new ResponseTypeEqualityComparer();


    public AuthorizeRequestValidator(
        IdentityServerOptions options,
        IIssuerNameService issuerNameService,
        IClientStore clients,
        ICustomAuthorizeRequestValidator customValidator,
        IRedirectUriValidator uriValidator,
        IResourceValidator resourceValidator,
        IUserSession userSession,
        IRequestObjectValidator requestObjectValidator,
        LicenseUsageTracker licenseUsage,
        ClientLoadedTracker clientLoadedTracker,
        ResourceLoadedTracker resourceLoadedTracker,
        SanitizedLogger<AuthorizeRequestValidator> sanitizedLogger)
    {
        _options = options;
        _issuerNameService = issuerNameService;
        _clients = clients;
        _customValidator = customValidator;
        _uriValidator = uriValidator;
        _resourceValidator = resourceValidator;
        _requestObjectValidator = requestObjectValidator;
        _userSession = userSession;
        _licenseUsage = licenseUsage;
        _clientLoadedTracker = clientLoadedTracker;
        _resourceLoadedTracker = resourceLoadedTracker;
        _sanitizedLogger = sanitizedLogger;
    }

    public async Task<AuthorizeRequestValidationResult> ValidateAsync(
        NameValueCollection parameters,
        Ct ct,
        ClaimsPrincipal? subject = null,
        AuthorizeRequestType authorizeRequestType = AuthorizeRequestType.Authorize)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("AuthorizeRequestValidator.Validate");

        _sanitizedLogger.LogDebug("Start authorize request protocol validation");

        var request = new ValidatedAuthorizeRequest
        {
            Options = _options,
            IssuerName = await _issuerNameService.GetCurrentAsync(ct),
            Subject = subject ?? Principal.Anonymous,
            Raw = parameters ?? throw new ArgumentNullException(nameof(parameters)),
            AuthorizeRequestType = authorizeRequestType
        };

        //validate ui_locales first so it's available for the rest of the flow for better localization support in the case of errors
        var uiLocalesResult = ValidateUiLocales(request);
        if (uiLocalesResult.IsError)
        {
            return uiLocalesResult;
        }

        // load client_id
        // client_id must always be present on the request
        var loadClientResult = await LoadClientAsync(request, ct);
        if (loadClientResult.IsError)
        {
            return loadClientResult;
        }

        // load request object
        var roLoadResult = await _requestObjectValidator.LoadRequestObjectAsync(request, ct);
        if (roLoadResult.IsError)
        {
            return roLoadResult;
        }

        // validate request object
        var roValidationResult = await _requestObjectValidator.ValidateRequestObjectAsync(request, ct);
        if (roValidationResult.IsError)
        {
            return roValidationResult;
        }

        // need to validate ui_locales again if it came from the request object
        uiLocalesResult = ValidateUiLocales(request);
        if (uiLocalesResult.IsError)
        {
            return uiLocalesResult;
        }

        // validate client_id and redirect_uri
        var clientResult = await ValidateClientAsync(request, ct);
        if (clientResult.IsError)
        {
            return clientResult;
        }

        // state, response_type, response_mode
        var mandatoryResult = ValidateCoreParameters(request);
        if (mandatoryResult.IsError)
        {
            return mandatoryResult;
        }

        // scope, scope restrictions and plausibility, and resource indicators
        var scopeResult = await ValidateScopeAndResourceAsync(request, ct);
        if (scopeResult.IsError)
        {
            return scopeResult;
        }

        // nonce, prompt, acr_values, login_hint etc.
        var optionalResult = await ValidateOptionalParametersAsync(request, ct);
        if (optionalResult.IsError)
        {
            return optionalResult;
        }

        // custom validator
        _sanitizedLogger.LogDebug("Calling into custom validator: {type}", _customValidator.GetType().FullName);
        var context = new CustomAuthorizeRequestValidationContext
        {
            Result = new AuthorizeRequestValidationResult(request)
        };
        await _customValidator.ValidateAsync(context, ct);

        var customResult = context.Result;
        if (customResult.IsError)
        {
            LogError("Error in custom validation", customResult.Error, request);
            return Invalid(request, customResult.Error ?? OidcConstants.AuthorizeErrors.InvalidRequest, customResult.ErrorDescription);
        }

        _sanitizedLogger.LogTrace("Authorize request protocol validation successful");

        _licenseUsage.ClientUsed(request.ClientId);
        _clientLoadedTracker.TrackClientLoaded(request.Client);
        IdentityServerLicenseValidator.Instance.ValidateClient(request.ClientId);
        _resourceLoadedTracker.TrackResources(request.ValidatedResources.Resources);

        return Valid(request);
    }

    // Support JAR + PAR together - if there is a request object within the PAR, extract it

    private AuthorizeRequestValidationResult ValidateUiLocales(ValidatedAuthorizeRequest request)
    {
        //////////////////////////////////////////////////////////
        // check ui locales
        //////////////////////////////////////////////////////////
        var uilocales = request.Raw.Get(OidcConstants.AuthorizeRequest.UiLocales);
        if (uilocales.IsPresent())
        {
            if (uilocales.Length > _options.InputLengthRestrictions.UiLocale)
            {
                LogError("UI locale too long", request);
                return Invalid(request, description: "Invalid ui_locales");
            }

            request.UiLocales = uilocales;
        }

        return Valid(request);
    }

    private async Task<AuthorizeRequestValidationResult> LoadClientAsync(ValidatedAuthorizeRequest request, Ct ct)
    {
        //////////////////////////////////////////////////////////
        // client_id must be present
        /////////////////////////////////////////////////////////
        var clientId = request.Raw.Get(OidcConstants.AuthorizeRequest.ClientId);

        if (clientId.IsMissingOrTooLong(_options.InputLengthRestrictions.ClientId))
        {
            LogError("client_id is missing or too long", request);
            return Invalid(request, description: "Invalid client_id");
        }

        request.ClientId = clientId!;

        //////////////////////////////////////////////////////////
        // check for valid client
        //////////////////////////////////////////////////////////
        var client = await _clients.FindEnabledClientByIdAsync(request.ClientId, ct);
        if (client == null)
        {
            LogError("Unknown client or not enabled", request.ClientId, request);
            return Invalid(request, OidcConstants.AuthorizeErrors.UnauthorizedClient, "Unknown client or client not enabled");
        }

        request.SetClient(client);

        return Valid(request);
    }

    private async Task<AuthorizeRequestValidationResult> ValidateClientAsync(ValidatedAuthorizeRequest request, Ct ct)
    {
        //////////////////////////////////////////////////////////
        // check request object requirement
        //////////////////////////////////////////////////////////
        if (request.Client.RequireRequestObject)
        {
            if (!request.RequestObjectValues.Any())
            {
                return Invalid(request, description: "Client must use request object, but no request or request_uri parameter present");
            }
        }

        //////////////////////////////////////////////////////////
        // redirect_uri must be present, and a valid uri
        //////////////////////////////////////////////////////////
        var redirectUri = request.Raw.Get(OidcConstants.AuthorizeRequest.RedirectUri);

        if (redirectUri.IsMissingOrTooLong(_options.InputLengthRestrictions.RedirectUri))
        {
            LogError("redirect_uri is missing or too long", request);
            return Invalid(request, description: "Invalid redirect_uri");
        }

        if (!redirectUri!.IsUri())
        {
            LogError("malformed redirect_uri", redirectUri, request);
            return Invalid(request, description: "Invalid redirect_uri");
        }

        //////////////////////////////////////////////////////////
        // check if client protocol type is oidc
        //////////////////////////////////////////////////////////
        if (request.Client.ProtocolType != IdentityServerConstants.ProtocolTypes.OpenIdConnect)
        {
            LogError("Invalid protocol type for OIDC authorize endpoint", request.Client.ProtocolType, request);
            return Invalid(request, OidcConstants.AuthorizeErrors.UnauthorizedClient, description: "Invalid protocol");
        }

        //////////////////////////////////////////////////////////
        // check if redirect_uri is valid
        //////////////////////////////////////////////////////////
        var uriContext = new RedirectUriValidationContext(redirectUri!, request);
        if (await _uriValidator.IsRedirectUriValidAsync(uriContext, ct) == false)
        {
            LogError("Invalid redirect_uri", redirectUri, request);
            return Invalid(request, OidcConstants.AuthorizeErrors.InvalidRequest, "Invalid redirect_uri");
        }

        request.RedirectUri = redirectUri!;

        return Valid(request);
    }

    private AuthorizeRequestValidationResult ValidateCoreParameters(ValidatedAuthorizeRequest request)
    {
        //////////////////////////////////////////////////////////
        // check state
        //////////////////////////////////////////////////////////
        var state = request.Raw.Get(OidcConstants.AuthorizeRequest.State);
        if (state.IsPresent())
        {
            request.State = state;
        }

        //////////////////////////////////////////////////////////
        // response_type must be present and supported
        //////////////////////////////////////////////////////////
        var responseType = request.Raw.Get(OidcConstants.AuthorizeRequest.ResponseType);
        if (responseType.IsMissing())
        {
            LogError("Missing response_type", request);
            return Invalid(request, OidcConstants.AuthorizeErrors.InvalidRequest, "Missing response_type");
        }

        // The responseType may come in in an unconventional order.
        // Use an IEqualityComparer that doesn't care about the order of multiple values.
        // Per https://tools.ietf.org/html/rfc6749#section-3.1.1 -
        // 'Extension response types MAY contain a space-delimited (%x20) list of
        // values, where the order of values does not matter (e.g., response
        // type "a b" is the same as "b a").'
        // http://openid.net/specs/oauth-v2-multiple-response-types-1_0-03.html#terminology -
        // 'If a response type contains one of more space characters (%20), it is compared
        // as a space-delimited list of values in which the order of values does not matter.'
        if (!Constants.SupportedResponseTypes.Contains(responseType, _responseTypeEqualityComparer))
        {
            LogError("Response type not supported", responseType, request);
            return Invalid(request, OidcConstants.AuthorizeErrors.UnsupportedResponseType, "Response type not supported");
        }

        // Even though the responseType may have come in in an unconventional order,
        // we still need the request's ResponseType property to be set to the
        // conventional, supported response type.
        request.ResponseType = Constants.SupportedResponseTypes.First(
            supportedResponseType => _responseTypeEqualityComparer.Equals(supportedResponseType, responseType));

        //////////////////////////////////////////////////////////
        // match response_type to grant type
        //////////////////////////////////////////////////////////
        request.GrantType = Constants.ResponseTypeToGrantTypeMapping[request.ResponseType];

        // set default response mode for flow; this is needed for any client error processing below
        request.ResponseMode = Constants.AllowedResponseModesForGrantType[request.GrantType].First();

        //////////////////////////////////////////////////////////
        // check response_mode parameter and set response_mode
        //////////////////////////////////////////////////////////

        // check if response_mode parameter is present and valid
        // NOTE: this is intentionally done before the grant-type and PKCE checks below so that
        // error responses produced by those checks already carry the client's requested
        // response_mode rather than the flow default.
        var responseMode = request.Raw.Get(OidcConstants.AuthorizeRequest.ResponseMode);
        if (responseMode.IsPresent())
        {
            if (Constants.SupportedResponseModes.Contains(responseMode))
            {
                if (Constants.AllowedResponseModesForGrantType[request.GrantType].Contains(responseMode))
                {
                    request.ResponseMode = responseMode;
                }
                else
                {
                    LogError("Invalid response_mode for response_type", responseMode, request);
                    return Invalid(request, OidcConstants.AuthorizeErrors.InvalidRequest, description: "Invalid response_mode for response_type");
                }
            }
            else
            {
                LogError("Unsupported response_mode", responseMode, request);
                return Invalid(request, OidcConstants.AuthorizeErrors.UnsupportedResponseType, description: "Invalid response_mode");
            }
        }

        //////////////////////////////////////////////////////////
        // check if flow is allowed at authorize endpoint
        //////////////////////////////////////////////////////////
        if (!Constants.AllowedGrantTypesForAuthorizeEndpoint.Contains(request.GrantType))
        {
            LogError("Invalid grant type", request.GrantType, request);
            return Invalid(request, description: "Invalid response_type");
        }

        //////////////////////////////////////////////////////////
        // check if PKCE is required and validate parameters
        //////////////////////////////////////////////////////////
        if (request.GrantType == GrantType.AuthorizationCode || request.GrantType == GrantType.Hybrid)
        {
            _sanitizedLogger.LogDebug("Checking for PKCE parameters");

            /////////////////////////////////////////////////////////////////////////////
            // validate code_challenge and code_challenge_method
            /////////////////////////////////////////////////////////////////////////////
            var proofKeyResult = ValidatePkceParameters(request);

            if (proofKeyResult.IsError)
            {
                return proofKeyResult;
            }
        }


        //////////////////////////////////////////////////////////
        // check if grant type is allowed for client
        //////////////////////////////////////////////////////////
        if (!request.Client.AllowedGrantTypes.Contains(request.GrantType))
        {
            LogError("Invalid grant type for client", request.GrantType, request);
            return Invalid(request, OidcConstants.AuthorizeErrors.UnauthorizedClient, "Invalid grant type for client");
        }

        //////////////////////////////////////////////////////////
        // check if response type contains an access token,
        // and if client is allowed to request access token via browser
        //////////////////////////////////////////////////////////
        var responseTypes = responseType.FromSpaceSeparatedString();
        if (responseTypes.Contains(OidcConstants.ResponseTypes.Token))
        {
            if (!request.Client.AllowAccessTokensViaBrowser)
            {
                LogError("Client requested access token - but client is not configured to receive access tokens via browser", request);
                return Invalid(request, description: "Client not configured to receive access tokens via browser");
            }
        }

        return Valid(request);
    }

    private AuthorizeRequestValidationResult ValidatePkceParameters(ValidatedAuthorizeRequest request)
    {
        var fail = Invalid(request);

        var codeChallenge = request.Raw.Get(OidcConstants.AuthorizeRequest.CodeChallenge);
        if (codeChallenge.IsMissing())
        {
            if (request.Client.RequirePkce)
            {
                LogError("code_challenge is missing", request);
                fail.ErrorDescription = "code challenge required";
            }
            else
            {
                _sanitizedLogger.LogDebug("No PKCE used.");
                return Valid(request);
            }

            return fail;
        }

        if (codeChallenge.Length < _options.InputLengthRestrictions.CodeChallengeMinLength ||
            codeChallenge.Length > _options.InputLengthRestrictions.CodeChallengeMaxLength)
        {
            LogError("code_challenge is either too short or too long", request);
            fail.ErrorDescription = "Invalid code_challenge";
            return fail;
        }

        request.CodeChallenge = codeChallenge;

        var codeChallengeMethod = request.Raw.Get(OidcConstants.AuthorizeRequest.CodeChallengeMethod);
        if (codeChallengeMethod.IsMissing())
        {
            _sanitizedLogger.LogDebug("Missing code_challenge_method, defaulting to plain");
            codeChallengeMethod = OidcConstants.CodeChallengeMethods.Plain;
        }

        if (!Constants.SupportedCodeChallengeMethods.Contains(codeChallengeMethod))
        {
            LogError("Unsupported code_challenge_method", codeChallengeMethod, request);
            fail.ErrorDescription = "Transform algorithm not supported";
            return fail;
        }

        // check if plain method is allowed
        if (codeChallengeMethod == OidcConstants.CodeChallengeMethods.Plain)
        {
            if (!request.Client.AllowPlainTextPkce)
            {
                LogError("code_challenge_method of plain is not allowed", request);
                fail.ErrorDescription = "Transform algorithm not supported";
                return fail;
            }
        }

        request.CodeChallengeMethod = codeChallengeMethod;

        return Valid(request);
    }

    private async Task<AuthorizeRequestValidationResult> ValidateScopeAndResourceAsync(ValidatedAuthorizeRequest request, Ct ct)
    {
        //////////////////////////////////////////////////////////
        // scope must be present
        //////////////////////////////////////////////////////////
        var scope = request.Raw.Get(OidcConstants.AuthorizeRequest.Scope);
        if (scope.IsMissing())
        {
            LogError("scope is missing", request);
            return Invalid(request, description: "Invalid scope");
        }

        if (scope.Length > _options.InputLengthRestrictions.Scope)
        {
            LogError("scopes too long.", request);
            return Invalid(request, description: "Invalid scope");
        }

        request.RequestedScopes = scope.FromSpaceSeparatedString().Distinct().ToList();
        request.IsOpenIdRequest = request.RequestedScopes.Contains(IdentityServerConstants.StandardScopes.OpenId);

        //////////////////////////////////////////////////////////
        // check scope vs response_type plausability
        //////////////////////////////////////////////////////////
        var requirement = Constants.ResponseTypeToScopeRequirement[request.ResponseType];
        if (requirement == Constants.ScopeRequirement.Identity ||
            requirement == Constants.ScopeRequirement.IdentityOnly)
        {
            if (request.IsOpenIdRequest == false)
            {
                LogError("response_type requires the openid scope", request);
                return Invalid(request, description: "Missing openid scope");
            }
        }


        //////////////////////////////////////////////////////////
        // check for resource indicators and valid format
        //////////////////////////////////////////////////////////
        var resourceIndicators = request.Raw.GetValues(OidcConstants.AuthorizeRequest.Resource);
        if (resourceIndicators == null)
        {
            request.RequestedResourceIndicators = [];
        }
        else
        {
            if (resourceIndicators.Any(x => x.Length > _options.InputLengthRestrictions.ResourceIndicatorMaxLength))
            {
                return Invalid(request, OidcConstants.AuthorizeErrors.InvalidTarget, "Resource indicator maximum length exceeded");
            }

            if (!resourceIndicators.AreValidResourceIndicatorFormat(_sanitizedLogger.ToILogger()))
            {
                return Invalid(request, OidcConstants.AuthorizeErrors.InvalidTarget, "Invalid resource indicator format");
            }

            // we don't want to allow resource indicators when "token" is requested to authorize endpoint
            if (request.GrantType == GrantType.Implicit && resourceIndicators.Length != 0)
            {
                // todo: correct error?
                return Invalid(request, OidcConstants.AuthorizeErrors.InvalidTarget, "Resource indicators not allowed for response_type 'token'.");
            }

            request.RequestedResourceIndicators = resourceIndicators;
        }

        //////////////////////////////////////////////////////////
        // check if scopes are valid/supported and check for resource scopes
        //////////////////////////////////////////////////////////
        var validatedResources = await _resourceValidator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = request.Client,
            Scopes = request.RequestedScopes,
            ResourceIndicators = resourceIndicators,
        }, ct);

        if (!validatedResources.Succeeded)
        {
            if (validatedResources.InvalidResourceIndicators.Count > 0)
            {
                return Invalid(request, OidcConstants.AuthorizeErrors.InvalidTarget, "Invalid resource indicator");
            }

            if (validatedResources.InvalidScopes.Count > 0)
            {
                return Invalid(request, OidcConstants.AuthorizeErrors.InvalidScope, "Invalid scope");
            }
        }

        _licenseUsage.ResourceIndicatorsUsed(resourceIndicators!);
        IdentityServerLicenseValidator.Instance.ValidateResourceIndicators(resourceIndicators);

        if (validatedResources.Resources.IdentityResources.Count > 0 && !request.IsOpenIdRequest)
        {
            LogError("Identity related scope requests, but no openid scope", request);
            return Invalid(request, OidcConstants.AuthorizeErrors.InvalidScope, "Identity scopes requested, but openid scope is missing");
        }

        if (validatedResources.Resources.ApiScopes.Count > 0)
        {
            request.IsApiResourceRequest = true;
        }

        //////////////////////////////////////////////////////////
        // check id vs resource scopes and response types plausability
        //////////////////////////////////////////////////////////
        var responseTypeValidationCheck = true;
        switch (requirement)
        {
            case Constants.ScopeRequirement.Identity:
                if (validatedResources.Resources.IdentityResources.Count == 0)
                {
                    _sanitizedLogger.LogError("Requests for id_token response type must include identity scopes");
                    responseTypeValidationCheck = false;
                }
                break;
            case Constants.ScopeRequirement.IdentityOnly:
                if (validatedResources.Resources.IdentityResources.Count == 0 || validatedResources.Resources.ApiScopes.Count > 0)
                {
                    _sanitizedLogger.LogError("Requests for id_token response type only must not include resource scopes");
                    responseTypeValidationCheck = false;
                }
                break;
            case Constants.ScopeRequirement.ResourceOnly:
                if (validatedResources.Resources.IdentityResources.Count > 0 || validatedResources.Resources.ApiScopes.Count == 0)
                {
                    _sanitizedLogger.LogError("Requests for token response type only must include resource scopes, but no identity scopes.");
                    responseTypeValidationCheck = false;
                }
                break;
        }

        if (!responseTypeValidationCheck)
        {
            return Invalid(request, OidcConstants.AuthorizeErrors.InvalidScope, "Invalid scope for response type");
        }

        request.ValidatedResources = validatedResources;

        return Valid(request);
    }

    private async Task<AuthorizeRequestValidationResult> ValidateOptionalParametersAsync(ValidatedAuthorizeRequest request, Ct ct)
    {
        //////////////////////////////////////////////////////////
        // check nonce
        //////////////////////////////////////////////////////////
        var nonce = request.Raw.Get(OidcConstants.AuthorizeRequest.Nonce);
        if (nonce.IsPresent())
        {
            if (nonce.Length > _options.InputLengthRestrictions.Nonce)
            {
                LogError("Nonce too long", request);
                return Invalid(request, description: "Invalid nonce");
            }

            request.Nonce = nonce;
        }
        else
        {
            if (request.ResponseType.FromSpaceSeparatedString().Contains(TokenTypes.IdentityToken))
            {
                LogError("Nonce required for flow with id_token response type", request);
                return Invalid(request, description: "Invalid nonce");
            }
        }


        //////////////////////////////////////////////////////////
        // check prompt
        //////////////////////////////////////////////////////////
        var prompt = request.Raw.Get(OidcConstants.AuthorizeRequest.Prompt);
        if (prompt.IsPresent())
        {
            var prompts = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (prompts.All(p => _options.UserInteraction.PromptValuesSupported?.Contains(p) == true))
            {
                if (prompts.Contains(OidcConstants.PromptModes.None) && prompts.Length > 1)
                {
                    LogError("prompt contains 'none' and other values. 'none' should be used by itself.", request);
                    return Invalid(request, description: "Invalid prompt");
                }
                if (prompts.Contains(OidcConstants.PromptModes.Create) && prompts.Length > 1)
                {
                    LogError("prompt contains 'create' and other values. 'create' should be used by itself.", request);
                    return Invalid(request, description: "Invalid prompt");
                }

                request.OriginalPromptModes = prompts;
            }
            else
            {
                LogError("Unsupported prompt mode", request);
                return Invalid(request, description: "Unsupported prompt mode");
            }
        }

        var processed_prompt = request.Raw.Get(Constants.ProcessedPrompt);
        if (processed_prompt.IsPresent())
        {
            var prompts = processed_prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (prompts.All(p => _options.UserInteraction.PromptValuesSupported?.Contains(p) == true))
            {
                if (prompts.Contains(OidcConstants.PromptModes.None) && prompts.Length > 1)
                {
                    LogError("processed_prompt contains 'none' and other values. 'none' should be used by itself.", request);
                    return Invalid(request, description: "Invalid prompt");
                }
                if (prompts.Contains(OidcConstants.PromptModes.Create) && prompts.Length > 1)
                {
                    LogError("prompt contains 'create' and other values. 'create' should be used by itself.", request);
                    return Invalid(request, description: "Invalid prompt");
                }

                request.ProcessedPromptModes = prompts;
            }
            else
            {
                LogError("Unsupported processed_prompt mode.", request);
                return Invalid(request, description: "Invalid prompt");
            }
        }

        request.PromptModes = request.OriginalPromptModes.Except(request.ProcessedPromptModes).ToArray();

        //////////////////////////////////////////////////////////
        // check display
        //////////////////////////////////////////////////////////
        var display = request.Raw.Get(OidcConstants.AuthorizeRequest.Display);
        if (display.IsPresent())
        {
            if (Constants.SupportedDisplayModes.Contains(display))
            {
                request.DisplayMode = display;
            }

            _sanitizedLogger.LogDebug("Unsupported display mode - ignored: {display}", display);
        }

        //////////////////////////////////////////////////////////
        // check max_age
        //////////////////////////////////////////////////////////
        var maxAge = request.Raw.Get(OidcConstants.AuthorizeRequest.MaxAge);
        if (maxAge.IsPresent())
        {
            if (int.TryParse(maxAge, out var seconds))
            {
                if (seconds >= 0)
                {
                    request.MaxAge = seconds;
                }
                else
                {
                    LogError("Invalid max_age.", request);
                    return Invalid(request, description: "Invalid max_age");
                }
            }
            else
            {
                LogError("Invalid max_age.", request);
                return Invalid(request, description: "Invalid max_age");
            }
        }

        var processed_max_age = request.Raw.Get(Constants.ProcessedMaxAge);
        if (processed_max_age.IsPresent())
        {
            request.MaxAge = null;
            // TODO - Consider adding an OriginalMaxAge property for consistency with prompt.
        }

        //////////////////////////////////////////////////////////
        // check login_hint
        //////////////////////////////////////////////////////////
        var loginHint = request.Raw.Get(OidcConstants.AuthorizeRequest.LoginHint);
        if (loginHint.IsPresent())
        {
            if (loginHint.Length > _options.InputLengthRestrictions.LoginHint)
            {
                LogError("Login hint too long", request);
                return Invalid(request, description: "Invalid login_hint");
            }

            request.LoginHint = loginHint;
        }

        //////////////////////////////////////////////////////////
        // check acr_values
        //////////////////////////////////////////////////////////
        var acrValues = request.Raw.Get(OidcConstants.AuthorizeRequest.AcrValues);
        if (acrValues.IsPresent())
        {
            if (acrValues.Length > _options.InputLengthRestrictions.AcrValues)
            {
                LogError("Acr values too long", request);
                return Invalid(request, description: "Invalid acr_values");
            }

            request.AuthenticationContextReferenceClasses = acrValues.FromSpaceSeparatedString().Distinct().ToList();
        }

        //////////////////////////////////////////////////////////
        // check custom acr_values: idp
        //////////////////////////////////////////////////////////
        var idp = request.GetIdP();
        if (idp.IsPresent())
        {
            // if idp is present but client does not allow it, strip it from the request message
            if (request.Client.IdentityProviderRestrictions != null && request.Client.IdentityProviderRestrictions.Count > 0)
            {
                if (!request.Client.IdentityProviderRestrictions.Contains(idp))
                {
                    _sanitizedLogger.LogWarning("idp requested ({idp}) is not in client restriction list.", idp);
                    request.RemoveIdP();
                }
            }
        }

        //////////////////////////////////////////////////////////
        // session id
        //////////////////////////////////////////////////////////
        if (request.Subject.IsAuthenticated())
        {
            var sessionId = await _userSession.GetSessionIdAsync(ct);
            if (sessionId.IsPresent())
            {
                request.SessionId = sessionId;
            }
            else
            {
                LogError("SessionId is missing", request);
            }
        }
        else
        {
            request.SessionId = ""; // empty string for anonymous users
        }

        //////////////////////////////////////////////////////////
        // DPoP
        //////////////////////////////////////////////////////////
        if (!ValidateDpopThumbprint(request))
        {
            return Invalid(request, description: "Invalid dpop_jkt");
        }

        return Valid(request);
    }


    private bool ValidateDpopThumbprint(ValidatedAuthorizeRequest request)
    {
        var dpop_jkt = request.Raw.Get(OidcConstants.AuthorizeRequest.DPoPKeyThumbprint);
        if (dpop_jkt.IsPresent())
        {
            if (dpop_jkt.Length > _options.InputLengthRestrictions.DPoPKeyThumbprint)
            {
                LogError("dpop_jwt value too long", request);
                return false;
            }

            request.DPoPKeyThumbprint = dpop_jkt;
        }

        return true;
    }

    private static AuthorizeRequestValidationResult Invalid(ValidatedAuthorizeRequest request, string error = OidcConstants.AuthorizeErrors.InvalidRequest, string? description = null) => new AuthorizeRequestValidationResult(request, error, description);

    private static AuthorizeRequestValidationResult Valid(ValidatedAuthorizeRequest request) => new AuthorizeRequestValidationResult(request);

    private void LogError(string message, ValidatedAuthorizeRequest request)
    {
        var requestDetails = new AuthorizeRequestValidationLog(request, _options.Logging.AuthorizeRequestSensitiveValuesFilter);
        _sanitizedLogger.LogError(message + "\n{@requestDetails}", requestDetails);
    }

    private void LogError(string message, string? detail, ValidatedAuthorizeRequest request)
    {
        var requestDetails = new AuthorizeRequestValidationLog(request, _options.Logging.AuthorizeRequestSensitiveValuesFilter);
        _sanitizedLogger.LogError(message + ": {detail}\n{@requestDetails}", detail, requestDetails);
    }
}
