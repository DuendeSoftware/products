// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

internal class DefaultIdentityServerInteractionService : IIdentityServerInteractionService
{
    private readonly TimeProvider _timeProvider;
    private readonly IHttpContextAccessor _context;
    private readonly IMessageStore<LogoutMessage> _logoutMessageStore;
    private readonly IMessageStore<ErrorMessage> _errorMessageStore;
    private readonly IConsentMessageStore _consentMessageStore;
    private readonly IPersistedGrantService _grants;
    private readonly IUserSession _userSession;
    private readonly ILogger _logger;
    private readonly ReturnUrlParser _returnUrlParser;

    public DefaultIdentityServerInteractionService(
        TimeProvider timeProvider,
        IHttpContextAccessor context,
        IMessageStore<LogoutMessage> logoutMessageStore,
        IMessageStore<ErrorMessage> errorMessageStore,
        IConsentMessageStore consentMessageStore,
        IPersistedGrantService grants,
        IUserSession userSession,
        ReturnUrlParser returnUrlParser,
        ILogger<DefaultIdentityServerInteractionService> logger)
    {
        _timeProvider = timeProvider;
        _context = context;
        _logoutMessageStore = logoutMessageStore;
        _errorMessageStore = errorMessageStore;
        _consentMessageStore = consentMessageStore;
        _grants = grants;
        _userSession = userSession;
        _returnUrlParser = returnUrlParser;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AuthorizationRequest> GetAuthorizationContextAsync(string returnUrl, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.GetAuthorizationContext");

        var result = await _returnUrlParser.ParseAsync(returnUrl, ct);

        if (result != null)
        {
            _logger.LogTrace("AuthorizationRequest being returned");
        }
        else
        {
            _logger.LogTrace("No AuthorizationRequest being returned");
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<LogoutRequest> GetLogoutContextAsync(string logoutId, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.GetLogoutContext");

        var msg = await _logoutMessageStore.ReadAsync(logoutId, ct);
        var iframeUrl = await _context.HttpContext.GetIdentityServerSignoutFrameCallbackUrlAsync(msg?.Data);
        return new LogoutRequest(iframeUrl, msg?.Data);
    }

    /// <inheritdoc/>
    public async Task<string> CreateLogoutContextAsync(Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.CreateLogoutContext");

        var user = await _userSession.GetUserAsync(ct);
        if (user != null)
        {
            var clientIds = await _userSession.GetClientListAsync(ct);
            var samlSessions = await _userSession.GetSamlSessionListAsync(ct);
            if (clientIds.Any() || samlSessions.Any())
            {
                var sid = await _userSession.GetSessionIdAsync(ct);
                var msg = new Message<LogoutMessage>(new LogoutMessage
                {
                    SubjectId = user.GetSubjectId(),
                    SessionId = sid,
                    ClientIds = clientIds,
                    SamlSessions = samlSessions
                }, _timeProvider.GetUtcNow().UtcDateTime);
                var id = await _logoutMessageStore.WriteAsync(msg, ct);
                return id;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<ErrorMessage> GetErrorContextAsync(string errorId, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.GetErrorContext");

        if (errorId != null)
        {
            var result = await _errorMessageStore.ReadAsync(errorId, ct);
            var data = result?.Data;
            if (data != null)
            {
                _logger.LogTrace("Error context loaded");
            }
            else
            {
                _logger.LogTrace("No error context found");
            }
            return data;
        }

        _logger.LogTrace("No error context found");

        return null;
    }

    /// <inheritdoc/>
    public async Task GrantConsentAsync(AuthorizationRequest request, ConsentResponse consent, Ct ct, string subject = null)
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
    public Task DenyAuthorizationAsync(AuthorizationRequest request, AuthorizationError error, Ct ct, string errorDescription = null)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.DenyAuthorization");

        var response = new ConsentResponse
        {
            Error = error,
            ErrorDescription = errorDescription
        };
        return GrantConsentAsync(request, response, ct);
    }

    public bool IsValidReturnUrl(string returnUrl)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.IsValidReturnUrl");

        var result = _returnUrlParser.IsValidReturnUrl(returnUrl);

        if (result)
        {
            _logger.LogTrace("IsValidReturnUrl true");
        }
        else
        {
            _logger.LogTrace("IsValidReturnUrl false");
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Grant>> GetAllUserGrantsAsync(Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.GetAllUserGrants");

        var user = await _userSession.GetUserAsync(ct);
        if (user != null)
        {
            var subject = user.GetSubjectId();
            return await _grants.GetAllGrantsAsync(subject, ct);
        }

        return Enumerable.Empty<Grant>();
    }

    /// <inheritdoc/>
    public async Task RevokeUserConsentAsync(string clientId, Ct ct)
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
