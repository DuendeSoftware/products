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

    public async Task<AuthorizationRequest> GetAuthorizationContextAsync(string returnUrl)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.GetAuthorizationContext");

        var result = await _returnUrlParser.ParseAsync(returnUrl);

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

    public async Task<LogoutRequest> GetLogoutContextAsync(string logoutId)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.GetLogoutContext");

        var msg = await _logoutMessageStore.ReadAsync(logoutId, default);
        var iframeUrl = await _context.HttpContext.GetIdentityServerSignoutFrameCallbackUrlAsync(msg?.Data);
        return new LogoutRequest(iframeUrl, msg?.Data);
    }

    public async Task<string> CreateLogoutContextAsync()
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.CreateLogoutContext");

        var user = await _userSession.GetUserAsync(default);
        if (user != null)
        {
            var clientIds = await _userSession.GetClientListAsync(default);
            if (clientIds.Any())
            {
                var sid = await _userSession.GetSessionIdAsync(default);
                var msg = new Message<LogoutMessage>(new LogoutMessage
                {
                    SubjectId = user.GetSubjectId(),
                    SessionId = sid,
                    ClientIds = clientIds
                }, _timeProvider.GetUtcNow().UtcDateTime);
                var id = await _logoutMessageStore.WriteAsync(msg, default);
                return id;
            }
        }

        return null;
    }

    public async Task<ErrorMessage> GetErrorContextAsync(string errorId)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.GetErrorContext");

        if (errorId != null)
        {
            var result = await _errorMessageStore.ReadAsync(errorId, default);
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

    public async Task GrantConsentAsync(AuthorizationRequest request, ConsentResponse consent, string subject = null)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.GrantConsent");

        if (subject == null)
        {
            var user = await _userSession.GetUserAsync(default);
            subject = user?.GetSubjectId();
        }

        if (subject == null && consent.Granted)
        {
            throw new ArgumentNullException(nameof(subject), "User is not currently authenticated, and no subject id passed");
        }

        var consentRequest = new ConsentRequest(request, subject);
        await _consentMessageStore.WriteAsync(consentRequest.Id, new Message<ConsentResponse>(consent, _timeProvider.GetUtcNow().UtcDateTime), default);
    }

    public Task DenyAuthorizationAsync(AuthorizationRequest request, AuthorizationError error, string errorDescription = null)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.DenyAuthorization");

        var response = new ConsentResponse
        {
            Error = error,
            ErrorDescription = errorDescription
        };
        return GrantConsentAsync(request, response);
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

    public async Task<IEnumerable<Grant>> GetAllUserGrantsAsync()
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.GetAllUserGrants");

        var user = await _userSession.GetUserAsync(default);
        if (user != null)
        {
            var subject = user.GetSubjectId();
            return await _grants.GetAllGrantsAsync(subject, default);
        }

        return Enumerable.Empty<Grant>();
    }

    public async Task RevokeUserConsentAsync(string clientId)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.RevokeUserConsent");

        var user = await _userSession.GetUserAsync(default);
        if (user != null)
        {
            var subject = user.GetSubjectId();
            await _grants.RemoveAllGrantsAsync(subject, clientId);
        }
    }

    public async Task RevokeTokensForCurrentSessionAsync()
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultIdentityServerInteractionService.RevokeTokensForCurrentSession");

        var user = await _userSession.GetUserAsync(default);
        if (user != null)
        {
            var subject = user.GetSubjectId();
            var sessionId = await _userSession.GetSessionIdAsync(default);
            await _grants.RemoveAllGrantsAsync(subject, sessionId: sessionId);
        }
    }
}
