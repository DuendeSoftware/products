// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Internal.Saml.SingleSignin;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace UnitTests.Saml;

public class SamlSigninStateIdCookieTests
{
    private static SamlSigninStateIdCookie CreateCookie(HttpContext httpContext, SamlOptions options = null)
    {
        var accessor = new StubHttpContextAccessor(httpContext);
        var samlOptions = options ?? new SamlOptions();
        return new SamlSigninStateIdCookie(accessor, Options.Create(samlOptions));
    }

    [Fact]
    public void StoreSamlSigninStateId_UsesDefaultCookieName()
    {
        var httpContext = new DefaultHttpContext();
        var cookie = CreateCookie(httpContext);

        cookie.StoreSamlSigninStateId(StateId.NewId());

        httpContext.Response.Headers.SetCookie.ToString().ShouldContain("__IdsSvr_SamlSigninState=");
    }

    [Fact]
    public void StoreSamlSigninStateId_UsesConfiguredCookieName()
    {
        var httpContext = new DefaultHttpContext();
        var options = new SamlOptions { SigninStateCookieName = "custom_cookie" };
        var cookie = CreateCookie(httpContext, options);

        cookie.StoreSamlSigninStateId(StateId.NewId());

        httpContext.Response.Headers.SetCookie.ToString().ShouldContain("custom_cookie=");
    }

    [Fact]
    public void TryGetSamlSigninStateId_UsesConfiguredCookieName()
    {
        var httpContext = new DefaultHttpContext();
        var stateId = StateId.NewId();
        httpContext.Request.Headers.Cookie = $"custom_cookie={stateId}";

        var options = new SamlOptions { SigninStateCookieName = "custom_cookie" };
        var cookie = CreateCookie(httpContext, options);

        var result = cookie.TryGetSamlSigninStateId(out _);

        result.ShouldBeTrue();
    }

    [Fact]
    public void ClearAuthenticationState_UsesConfiguredCookieName()
    {
        var httpContext = new DefaultHttpContext();
        var options = new SamlOptions { SigninStateCookieName = "custom_cookie" };
        var cookie = CreateCookie(httpContext, options);

        cookie.ClearAuthenticationState();

        httpContext.Response.Headers.SetCookie.ToString().ShouldContain("custom_cookie=");
    }

    private sealed class StubHttpContextAccessor(HttpContext httpContext) : IHttpContextAccessor
    {
        public HttpContext HttpContext { get; set; } = httpContext;
    }
}
