// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

internal class DefaultIdentityServerInteractionService : IIdentityServerInteractionService
{
    private readonly IdentityServerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IHttpContextAccessor _context;
    private readonly IMessageStore<LogoutMessage> _logoutMessageStore;
    private readonly IMessageStore<ErrorMessage> _errorMessageStore;
    private readonly IConsentMessageStore _consentMessageStore;
    private readonly IPersistedGrantService _grants;
    private readonly IUserSession _userSession;
    private readonly ILogger _logger;
    private readonly ReturnUrlParser _returnUrlParser;
    private readonly ISamlSigninStateStore? _samlSigninStateStore;

    public DefaultIdentityServerInteractionService(
        IdentityServerOptions options,
        TimeProvider timeProvider,
        IHttpContextAccessor context,
        IMessageStore<LogoutMessage> logoutMessageStore,
        IMessageStore<ErrorMessage> errorMessageStore,
        IConsentMessageStore consentMessageStore,
        IPersistedGrantService grants,
        IUserSession userSession,
        ReturnUrlParser returnUrlParser,
        ILogger<DefaultIdentityServerInteractionService> logger,
        ISamlSigninStateStore? samlSigninStateStore = null)
    {
        _options = options;
        _timeProvider = timeProvider;
        _context = context;
        _logoutMessageStore = logoutMessageStore;
        _errorMessageStore = errorMessageStore;
        _consentMessageStore = consentMessageStore;
        _grants = grants;
        _userSession = userSession;
        _returnUrlParser = returnUrlParser;
        _logger = logger;
        _samlSigninStateStore = samlSigninStateStore;
    }

    /// <inheritdoc/>
    public async Task<IAuthenticationContext?> GetAuthenticationContextAsync(string? returnUrl, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.GetAuthenticationContext");

        if (!string.IsNullOrEmpty(returnUrl))
        {
            var result = await _returnUrlParser.ParseAsync(returnUrl, ct);
            if (result != null)
            {
                _logger.AuthenticationContextBeingReturned();
                return result;
            }
        }

        _logger.NoAuthenticationContextFound();
        return null;
    }

