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

    protected AuthorizeEndpointBase(
        IEventService events,
        ILogger<AuthorizeEndpointBase> logger,
        IdentityServerOptions options,
        IAuthorizeRequestValidator validator,
        IAuthorizeInteractionResponseGenerator interactionGenerator,
        IAuthorizeResponseGenerator authorizeResponseGenerator,
        IUserSession userSession,
        IConsentMessageStore consentResponseStore)
    {
        _events = events;
        _options = options;
        Logger = logger;
        _validator = validator;
        _interactionGenerator = interactionGenerator;
        _authorizeResponseGenerator = authorizeResponseGenerator;
        UserSession = userSession;
        _consentResponseStore = consentResponseStore;
    }

    protected ILogger Logger { get; private set; }

    protected IUserSession UserSession { get; private set; }

    public abstract Task<IEndpointResult> ProcessAsync(HttpContext context);

    internal async Task<IEndpointResult> ProcessAuthorizeRequestAsync(NameValueCollection parameters, ClaimsPrincipal user, Ct ct, bool checkConsentResponse = false)
    {
        if (user != null)
        {
            var subjectId = user.GetSubjectId();
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.UserInAuthorizeRequest(subjectId);
            }
        }
        else
        {
            Logger.NoUserPresentInAuthorizeRequest();
        }

        // validate request
        var result = await _validator.ValidateAsync(parameters, ct, user);

        if (result.IsError)
        {
            return await CreateErrorResultAsync(
                "Request validation failed",
                ct,
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
                    return await CreateErrorResultAsync("consent message is missing data", ct, result.ValidatedRequest);
                }
            }

            var request = result.ValidatedRequest;
            LogRequest(request);

            // determine user interaction
            var interactionResult = await _interactionGenerator.ProcessInteractionAsync(request, consent?.Data, ct);
            if (interactionResult.ResponseType == InteractionResponseType.Error)
            {
                return await CreateErrorResultAsync("Interaction generator error", ct, request, interactionResult.Error, interactionResult.ErrorDescription, false);
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

            await RaiseResponseEventAsync(response, ct);

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
        Ct ct,
        ValidatedAuthorizeRequest request = null,
        string error = OidcConstants.AuthorizeErrors.ServerError,
        string errorDescription = null,
        bool logError = true)
    {
        if (logError)
        {
            Logger.EndpointError(logMessage);
        }

        if (request != null)
        {
            var details = new AuthorizeRequestValidationLog(request, _options.Logging.AuthorizeRequestSensitiveValuesFilter);
            Logger.AuthorizeRequestValidationErrorDetails(details);
        }

        // TODO: should we raise a token failure event for all errors to the authorize endpoint?
        await RaiseFailureEventAsync(request, error, errorDescription, ct);

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
        if (!Logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        var details = new AuthorizeRequestValidationLog(request, _options.Logging.AuthorizeRequestSensitiveValuesFilter);
        Logger.ValidatedAuthorizeRequestDetails(details);
    }

    private void LogResponse(AuthorizeResponse response)
    {
        if (!Logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        var details = new AuthorizeResponseLog(response);
        Logger.AuthorizeEndpointResponseDetails(details);
    }

    private void LogTokens(AuthorizeResponse response)
    {
        var clientId = $"{response.Request.ClientId} ({response.Request.Client.ClientName ?? "no name set"})";
        var subjectId = response.Request.Subject.GetSubjectId();

        if (response.IdentityToken != null)
        {
            Logger.IdentityTokenIssuedFor(clientId, subjectId, response.IdentityToken);
        }
        if (response.Code != null)
        {
            Logger.CodeIssuedFor(clientId, subjectId, response.Code);
        }
        if (response.AccessToken != null)
        {
            Logger.AccessTokenIssuedFor(clientId, subjectId, response.AccessToken);
        }
    }

    private Task RaiseFailureEventAsync(ValidatedAuthorizeRequest request, string error, string errorDescription, Ct ct)
    {
        Telemetry.Metrics.TokenIssuedFailure(
            request.ClientId,
            request.GrantType,
            request.AuthorizeRequestType,
            error);
        return _events.RaiseAsync(new TokenIssuedFailureEvent(request, error, errorDescription), ct);
    }

    private Task RaiseResponseEventAsync(AuthorizeResponse response, Ct ct)
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
            return _events.RaiseAsync(new TokenIssuedSuccessEvent(response), ct);
        }

        return RaiseFailureEventAsync(response.Request, response.Error, response.ErrorDescription, ct);
    }
}
