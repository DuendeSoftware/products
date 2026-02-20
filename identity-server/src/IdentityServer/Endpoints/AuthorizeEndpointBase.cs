// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Logging.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Endpoints;

internal abstract class AuthorizeEndpointBase : IEndpointHandler
{
    private readonly IAuthorizeResponseGenerator _authorizeResponseGenerator;

    private readonly IEventService _events;
    private readonly IdentityServerOptions _options;

    private readonly IAuthorizeInteractionResponseGenerator _interactionGenerator;

    private readonly IAuthorizeRequestValidator _validator;

    private readonly IConsentMessageStore _consentResponseStore;
    private readonly IAuthorizationParametersMessageStore _authorizationParametersMessageStore;

    protected AuthorizeEndpointBase(
        IEventService events,
        ILogger<AuthorizeEndpointBase> logger,
        IdentityServerOptions options,
        IAuthorizeRequestValidator validator,
        IAuthorizeInteractionResponseGenerator interactionGenerator,
        IAuthorizeResponseGenerator authorizeResponseGenerator,
        IUserSession userSession,
        IConsentMessageStore consentResponseStore,
        IAuthorizationParametersMessageStore authorizationParametersMessageStore = null)
    {
        _events = events;
        _options = options;
        Logger = logger;
        _validator = validator;
        _interactionGenerator = interactionGenerator;
        _authorizeResponseGenerator = authorizeResponseGenerator;
        UserSession = userSession;
        _consentResponseStore = consentResponseStore;
        _authorizationParametersMessageStore = authorizationParametersMessageStore;
    }

    protected ILogger Logger { get; private set; }

    protected IUserSession UserSession { get; private set; }

    public abstract Task<IEndpointResult> ProcessAsync(HttpContext context);

    internal async Task<IEndpointResult> ProcessAuthorizeRequestAsync(NameValueCollection parameters, ClaimsPrincipal user, CT ct, bool checkConsentResponse = false)
    {
        if (user != null)
        {
            Logger.LogDebug("User in authorize request: {subjectId}", user.GetSubjectId());
        }
        else
        {
            Logger.LogDebug("No user present in authorize request");
        }

        if (checkConsentResponse && _authorizationParametersMessageStore != null)
        {
            var messageStoreId = parameters[Constants.AuthorizationParamsStore.MessageStoreIdParameterName];
            var entry = await _authorizationParametersMessageStore.ReadAsync(messageStoreId, ct);
            parameters = entry?.Data.FromFullDictionary() ?? new NameValueCollection();

            await _authorizationParametersMessageStore.DeleteAsync(messageStoreId, ct);
        }

        // validate request
        var result = await _validator.ValidateAsync(parameters, user);

        if (result.IsError)
        {
            return await CreateErrorResultAsync(
                "Request validation failed",
                result.ValidatedRequest,
                result.Error,
                result.ErrorDescription);
        }

        string consentRequestId = null;

        try
        {
            Message<ConsentResponse> consent = null;

            if (checkConsentResponse)
            {
                var consentRequest = new ConsentRequest(result.ValidatedRequest.Raw, user?.GetSubjectId());
                consentRequestId = consentRequest.Id;
                consent = await _consentResponseStore.ReadAsync(consentRequestId, ct);

                if (consent != null && consent.Data == null)
                {
                    return await CreateErrorResultAsync("consent message is missing data", result.ValidatedRequest);
                }
            }

            var request = result.ValidatedRequest;
            LogRequest(request);

            // determine user interaction
            var interactionResult = await _interactionGenerator.ProcessInteractionAsync(request, consent?.Data, ct);
            if (interactionResult.ResponseType == InteractionResponseType.Error)
            {
                return await CreateErrorResultAsync("Interaction generator error", request, interactionResult.Error, interactionResult.ErrorDescription, false);
            }

            if (interactionResult.ResponseType == InteractionResponseType.UserInteraction)
            {
                if (interactionResult.IsLogin)
                {
                    return new LoginPageResult(request, _options);
                }
                if (interactionResult.IsConsent)
                {
                    return new ConsentPageResult(request, _options);
                }
                if (interactionResult.IsRedirect)
                {
                    return new CustomRedirectResult(request, interactionResult.RedirectUrl, _options);
                }
                if (interactionResult.IsCreateAccount)
                {
                    return new CreateAccountPageResult(request, _options);
                }
            }

            var response = await _authorizeResponseGenerator.CreateResponseAsync(request, ct);

            await RaiseResponseEventAsync(response);

            LogResponse(response);

            return new AuthorizeResult(response);
        }
        finally
        {
            if (consentRequestId != null)
            {
                await _consentResponseStore.DeleteAsync(consentRequestId, ct);
            }
        }
    }

