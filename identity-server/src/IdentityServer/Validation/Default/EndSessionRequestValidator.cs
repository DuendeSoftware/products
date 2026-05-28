// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Logging.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validates requests to the end session endpoint.
/// </summary>
public class EndSessionRequestValidator : IEndSessionRequestValidator
{
    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    ///  The IdentityServer options.
    /// </summary>
    protected readonly IdentityServerOptions Options;

    /// <summary>
    /// The token validator.
    /// </summary>
    protected readonly ITokenValidator TokenValidator;

    /// <summary>
    /// The URI validator.
    /// </summary>
    protected readonly IRedirectUriValidator UriValidator;

    /// <summary>
    /// The user session service.
    /// </summary>
    protected readonly IUserSession UserSession;

    /// <summary>
    /// The logout notification service.
    /// </summary>
    public ILogoutNotificationService LogoutNotificationService { get; }

    /// <summary>
    /// The SAML logout notification service.
    /// </summary>
    protected ISamlLogoutNotificationService SamlLogoutNotificationService { get; }

    /// <summary>
    /// The SAML logout session store.
    /// </summary>
    protected ISamlLogoutSessionStore SamlLogoutSessionStore { get; }

    /// <summary>
    /// The time provider.
    /// </summary>
    protected TimeProvider TimeProvider { get; }

    /// <summary>
    /// The end session message store.
    /// </summary>
    protected readonly IMessageStore<LogoutNotificationContext> EndSessionMessageStore;

    /// <summary>
    /// Creates a new instance of the EndSessionRequestValidator.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="tokenValidator"></param>
    /// <param name="uriValidator"></param>
    /// <param name="userSession"></param>
    /// <param name="logoutNotificationService"></param>
    /// <param name="samlLogoutNotificationService"></param>
    /// <param name="timeProvider"></param>
    /// <param name="endSessionMessageStore"></param>
    /// <param name="logger"></param>
    /// <param name="samlLogoutSessionStore"></param>
    public EndSessionRequestValidator(
        IdentityServerOptions options,
        ITokenValidator tokenValidator,
        IRedirectUriValidator uriValidator,
        IUserSession userSession,
        ILogoutNotificationService logoutNotificationService,
        ISamlLogoutNotificationService samlLogoutNotificationService,
        TimeProvider timeProvider,
        IMessageStore<LogoutNotificationContext> endSessionMessageStore,
        ILogger<EndSessionRequestValidator> logger,
        ISamlLogoutSessionStore samlLogoutSessionStore = null)
    {
        Options = options;
        TokenValidator = tokenValidator;
        UriValidator = uriValidator;
        UserSession = userSession;
        LogoutNotificationService = logoutNotificationService;
        SamlLogoutNotificationService = samlLogoutNotificationService;
        TimeProvider = timeProvider;
        EndSessionMessageStore = endSessionMessageStore;
        Logger = logger;
        SamlLogoutSessionStore = samlLogoutSessionStore;
    }

    /// <inheritdoc />
    public async Task<EndSessionValidationResult> ValidateAsync(NameValueCollection parameters, ClaimsPrincipal subject, Ct ct)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("EndSessionRequestValidator.Validate");

        Logger.LogDebug("Start end session request validation");

        var validatedRequest = new ValidatedEndSessionRequest
        {
            Raw = parameters
        };

        var uilocales = parameters.Get(OidcConstants.EndSessionRequest.UiLocales);
        if (uilocales.IsPresent())
        {
            if (uilocales.Length > Options.InputLengthRestrictions.UiLocale)
            {
                var log = new EndSessionRequestValidationLog(validatedRequest);
                Logger.LogWarning("UI locale too long. It will be ignored:{@details}", log);
            }
            else
            {
                validatedRequest.UiLocales = uilocales;
            }
        }

        var isAuthenticated = subject.IsAuthenticated();

        if (!isAuthenticated && Options.Authentication.RequireAuthenticatedUserForSignOutMessage)
        {
            return Invalid("User is anonymous. Ignoring end session parameters", validatedRequest);
        }

