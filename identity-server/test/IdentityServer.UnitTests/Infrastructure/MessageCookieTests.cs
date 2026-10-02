// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Microsoft.Net.Http.Headers;
using UnitTests.Common;

namespace UnitTests.Infrastructure;

public class MessageCookieTests
{
    private readonly IdentityServerOptions _options = new IdentityServerOptions();

    private (MessageCookie<string> Subject, MockHttpContextAccessor Context) CreateSubject(
        string basePath, bool isHttps = false)
    {
        var mockContext = new MockHttpContextAccessor(
            options: _options,
            urls: new MockServerUrls { Origin = "https://server", BasePath = basePath });

        mockContext.HttpContext.Request.Scheme = isHttps ? "https" : "http";

        var subject = new MessageCookie<string>(
            TestLogger.Create<MessageCookie<string>>(),
            _options,
            mockContext,
            new MockServerUrls { Origin = "https://server", BasePath = basePath },
            new StubDataProtectionProvider());

        return (subject, mockContext);
    }

    private static SetCookieHeaderValue GetCookie(MockHttpContextAccessor context, string name)
    {
        var setCookieValues = context.HttpContext.Response.Headers.SetCookie;
        return SetCookieHeaderValue.ParseList(setCookieValues)
            .FirstOrDefault(x => x.Name.ToString().StartsWith(name, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData("/space-a", "/space-a")]
    [InlineData("/hosted/t/space-a", "/hosted/t/space-a")]
    [InlineData("/hosted/t/space-a/", "/hosted/t/space-a")]
    public void Write_issues_cookie_with_resolved_base_path(string basePath, string expectedPath)
    {
        var (subject, context) = CreateSubject(basePath);

        subject.Write("id1", new Message<string>("payload", DateTime.UtcNow));

        var cookie = GetCookie(context, "String.id1");
        cookie.ShouldNotBeNull();
        cookie.Path.ToString().ShouldBe(expectedPath);
    }

    [Fact]
    public void Write_over_https_marks_cookie_secure()
    {
        var (subject, context) = CreateSubject("/space-a", isHttps: true);

        subject.Write("id1", new Message<string>("payload", DateTime.UtcNow));

        var cookie = GetCookie(context, "String.id1");
        cookie.ShouldNotBeNull();
        cookie.Secure.ShouldBeTrue();
    }

    [Fact]
    public void Write_over_http_does_not_mark_cookie_secure()
    {
        var (subject, context) = CreateSubject("/space-a", isHttps: false);

        subject.Write("id1", new Message<string>("payload", DateTime.UtcNow));

        var cookie = GetCookie(context, "String.id1");
        cookie.ShouldNotBeNull();
        cookie.Secure.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("/", "/")]
    [InlineData("/space-a", "/space-a")]
    [InlineData("/hosted/t/space-a", "/hosted/t/space-a")]
    public void Clear_uses_same_resolved_base_path_as_write(string basePath, string expectedPath)
    {
        var (subject, context) = CreateSubject(basePath);

        subject.Clear("id1");

        var cookie = GetCookie(context, "String.id1");
        cookie.ShouldNotBeNull();
        cookie.Path.ToString().ShouldBe(expectedPath);
        // clear/delete cookies are expired far in the past.
        cookie.Expires.ShouldNotBeNull();
        cookie.Expires.Value.ShouldBeLessThan(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Clear_path_matches_write_path_for_same_base_path()
    {
        const string basePath = "/hosted/t/space-a";

        var (writeSubject, writeContext) = CreateSubject(basePath);
        writeSubject.Write("id1", new Message<string>("payload", DateTime.UtcNow));
        var writtenCookie = GetCookie(writeContext, "String.id1");

        var (clearSubject, clearContext) = CreateSubject(basePath);
        clearSubject.Clear("id1");
        var clearedCookie = GetCookie(clearContext, "String.id1");

        writtenCookie.Path.ToString().ShouldBe(clearedCookie.Path.ToString());
    }
}
