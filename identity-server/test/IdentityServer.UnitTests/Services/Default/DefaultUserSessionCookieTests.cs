// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using UnitTests.Common;

namespace UnitTests.Services.Default;

public class DefaultUserSessionCookieTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static (DefaultUserSession Subject, MockHttpContextAccessor Context, IdentityServerOptions Options) CreateSubject(
        string basePath,
        bool isHttps = false,
        string checkSessionCookieDomain = null,
        Microsoft.AspNetCore.Http.SameSiteMode sameSite = Microsoft.AspNetCore.Http.SameSiteMode.None)
    {
        var options = new IdentityServerOptions();
        options.Authentication.CheckSessionCookieDomain = checkSessionCookieDomain;
        options.Authentication.CheckSessionCookieSameSiteMode = sameSite;

        var mockAuthenticationHandlerProvider = new MockAuthenticationHandlerProvider();
        var mockAuthenticationHandler = new MockAuthenticationHandler();
        mockAuthenticationHandlerProvider.Handler = mockAuthenticationHandler;

        var mockContext = new MockHttpContextAccessor(options: options);
        mockContext.HttpContext.Request.Scheme = isHttps ? "https" : "http";

        var subject = new DefaultUserSession(
            mockContext,
            mockAuthenticationHandlerProvider,
            options,
            new FakeTimeProvider(),
            new MockServerUrls { Origin = "https://server", BasePath = basePath },
            TestLogger.Create<DefaultUserSession>());

        return (subject, mockContext, options);
    }

    private static SetCookieHeaderValue GetCookie(MockHttpContextAccessor context, string name)
    {
        var setCookieValues = context.HttpContext.Response.Headers.SetCookie;
        return SetCookieHeaderValue.ParseList(setCookieValues)
            .FirstOrDefault(x => x.Name.ToString() == name);
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData("/space-a", "/space-a")]
    [InlineData("/hosted/t/space-a", "/hosted/t/space-a")]
    [InlineData("/hosted/t/space-a/", "/hosted/t/space-a")]
    public void IssueSessionIdCookie_uses_resolved_base_path(string basePath, string expectedPath)
    {
        var (subject, context, options) = CreateSubject(basePath);

        subject.IssueSessionIdCookie("sid-123");

        var cookie = GetCookie(context, options.Authentication.CheckSessionCookieName);
        cookie.ShouldNotBeNull();
        cookie.Path.ToString().ShouldBe(expectedPath);
    }

    [Fact]
    public void IssueSessionIdCookie_over_https_marks_cookie_secure()
    {
        var (subject, context, options) = CreateSubject("/space-a", isHttps: true);

        subject.IssueSessionIdCookie("sid-123");

        var cookie = GetCookie(context, options.Authentication.CheckSessionCookieName);
        cookie.ShouldNotBeNull();
        cookie.Secure.ShouldBeTrue();
    }

    [Fact]
    public void IssueSessionIdCookie_over_http_does_not_mark_cookie_secure()
    {
        var (subject, context, options) = CreateSubject("/space-a", isHttps: false);

        subject.IssueSessionIdCookie("sid-123");

        var cookie = GetCookie(context, options.Authentication.CheckSessionCookieName);
        cookie.ShouldNotBeNull();
        cookie.Secure.ShouldBeFalse();
    }

    [Fact]
    public void IssueSessionIdCookie_preserves_configured_check_session_domain()
    {
        var (subject, context, options) = CreateSubject("/space-a", checkSessionCookieDomain: "dev.localhost");

        subject.IssueSessionIdCookie("sid-123");

        var cookie = GetCookie(context, options.Authentication.CheckSessionCookieName);
        cookie.ShouldNotBeNull();
        cookie.Domain.ToString().ShouldBe("dev.localhost");
    }

    [Fact]
    public void CreateSessionIdCookieOptions_preserves_configured_same_site_mode()
    {
        var (subject, _, options) = CreateSubject("/space-a", sameSite: Microsoft.AspNetCore.Http.SameSiteMode.Strict);

        var cookieOptions = subject.CreateSessionIdCookieOptions();

        cookieOptions.SameSite.ShouldBe(Microsoft.AspNetCore.Http.SameSiteMode.Strict);
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("/", "/")]
    [InlineData("/space-a", "/space-a")]
    [InlineData("/hosted/t/space-a", "/hosted/t/space-a")]
    public async Task RemoveSessionIdCookieAsync_uses_same_resolved_base_path_as_issue(string basePath, string expectedPath)
    {
        var (subject, context, options) = CreateSubject(basePath);

        // Removal only appends a Set-Cookie if the request currently carries the cookie.
        context.HttpContext.Request.Headers.Append("Cookie", $"{options.Authentication.CheckSessionCookieName}=sid-123");

        await subject.RemoveSessionIdCookieAsync(_ct);

        var cookie = GetCookie(context, options.Authentication.CheckSessionCookieName);
        cookie.ShouldNotBeNull();
        cookie.Path.ToString().ShouldBe(expectedPath);
        cookie.Expires.ShouldNotBeNull();
        cookie.Expires.Value.ShouldBeLessThan(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task RemoveSessionIdCookieAsync_preserves_configured_check_session_domain()
    {
        var (subject, context, options) = CreateSubject("/space-a", checkSessionCookieDomain: "dev.localhost");
        context.HttpContext.Request.Headers.Append("Cookie", $"{options.Authentication.CheckSessionCookieName}=sid-123");

        await subject.RemoveSessionIdCookieAsync(_ct);

        var cookie = GetCookie(context, options.Authentication.CheckSessionCookieName);
        cookie.ShouldNotBeNull();
        cookie.Domain.ToString().ShouldBe("dev.localhost");
    }

    [Fact]
    public void Issue_and_remove_resolve_to_the_same_path_for_the_same_base_path()
    {
        const string basePath = "/hosted/t/space-a";

        var (issueSubject, issueContext, options) = CreateSubject(basePath);
        issueSubject.IssueSessionIdCookie("sid-123");
        var issuedCookie = GetCookie(issueContext, options.Authentication.CheckSessionCookieName);

        var (removeSubject, removeContext, _) = CreateSubject(basePath);
        removeContext.HttpContext.Request.Headers.Append("Cookie", $"{options.Authentication.CheckSessionCookieName}=sid-123");

        removeSubject.RemoveSessionIdCookieAsync(_ct).GetAwaiter().GetResult();
        var removedCookie = GetCookie(removeContext, options.Authentication.CheckSessionCookieName);

        issuedCookie.Path.ToString().ShouldBe(removedCookie.Path.ToString());
    }
}
