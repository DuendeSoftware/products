// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SamplesShared;

namespace MfaStepUp;

internal sealed class SampleSecondFactorPasskeyResolver(
    IHttpContextAccessor httpContextAccessor)
    : ISecondFactorPasskeyAuthenticationResolver
{
    public async Task<UserSubjectId?> ResolveAsync(Ct ct)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        var authenticationResult = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authenticationResult.Succeeded || authenticationResult.Principal is null)
        {
            return null;
        }

        return authenticationResult.Principal.GetSubjectId();
    }
}
