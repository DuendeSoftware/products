// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Authentication.Cookies;

namespace Duende.IdentityServer.Stores.Default;

internal static class ServerSideSessionCookieEvents
{
    public static Task OnCheckSlidingExpiration(CookieSlidingExpirationContext context, TimeProvider timeProvider)
    {
        if (context.Properties.Items.ContainsKey(IdentityServerConstants.ForceCookieRenewalFlag) &&
            (context.Properties.ExpiresUtc == null || timeProvider.GetUtcNow() < context.Properties.ExpiresUtc))
        {
            context.ShouldRenew = true;
            context.Properties.Items.Remove(IdentityServerConstants.ForceCookieRenewalFlag);
        }

        return Task.CompletedTask;
    }
}
