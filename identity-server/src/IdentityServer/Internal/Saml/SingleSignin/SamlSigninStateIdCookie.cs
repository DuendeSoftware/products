// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin;

internal sealed class SamlSigninStateIdCookie(IHttpContextAccessor httpContextAccessor, IOptions<SamlOptions> options)
{
    private readonly string _cookieName = options.Value.SigninStateCookieName;
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromMinutes(5);

    private HttpContext HttpContext => httpContextAccessor.HttpContext
                                       ?? throw new InvalidOperationException("HttpContext is not available.");

    internal void StoreSamlSigninStateId(StateId stateId)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            // Note: Safari does not set the cookie on a redirect if this is set to Strict
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.Add(CookieLifetime)
        };

        HttpContext.Response.Cookies.Append(_cookieName, stateId.Value.ToString(), cookieOptions);
    }

    internal bool TryGetSamlSigninStateId([NotNullWhen(true)] out StateId? stateId)
    {
        stateId = null;

        if (!HttpContext.Request.Cookies.TryGetValue(_cookieName, out var rawStateId) || string.IsNullOrEmpty(rawStateId))
        {
            return false;
        }

        try
        {
            if (!Guid.TryParse(rawStateId, out var guid))
            {
                return false;
            }

            stateId = new StateId(guid);
            return true;
        }
#pragma warning disable CA1031
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
    }

    internal void ClearAuthenticationState() => HttpContext.Response.Cookies.Delete(_cookieName, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        IsEssential = true
    });
}