    /// <inheritdoc/>
    public async Task<AuthorizationRequest?> GetAuthorizationContextAsync(string? returnUrl, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.GetAuthorizationContext");

        if (string.IsNullOrEmpty(returnUrl))
        {
            _logger.NoAuthorizationRequestBeingReturned();
            return null;
        }

        var result = await _returnUrlParser.ParseAsync(returnUrl, ct) as AuthorizationRequest;

        if (result != null)
        {
            _logger.AuthorizationRequestBeingReturned();
        }
        else
        {
            _logger.NoAuthorizationRequestBeingReturned2();
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<LogoutRequest> GetLogoutContextAsync(string? logoutId, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.GetLogoutContext");

        // The protected value passed in (and returned from CreateLogoutContextAsync) is a handle
        // referencing the persisted LogoutMessage in the message store, not a SAML correlation ID.
        var logoutMessageHandle = logoutId;

        var msg = await _logoutMessageStore.ReadAsync(logoutMessageHandle, ct);
        var iframeUrl = await _context.HttpContext.GetIdentityServerSignoutFrameCallbackUrlAsync(msg?.Data);
        var logoutRequest = new LogoutRequest(iframeUrl, msg?.Data);

        // For SAML-initiated logouts, append the logout message handle to PostLogoutRedirectUri so the
        // SingleLogoutCallbackEndpoint can retrieve the logout message (and its SAML sessions) from the store.
        // The SAML logout correlation ID lives inside the stored LogoutMessage itself and is routed
        // separately into LogoutNotificationContext.SamlLogoutId.
        if (logoutRequest.SamlServiceProviderEntityId != null && logoutRequest.PostLogoutRedirectUri != null)
        {
            logoutRequest.PostLogoutRedirectUri = logoutRequest.PostLogoutRedirectUri
                .AddQueryString(_options.UserInteraction.LogoutIdParameter, logoutMessageHandle!);
        }

        return logoutRequest;
    }

    /// <inheritdoc/>
    public async Task<string?> CreateLogoutContextAsync(Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.CreateLogoutContext");

        var user = await _userSession.GetUserAsync(ct);
        if (user != null)
        {
            var clientIds = await _userSession.GetClientListAsync(ct);
            var samlSessions = await _userSession.GetSamlSessionListAsync(ct);
            if (clientIds.Count > 0 || samlSessions.Count > 0)
            {
                var sid = await _userSession.GetSessionIdAsync(ct);
                var msg = new Message<LogoutMessage>(new LogoutMessage
                {
                    SubjectId = user.GetSubjectId(),
                    SessionId = sid,
                    ClientIds = clientIds,
                    SamlSessions = samlSessions,
                    // Assign a correlation ID at creation time when there are downstream SAML sessions
                    // to notify, so the SAML logout session store can track SP responses.
                    SamlLogoutCorrelationId = samlSessions.Count > 0
                        ? CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Hex)
                        : null
                }, _timeProvider.GetUtcNow().UtcDateTime);
                var id = await _logoutMessageStore.WriteAsync(msg, ct);
                return id;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<ErrorMessage?> GetErrorContextAsync(string? errorId, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.GetErrorContext");

        if (errorId != null)
        {
            var result = await _errorMessageStore.ReadAsync(errorId, ct);
            var data = result?.Data;
            if (data != null)
            {
                _logger.ErrorContextLoaded();
            }
            else
            {
                _logger.NoErrorContextFound();
            }
            return data;
        }

        _logger.NoErrorContextFound2();

        return null;
    }

    /// <inheritdoc/>
    public async Task GrantConsentAsync(AuthorizationRequest request, ConsentResponse consent, Ct ct, string? subject = null)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.GrantConsent");

        if (subject == null)
        {
            var user = await _userSession.GetUserAsync(ct);
            subject = user?.GetSubjectId();
        }

        if (subject == null && consent.Granted)
        {
            throw new ArgumentNullException(nameof(subject), "User is not currently authenticated, and no subject id passed");
        }

        var consentRequest = new ConsentRequest(request, subject);
        await _consentMessageStore.WriteAsync(consentRequest.Id, new Message<ConsentResponse>(consent, _timeProvider.GetUtcNow().UtcDateTime), ct);
    }

    /// <inheritdoc/>
    public Task DenyAuthorizationAsync(AuthorizationRequest request, InteractionError error, Ct ct, string? errorDescription = null) =>
        DenyAuthenticationAsync(request, error, ct, errorDescription);

    /// <inheritdoc/>
    public async Task DenyAuthenticationAsync(IAuthenticationContext context, InteractionError error, Ct ct, string? errorDescription = null)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.DenyAuthentication");

        if (context is AuthorizationRequest request)
        {
            var response = new ConsentResponse
            {
                Error = error,
                ErrorDescription = errorDescription
            };
            await GrantConsentAsync(request, response, ct);
            return;
        }

        if (context is Saml.Models.SamlAuthenticationContext samlContext)
        {
            if (_samlSigninStateStore is null)
            {
                throw new InvalidOperationException(
                    "ISamlSigninStateStore is not registered. Ensure SAML support is configured via AddSaml().");
            }

            var state = await _samlSigninStateStore.RetrieveSigninRequestStateAsync(samlContext.StateId, ct);
            if (state is null)
            {
                _logger.SAMLSigninStateNotFoundOrExpiredFor(samlContext.StateId);
                return;
            }

            state.DenialError = error;
            state.DenialErrorDescription = errorDescription;
            await _samlSigninStateStore.UpdateSigninRequestStateAsync(samlContext.StateId, state, ct);

            _logger.RecordedSAMLAuthenticationDenialErrorForStateIdStateId(error, samlContext.StateId);
            return;
        }

        throw new InvalidOperationException(
            $"Unsupported authentication context type: {context.GetType().Name}.");
    }

    public bool IsValidReturnUrl(string? returnUrl)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.IsValidReturnUrl");

        var result = _returnUrlParser.IsValidReturnUrl(returnUrl ?? string.Empty);

        if (result)
        {
            _logger.IsValidReturnUrlTrue();
        }
        else
        {
            _logger.IsValidReturnUrlFalse();
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<Grant>> GetAllUserGrantsAsync(Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.GetAllUserGrants");

        var user = await _userSession.GetUserAsync(ct);
        if (user != null)
        {
            var subject = user.GetSubjectId();
            return await _grants.GetAllGrantsAsync(subject, ct);
        }

        return Array.Empty<Grant>();
    }

    /// <inheritdoc/>
    public async Task RevokeUserConsentAsync(string? clientId, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.RevokeUserConsent");

        var user = await _userSession.GetUserAsync(ct);
        if (user != null)
        {
            var subject = user.GetSubjectId();
            await _grants.RemoveAllGrantsAsync(subject, ct, clientId);
        }
    }

    /// <inheritdoc/>
    public async Task RevokeTokensForCurrentSessionAsync(Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.RevokeTokensForCurrentSession");

        var user = await _userSession.GetUserAsync(ct);
        if (user != null)
        {
            var subject = user.GetSubjectId();
            var sessionId = await _userSession.GetSessionIdAsync(ct);
            await _grants.RemoveAllGrantsAsync(subject, ct, sessionId: sessionId);
        }
    }
}