    protected async Task<IEndpointResult> CreateErrorResultAsync(
        string logMessage,
        ValidatedAuthorizeRequest request = null,
        string error = OidcConstants.AuthorizeErrors.ServerError,
        string errorDescription = null,
        bool logError = true)
    {
        if (logError)
        {
#pragma warning disable CA2254 // Structured logging is not needed for this message
            Logger.LogError(logMessage);
#pragma warning restore CA2254
        }

        if (request != null)
        {
            var details = new AuthorizeRequestValidationLog(request, _options.Logging.AuthorizeRequestSensitiveValuesFilter);
            Logger.LogInformation("{@validationDetails}", details);
        }

        // TODO: should we raise a token failure event for all errors to the authorize endpoint?
        await RaiseFailureEventAsync(request, error, errorDescription);

        return new AuthorizeResult(new AuthorizeResponse
        {
            Request = request,
            Error = error,
            ErrorDescription = errorDescription,
            SessionState = request?.GenerateSessionStateValue()
        });
    }

    private void LogRequest(ValidatedAuthorizeRequest request)
    {
        var details = new AuthorizeRequestValidationLog(request, _options.Logging.AuthorizeRequestSensitiveValuesFilter);
        Logger.LogDebug(nameof(ValidatedAuthorizeRequest) + Environment.NewLine + "{@validationDetails}", details);
    }

    private void LogResponse(AuthorizeResponse response)
    {
        var details = new AuthorizeResponseLog(response);
        Logger.LogDebug("Authorize endpoint response" + Environment.NewLine + "{@details}", details);
    }

    private void LogTokens(AuthorizeResponse response)
    {
        var clientId = $"{response.Request.ClientId} ({response.Request.Client.ClientName ?? "no name set"})";
        var subjectId = response.Request.Subject.GetSubjectId();

        if (response.IdentityToken != null)
        {
            Logger.LogTrace("Identity token issued for {clientId} / {subjectId}: {token}", clientId, subjectId, response.IdentityToken);
        }
        if (response.Code != null)
        {
            Logger.LogTrace("Code issued for {clientId} / {subjectId}: {token}", clientId, subjectId, response.Code);
        }
        if (response.AccessToken != null)
        {
            Logger.LogTrace("Access token issued for {clientId} / {subjectId}: {token}", clientId, subjectId, response.AccessToken);
        }
    }

    private Task RaiseFailureEventAsync(ValidatedAuthorizeRequest request, string error, string errorDescription)
    {
        Telemetry.Metrics.TokenIssuedFailure(
            request.ClientId,
            request.GrantType,
            request.AuthorizeRequestType,
            error);
        return _events.RaiseAsync(new TokenIssuedFailureEvent(request, error, errorDescription));
    }

    private Task RaiseResponseEventAsync(AuthorizeResponse response)
    {
        if (!response.IsError)
        {
            LogTokens(response);
            Telemetry.Metrics.TokenIssued(
                response.Request.ClientId,
                response.Request.GrantType,
                response.Request.AuthorizeRequestType,
                response.AccessToken.IsPresent(),
                response.AccessToken.IsPresent() ? response.Request.AccessTokenType : null,
                false,
                ProofType.None,
                response.IdentityToken.IsPresent());
            return _events.RaiseAsync(new TokenIssuedSuccessEvent(response));
        }

        return RaiseFailureEventAsync(response.Request, response.Error, response.ErrorDescription);
    }
}