        var idTokenHint = parameters.Get(OidcConstants.EndSessionRequest.IdTokenHint);
        if (idTokenHint.IsPresent())
        {
            // validate id_token - no need to validate token life time
            var tokenValidationResult = await TokenValidator.ValidateIdentityTokenAsync(idTokenHint, null, false, ct);
            if (tokenValidationResult.IsError)
            {
                return Invalid("Error validating id token hint", validatedRequest);
            }

            validatedRequest.SetClient(tokenValidationResult.Client);

            if (isAuthenticated)
            {
                var sessionId = await UserSession.GetSessionIdAsync(ct);
                var hintValidationContext = new EndSessionHintValidationContext(subject, tokenValidationResult, sessionId);
                var hintResult = await ValidateIdTokenHintAsync(hintValidationContext, ct);

                switch (hintResult.Outcome)
                {
                    case EndSessionHintValidationOutcome.Invalid:
                        return Invalid(hintResult.ErrorMessage, validatedRequest);

                    case EndSessionHintValidationOutcome.Valid:
                    case EndSessionHintValidationOutcome.RequiresConfirmation:
                        validatedRequest.Subject = subject;
                        validatedRequest.SessionId = sessionId;
                        validatedRequest.ClientIds = await UserSession.GetClientListAsync(ct);
                        validatedRequest.SamlSessions = await UserSession.GetSamlSessionListAsync(ct);
                        if (hintResult.Outcome == EndSessionHintValidationOutcome.RequiresConfirmation)
                        {
                            validatedRequest.RequiresConfirmation = true;
                        }
                        break;
                }
            }

            var redirectUri = parameters.Get(OidcConstants.EndSessionRequest.PostLogoutRedirectUri);
            if (redirectUri.IsPresent())
            {
                if (await UriValidator.IsPostLogoutRedirectUriValidAsync(redirectUri, validatedRequest.Client, ct))
                {
                    validatedRequest.PostLogOutUri = redirectUri;
                }
                else
                {
                    Logger.LogWarning("Invalid PostLogoutRedirectUri: {postLogoutRedirectUri}", redirectUri);
                }
            }

            if (validatedRequest.PostLogOutUri != null)
            {
                var state = parameters.Get(OidcConstants.EndSessionRequest.State);
                if (state.IsPresent())
                {
                    validatedRequest.State = state;
                }
            }
        }
        else
        {
            // no id_token to authenticate the client, but we do have a user and a user session
            validatedRequest.Subject = subject;
            validatedRequest.SessionId = await UserSession.GetSessionIdAsync(ct);
            validatedRequest.ClientIds = await UserSession.GetClientListAsync(ct);

            var samlSessions = await UserSession.GetSamlSessionListAsync(ct);
            validatedRequest.SamlSessions = samlSessions;
        }

        LogSuccess(validatedRequest);

