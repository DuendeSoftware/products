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

    internal static async Task<string> GetIdentityServerSignoutFrameCallbackUrlAsync(this HttpContext context, LogoutMessage logoutMessage = null)
    {
        var userSession = context.RequestServices.GetRequiredService<IUserSession>();
        var user = await userSession.GetUserAsync();
        var currentSubId = user?.GetSubjectId();

        LogoutNotificationContext endSessionMsg = null;

        // if we have a logout message, then that take precedence over the current user
        if (logoutMessage?.ClientIds?.Any() == true)
        {
            var clientIds = logoutMessage.ClientIds;

            // check if current user is same, since we might have new clients (albeit unlikely)
            if (currentSubId == logoutMessage.SubjectId)
            {
                clientIds = clientIds.Union(await userSession.GetClientListAsync());
            }

            endSessionMsg = new LogoutNotificationContext
            {
                SubjectId = logoutMessage.SubjectId,
                SessionId = logoutMessage.SessionId,
                ClientIds = clientIds
            };
        }
        else if (currentSubId != null)
        {
            // see if current user has any clients they need to signout of 
            var clientIds = await userSession.GetClientListAsync();
            if (clientIds.Any())
            {
                endSessionMsg = new LogoutNotificationContext
                {
                    SubjectId = currentSubId,
                    SessionId = await userSession.GetSessionIdAsync(),
                    ClientIds = clientIds
                };
            }
        }

        if (endSessionMsg != null)
        {
            var clock = context.RequestServices.GetRequiredService<IClock>();
            var msg = new Message<LogoutNotificationContext>(endSessionMsg, clock.UtcNow.UtcDateTime);

            var endSessionMessageStore = context.RequestServices.GetRequiredService<IMessageStore<LogoutNotificationContext>>();
            var id = await endSessionMessageStore.WriteAsync(msg);

            var urls = context.RequestServices.GetRequiredService<IServerUrls>();
            var signoutIframeUrl = urls.BaseUrl.EnsureTrailingSlash() + ProtocolRoutePaths.EndSessionCallback;
            signoutIframeUrl = signoutIframeUrl.AddQueryString(Constants.UIConstants.DefaultRoutePathParams.EndSessionCallback, id);

            return signoutIframeUrl;
        }

        // no sessions, so nothing to cleanup
        return null;
    }
}
