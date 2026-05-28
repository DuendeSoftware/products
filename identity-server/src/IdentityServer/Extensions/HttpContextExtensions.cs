// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using static Duende.IdentityServer.IdentityServerConstants;

#pragma warning disable 1591

namespace Duende.IdentityServer.Extensions;

public static class HttpContextExtensions
{
    internal static void SetSignOutCalled(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Items[Constants.EnvironmentKeys.SignOutCalled] = "true";
    }

    internal static bool GetSignOutCalled(this HttpContext context) => context.Items.ContainsKey(Constants.EnvironmentKeys.SignOutCalled);

    internal static void SetBackChannelLogoutTriggered(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Items[Constants.EnvironmentKeys.BackChannelLogoutTriggered] = "true";
    }

    internal static bool GetBackChannelLogoutTriggered(this HttpContext context) => context.Items.ContainsKey(Constants.EnvironmentKeys.BackChannelLogoutTriggered);

    internal static void SetExpiredUserSession(this HttpContext context, UserSession userSession)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Items[Constants.EnvironmentKeys.DetectedExpiredUserSession] = userSession;
    }

    internal static bool TryGetExpiredUserSession(this HttpContext context, out UserSession expiredUserSession)
    {
        expiredUserSession = null;
        if (context.Items.TryGetValue(Constants.EnvironmentKeys.DetectedExpiredUserSession, out var userSession))
        {
            expiredUserSession = userSession as UserSession;
        }

        return expiredUserSession != null;
    }

    /// <summary>
    /// Builds the signout iframe callback URL that triggers front-channel logout
    /// notifications to clients and SAML SPs.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="logoutMessage">The logout message, if one exists.</param>
    /// <param name="logoutId">The logout message store ID. When present and the logout
    /// was initiated by a SAML SP, this is stored in
    /// <see cref="LogoutNotificationContext.SamlLogoutId"/> so that the
    /// <c>EndSessionRequestValidator</c> can create a <c>SamlLogoutSession</c> to
    /// track SP logout responses. Only set for SAML-initiated logouts (when
    /// <see cref="LogoutMessage.SamlServiceProviderEntityId"/> is non-null).</param>
    internal static async Task<string> GetIdentityServerSignoutFrameCallbackUrlAsync(this HttpContext context, LogoutMessage logoutMessage = null, string logoutId = null)
    {
        var userSession = context.RequestServices.GetRequiredService<IUserSession>();
        var user = await userSession.GetUserAsync(context.RequestAborted);
        var currentSubId = user?.GetSubjectId();

        LogoutNotificationContext endSessionMsg = null;

        // if we have a logout message, then that take precedence over the current user
        if (logoutMessage?.ClientIds?.Count > 0 || logoutMessage?.SamlSessions?.Count > 0)
        {
            var clientIds = logoutMessage.ClientIds ?? [];
            var samlSessions = logoutMessage.SamlSessions?.ToList() ?? [];

            // check if current user is same, since we might have new clients (albeit unlikely)
            if (currentSubId == logoutMessage.SubjectId)
            {
                clientIds = clientIds.Union(await userSession.GetClientListAsync(context.RequestAborted)).ToArray();
                var currentSamlSessions = await userSession.GetSamlSessionListAsync(context.RequestAborted);
                samlSessions = samlSessions.Union(currentSamlSessions).ToList();
            }

            var samlEntityIds = samlSessions.Select(s => s.EntityId);
            if (await AnyClientHasFrontChannelLogout(clientIds) || await AnySamlServiceProviderHasFrontChannelLogout(samlEntityIds, context.RequestAborted))
            {
                endSessionMsg = new LogoutNotificationContext
                {
                    SubjectId = logoutMessage.SubjectId,
                    SessionId = logoutMessage.SessionId,
                    ClientIds = clientIds,
                    SamlSessions = samlSessions,
                    SamlInitiatingServiceProviderEntityId = logoutMessage.SamlServiceProviderEntityId,
                    SamlLogoutId = logoutMessage.SamlServiceProviderEntityId != null ? logoutId : null
                };
            }
        }
        else if (currentSubId != null)
        {
            // see if current user has any clients they need to signout of
            var clientIds = await userSession.GetClientListAsync(context.RequestAborted);
            var samlSessions = await userSession.GetSamlSessionListAsync(context.RequestAborted);
            var samlEntityIds = samlSessions.Select(s => s.EntityId);

            if ((clientIds.Count > 0 && await AnyClientHasFrontChannelLogout(clientIds)) ||
                (samlSessions.Count > 0 && await AnySamlServiceProviderHasFrontChannelLogout(samlEntityIds, context.RequestAborted)))
            {
                endSessionMsg = new LogoutNotificationContext
                {
                    SubjectId = currentSubId,
                    SessionId = await userSession.GetSessionIdAsync(context.RequestAborted),
                    ClientIds = clientIds,
                    SamlSessions = samlSessions
                };
            }
        }

        if (endSessionMsg != null)
        {
            var timeProvider = context.RequestServices.GetRequiredService<TimeProvider>();
            var msg = new Message<LogoutNotificationContext>(endSessionMsg, timeProvider.GetUtcNow().UtcDateTime);

            var endSessionMessageStore = context.RequestServices.GetRequiredService<IMessageStore<LogoutNotificationContext>>();
            var id = await endSessionMessageStore.WriteAsync(msg, context.RequestAborted);

            var urls = context.RequestServices.GetRequiredService<IServerUrls>();
            var signoutIframeUrl = urls.BaseUrl.EnsureTrailingSlash() + ProtocolRoutePaths.EndSessionCallback;
            signoutIframeUrl = signoutIframeUrl.AddQueryString(Constants.UIConstants.DefaultRoutePathParams.EndSessionCallback, id);

            return signoutIframeUrl;
        }

        // no sessions, so nothing to cleanup
        return null;

        async Task<bool> AnyClientHasFrontChannelLogout(IEnumerable<string> clientIds)
        {
            var clientStore = context.RequestServices.GetRequiredService<IClientStore>();
            foreach (var clientId in clientIds)
            {
                var client = await clientStore.FindEnabledClientByIdAsync(clientId, context.RequestAborted);
                if (client?.FrontChannelLogoutUri.IsPresent() == true)
                {
                    return true;
                }
            }

            return false;
        }

        async Task<bool> AnySamlServiceProviderHasFrontChannelLogout(IEnumerable<string> entityIds, Ct ct)
        {
            var serviceProviderStore = context.RequestServices.GetRequiredService<ISamlServiceProviderStore>();
            foreach (var entityId in entityIds)
            {
                var sp = await serviceProviderStore.FindByEntityIdAsync(entityId, ct);
                if (sp?.Enabled == true && sp.GetSingleLogoutServiceEndpoint(SamlBinding.HttpRedirect) != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
