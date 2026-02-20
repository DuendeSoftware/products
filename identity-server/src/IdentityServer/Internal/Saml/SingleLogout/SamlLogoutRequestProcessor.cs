// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Claims;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleLogout.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LogoutRequest = Duende.IdentityServer.Internal.Saml.SingleLogout.Models.LogoutRequest;

namespace Duende.IdentityServer.Internal.Saml.SingleLogout;

internal class SamlLogoutRequestProcessor : SamlRequestProcessorBase<LogoutRequest, SamlLogoutRequest, SamlLogoutSuccess>
{
    private readonly IUserSession _userSession;
    private readonly LogoutResponseBuilder _logoutResponseBuilder;
    private readonly IMessageStore<LogoutMessage> _logoutMessageStore;
    private readonly TimeProvider _timeProvider;
    private readonly SamlUrlBuilder _urlBuilder;

    public SamlLogoutRequestProcessor(
        ISamlServiceProviderStore serviceProviderStore,
        IUserSession userSession,
        SamlRequestSignatureValidator<SamlLogoutRequest, LogoutRequest> signatureValidator,
        LogoutResponseBuilder logoutResponseBuilder,
        IServerUrls serverUrls,
        IOptions<SamlOptions> options,
        IMessageStore<LogoutMessage> logoutMessageStore,
        TimeProvider timeProvider,
        SamlUrlBuilder urlBuilder,
        SamlRequestValidator requestValidator,
        ILogger<SamlLogoutRequestProcessor> logger)
        : base(
            serviceProviderStore,
            options,
            requestValidator,
            signatureValidator,
            logger,
            serverUrls.GetAbsoluteUrl(options.Value.UserInteraction.Route + options.Value.UserInteraction.SingleLogoutPath))
    {
        _userSession = userSession;
        _logoutResponseBuilder = logoutResponseBuilder;
        _logoutMessageStore = logoutMessageStore;
        _timeProvider = timeProvider;
        _urlBuilder = urlBuilder;
    }

    protected override async Task<Result<SamlLogoutSuccess, SamlRequestError<SamlLogoutRequest>>> ProcessValidatedRequestAsync(
        SamlServiceProvider sp,
        SamlLogoutRequest request,
        CancellationToken ct)
    {
        var logoutRequest = request.LogoutRequest;

        if (sp.SingleLogoutServiceUrl == null)
        {
            Logger.SamlLogoutNoSingleLogoutServiceUrl(LogLevel.Error, sp.EntityId);
            return new SamlRequestError<SamlLogoutRequest>
            {
                Type = SamlRequestErrorType.Validation,
                ValidationMessage = $"Service Provider '{sp.EntityId}' has no SingleLogoutServiceUrl configured"
            };
        }

        Logger.ProcessingSamlLogoutRequest(LogLevel.Debug, logoutRequest.Id, sp.DisplayName, logoutRequest.Issuer);

        var user = await _userSession.GetUserAsync();
        if (user == null)
        {
            Logger.SamlLogoutRequestReceivedButNoActiveUserSession(LogLevel.Debug, logoutRequest.Id, logoutRequest.Issuer);
            var noUserAuthenticatedResponse = await _logoutResponseBuilder.BuildSuccessResponseAsync(logoutRequest.Id, sp, request.RelayState);
            // there is no user to log out, return success
            return SamlLogoutSuccess.CreateResponse(noUserAuthenticatedResponse);
        }

        var sessionMatch = await ValidateSessionIndexAsync(sp, logoutRequest.SessionIndex);
        if (!sessionMatch)
        {
            Logger.SamlLogoutRequestReceivedWithWrongSessionIndex(LogLevel.Warning, logoutRequest.Id, logoutRequest.SessionIndex);
            var noSessionIndexResponse = await _logoutResponseBuilder.BuildSuccessResponseAsync(logoutRequest.Id, sp, request.RelayState);
            // there is no session to terminate, return success
            return SamlLogoutSuccess.CreateResponse(noSessionIndexResponse);
        }

        Logger.SamlLogoutRedirectToLogoutPage(LogLevel.Information, logoutRequest.Issuer);

        var logoutId = await StoreLogoutMessageAsync(user, sp, request);
        var logoutUri = _urlBuilder.SamlLogoutUri(logoutId);

        return SamlLogoutSuccess.CreateRedirect(logoutUri);
    }

    protected override bool RequireSignature(SamlServiceProvider sp) =>
        // SAML 2.0 spec requires LogoutRequest to be signed
        true;

    protected override SamlRequestError<SamlLogoutRequest>? ValidateMessageSpecific(SamlServiceProvider sp, SamlLogoutRequest request)
    {
        var logoutRequest = request.LogoutRequest;

        // Validate NotOnOrAfter if present
        if (logoutRequest.NotOnOrAfter.HasValue)
        {
            var now = _timeProvider.GetUtcNow();
            var clockSkew = sp.ClockSkew ?? SamlOptions.DefaultClockSkew;

            if (now.Subtract(clockSkew) > logoutRequest.NotOnOrAfter.Value)
            {
                Logger.SamlLogoutRequestExpired(LogLevel.Warning, logoutRequest.Id, logoutRequest.NotOnOrAfter.Value);
                return new SamlRequestError<SamlLogoutRequest>
                {
                    Type = SamlRequestErrorType.Protocol,
                    ProtocolError = new SamlProtocolError<SamlLogoutRequest>(sp, request, new SamlError
                    {
                        StatusCode = SamlStatusCodes.Requester,
                        Message = "Logout request expired (NotOnOrAfter is in the past)"
                    })
                };
            }
        }

        return null;
    }

    private async Task<bool> ValidateSessionIndexAsync(SamlServiceProvider sp, string sessionIndex)
    {
        var samlSessions = await _userSession.GetSamlSessionListAsync();

        var spSession = samlSessions.FirstOrDefault(s => s.EntityId == sp.EntityId.ToString());

        if (spSession == null)
        {
            Logger.SamlLogoutNoSessionFoundForServiceProvider(LogLevel.Debug, sessionIndex, sp.EntityId);
            return false;
        }

        if (spSession.SessionIndex != sessionIndex)
        {
            Logger.SamlLogoutSessionIndexMisMatch(LogLevel.Debug, spSession.SessionIndex, sessionIndex);
            return false;
        }

        return true;
    }

    private async Task<string> StoreLogoutMessageAsync(ClaimsPrincipal user, SamlServiceProvider serviceProvider, SamlLogoutRequest logoutRequest)
    {
        var samlSessions = await _userSession.GetSamlSessionListAsync();

        var oidcClientIds = await _userSession.GetClientListAsync();

        var logoutMessage = new LogoutMessage
        {
            SubjectId = user.GetSubjectId(),
            SessionId = await _userSession.GetSessionIdAsync(),
            ClientIds = oidcClientIds,
            SamlServiceProviderEntityId = serviceProvider.EntityId,
            SamlSessions = samlSessions,
            SamlLogoutRequestId = logoutRequest.LogoutRequest.Id,
            SamlRelayState = logoutRequest.RelayState,
            PostLogoutRedirectUri = _urlBuilder.SamlLogoutCallBackUri().ToString()
        };

        var msg = new Message<LogoutMessage>(logoutMessage, _timeProvider.GetUtcNow().UtcDateTime);

        return await _logoutMessageStore.WriteAsync(msg);
    }
}