        return new EndSessionValidationResult
        {
            ValidatedRequest = validatedRequest,
            IsError = false
        };
    }

    /// <summary>
    /// Creates a result that indicates an error.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    protected virtual EndSessionValidationResult Invalid(string message, ValidatedEndSessionRequest request = null)
    {
        message = "End session request validation failure: " + message;
        if (request != null)
        {
            var log = new EndSessionRequestValidationLog(request);
            Logger.LogInformation("{Message}:{@details}", message, log);
        }
        else
        {
#pragma warning disable CA2254 // Structured logging is not needed for this message
            Logger.LogInformation(message);
#pragma warning restore CA2254
        }

        return new EndSessionValidationResult
        {
            IsError = true,
            Error = "Invalid request",
            ErrorDescription = message,
            ValidatedRequest = request
        };
    }

    /// <summary>
    /// Logs a success result.
    /// </summary>
    /// <param name="request"></param>
    protected virtual void LogSuccess(ValidatedEndSessionRequest request)
    {
        var log = new EndSessionRequestValidationLog(request);
        Logger.LogInformation("End session request validation success:{@details}", log);
    }

    /// <summary>
    /// Validates the id_token_hint's claims (sub/sid) against the current user session.
    /// Override this method to customize how the id_token_hint is matched to the session.
    /// </summary>
    /// <param name="context">
    /// The context containing the current authenticated user, the token validation result
    /// (with all token claims), and the current session ID.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// An <see cref="EndSessionHintValidationResult"/> indicating whether the hint is valid,
    /// invalid, or requires user confirmation.
    /// </returns>
    /// <remarks>
    /// The default implementation uses a sid-first strategy: if a <c>sid</c> claim is present
    /// in the token and the current session has a session ID, the two are compared. If no <c>sid</c>
    /// is present, or the current session has no session ID, the <c>sub</c> claim is compared
    /// against the authenticated user's subject ID as a fallback.
    /// If neither claim is present, the hint is treated as valid.
    /// <para>
    /// <b>Security note</b>: Returning <see cref="EndSessionHintValidationResult.Valid"/> unconditionally
    /// (i.e., accepting any id_token_hint regardless of sub/sid match) creates a cross-user logout
    /// vector. An attacker holding any valid id_token_hint can silently log out other users when the
    /// signout prompt is suppressed. Ensure custom overrides apply appropriate validation logic.
    /// </para>
    /// </remarks>
    protected virtual Task<EndSessionHintValidationResult> ValidateIdTokenHintAsync(
        EndSessionHintValidationContext context, Ct ct)
    {
        var sidClaim = context.TokenValidationResult.Claims
            .FirstOrDefault(c => c.Type == JwtClaimTypes.SessionId);

        if (sidClaim != null && context.SessionId != null)
        {
            // Sid-based comparison (preferred per OIDC RP-Initiated Logout spec)
            if (context.SessionId != sidClaim.Value)
            {
                return Task.FromResult(EndSessionHintValidationResult.Invalid(
                    "Session ID in id_token_hint does not match current session"));
            }
            return Task.FromResult(EndSessionHintValidationResult.Valid());
        }

        var subClaim = context.TokenValidationResult.Claims
            .FirstOrDefault(c => c.Type == JwtClaimTypes.Subject);

        if (subClaim != null)
        {
            // Sub-based comparison (fallback when no sid is present)
            if (context.Subject.GetSubjectId() != subClaim.Value)
            {
                return Task.FromResult(EndSessionHintValidationResult.Invalid(
                    "Current user does not match identity token"));
            }
        }

        return Task.FromResult(EndSessionHintValidationResult.Valid());
    }

    /// <inheritdoc />
    public async Task<EndSessionCallbackValidationResult> ValidateCallbackAsync(NameValueCollection parameters, Ct ct)
    {
        var result = new EndSessionCallbackValidationResult
        {
            IsError = true
        };

        var endSessionId = parameters[Constants.UIConstants.DefaultRoutePathParams.EndSessionCallback];
        var endSessionMessage = await EndSessionMessageStore.ReadAsync(endSessionId, ct);
        if (endSessionMessage?.Data?.ClientIds?.Count > 0 || endSessionMessage?.Data?.SamlSessions?.Count > 0)
        {
            result.IsError = false;
            result.FrontChannelLogoutUrls = await LogoutNotificationService.GetFrontChannelLogoutNotificationsUrlsAsync(endSessionMessage.Data, ct);

            var notificationResult = await SamlLogoutNotificationService.GetSamlFrontChannelLogoutsAsync(endSessionMessage.Data, ct);
            result.SamlFrontChannelLogouts = notificationResult.Messages;

            // Populate the logout session store for SAML-initiated logouts so that
            // SingleLogoutCallbackEndpoint can determine success/partial based on responses.
            if (endSessionMessage.Data.SamlLogoutId == null && notificationResult.Messages.Count > 0)
            {
                Logger.LogWarning(
                    "SAML front-channel logouts exist but SamlLogoutId is null. " +
                    "SP logout response tracking will be unavailable for this session");
            }

            if (endSessionMessage.Data.SamlLogoutId != null)
            {
                if (SamlLogoutSessionStore is null)
                {
                    Logger.LogError(
                        "SAML logout session tracking was requested but ISamlLogoutSessionStore is not registered. " +
                        "Ensure SAML support is configured via AddSaml().");
                    result.IsError = true;
                    result.Error = "SAML logout session store not configured";
                    return result;
                }

                var expectedResponses = new Dictionary<string, ExpectedSpLogout>();
                foreach (var requestContext in notificationResult.Messages)
                {
                    expectedResponses[requestContext.RequestId] = new ExpectedSpLogout(requestContext.SpEntityId);
                }

                // Always store the session, even with empty ExpectedResponses.
                // An empty session means no other SPs needed notification (single-SP case) → Success.
                // A missing session means tracking state was lost → PartialLogout.
                var session = new SamlLogoutSession
                {
                    LogoutId = endSessionMessage.Data.SamlLogoutId,
                    ExpectedResponses = expectedResponses,
                    SkippedSpCount = notificationResult.SkippedCount,
                    CreatedUtc = TimeProvider.GetUtcNow(),
                    ExpiresAtUtc = TimeProvider.GetUtcNow().Add(Options.Saml.LogoutSessionLifetime).UtcDateTime
                };
                await SamlLogoutSessionStore.StoreAsync(session, ct);
            }
        }
        else
        {
            result.Error = "Failed to read end session callback message";
        }

        return result;
    }
}
