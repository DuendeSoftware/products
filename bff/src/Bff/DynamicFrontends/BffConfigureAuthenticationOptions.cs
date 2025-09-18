// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Duende.Bff.DynamicFrontends;

internal class BffConfigureAuthenticationOptions : IPostConfigureOptions<AuthenticationOptions>
{
    public void PostConfigure(string? name, AuthenticationOptions options)
    {
        if (options.DefaultScheme == null
            && options.DefaultAuthenticateScheme == null
            && options.DefaultSignOutScheme == null
            )
        {
            options.DefaultScheme = BffAuthenticationSchemes.BffCookie;
            options.DefaultChallengeScheme = BffAuthenticationSchemes.BffOpenIdConnect;
            options.DefaultSignOutScheme = BffAuthenticationSchemes.BffOpenIdConnect;

            // If we don't set this forbid scheme, when calling forbid, it can trigger a stackoverflow exception
            // when calling HttpContext.Forbid(). 
            if (options.DefaultForbidScheme == null)
            {
                options.DefaultForbidScheme = BffAuthenticationSchemes.BffCookie;
            }
        }
    }
}
