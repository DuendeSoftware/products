// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Duende.Bff.DynamicFrontends;

internal class BffConfigureAuthenticationOptions : IPostConfigureOptions<AuthenticationOptions>
{
    public void PostConfigure(string? name, AuthenticationOptions options)
    {
        if (options.DefaultScheme == null && options.DefaultAuthenticateScheme == null && options.DefaultAuthenticateScheme == null)
        {
            options.DefaultScheme = BffAuthenticationSchemes.BffDefault;
            options.DefaultChallengeScheme = BffAuthenticationSchemes.BffOpenIdConnect;
        }
    }
}
