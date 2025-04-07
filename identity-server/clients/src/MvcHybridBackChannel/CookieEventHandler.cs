// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MvcHybrid;

public class CookieEventHandler : CookieAuthenticationEvents
{
    public CookieEventHandler(LogoutSessionManager logoutSessions) => LogoutSessions = logoutSessions;

    public LogoutSessionManager LogoutSessions { get; }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (context.Principal.Identity.IsAuthenticated)
        {
            var sub = context.Principal.FindFirst("sub")?.Value;
            var sid = context.Principal.FindFirst("sid")?.Value;

            if (LogoutSessions.IsLoggedOut(sub, sid))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync();

                // todo: if we have a refresh token, it should be revoked here.
            }
        }
    }
}
