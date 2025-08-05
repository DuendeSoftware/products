// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Web;

public class LogoutEvents : CookieAuthenticationEvents
{
    public LogoutEvents(LogoutSessionManager logoutSessions) => LogoutSessions = logoutSessions;

    public LogoutSessionManager LogoutSessions { get; }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (context.Principal!.Identity!.IsAuthenticated)
        {
            var sub = context.Principal.FindFirst("sub")?.Value;
            var sid = context.Principal.FindFirst("sid")?.Value;

            if (LogoutSessions.IsLoggedOut(sub, sid))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync();

                // TODO - Revoke the user's refresh token. This is hard to do, because we have to retrieve it from
                // the principal's claims, but we are in the process of validating the principal (infinite loop!)
            }
        }
    }
}
