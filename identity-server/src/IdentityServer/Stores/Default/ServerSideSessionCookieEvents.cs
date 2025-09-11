// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Authentication.Cookies;

namespace Duende.IdentityServer.Stores.Default;

internal static class ServerSideSessionCookieEvents
{
    public static Task OnCheckSlidingExpiration(CookieSlidingExpirationContext context)
    {
        if (context.Properties.Items.ContainsKey(IdentityServerConstants.ForceCookieRenewalFlag) &&
            (context.Properties.ExpiresUtc == null || DateTimeOffset.UtcNow < context.Properties.ExpiresUtc))
        {
            context.ShouldRenew = true;
            _ = context.Properties.Items.Remove(IdentityServerConstants.ForceCookieRenewalFlag);
        }

        return Task.CompletedTask;
    }
}
