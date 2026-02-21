// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace UnitTests.Configuration;

public class CookieNameMigrationExtensionsTests
{
    /// <summary>
    /// Sends a request through the migration middleware and returns:
    /// - The cookie values visible to the downstream handler (captured during the request)
    /// - The Set-Cookie response headers
    /// </summary>
    private static async Task<(IRequestCookieCollection downstreamCookies, IHeaderDictionary responseHeaders)> InvokeMiddleware(
        string oldCookieName,
        string newCookieName,
        string[] requestCookies)
    {
        IRequestCookieCollection capturedCookies = null;

        using var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.Configure(app =>
                {
                    app.MigrateIdentityServerCookieName(oldCookieName, newCookieName);
                    app.Run(ctx =>
                    {
                        // Capture cookie values while the HttpContext is still alive
                        capturedCookies = ctx.Request.Cookies;
                        return Task.CompletedTask;
                    });
                });
            })
            .StartAsync();

        var testServer = host.GetTestServer();
        var context = await testServer.SendAsync(ctx =>
        {
            ctx.Request.Headers.Cookie = string.Join("; ", requestCookies);
        });

        return (capturedCookies, context.Response.Headers);
    }

    // --- Old cookie migrated to new name when only old is present ---

    [Fact]
    public async Task when_old_cookie_present_and_new_absent_request_is_patched_with_new_name()
    {
        const string oldName = "idsrv";
        const string newName = "__Host-idsrv";
        const string cookieValue = "encrypted-ticket-value";

        var (downstreamCookies, _) = await InvokeMiddleware(
            oldName, newName,
            [$"{oldName}={cookieValue}"]);

        // The downstream handler should see the value under the new cookie name
        downstreamCookies[newName].ShouldBe(cookieValue);
    }

    [Fact]
    public async Task when_old_cookie_present_and_new_absent_response_sets_new_cookie()
    {
        const string oldName = "idsrv";
        const string newName = "__Host-idsrv";
        const string cookieValue = "encrypted-ticket-value";

        var (_, responseHeaders) = await InvokeMiddleware(
            oldName, newName,
            [$"{oldName}={cookieValue}"]);

        var setCookieHeaders = responseHeaders["Set-Cookie"].ToList();
        setCookieHeaders.ShouldContain(h => h.StartsWith(newName + "="));
    }

    [Fact]
    public async Task when_old_cookie_present_and_new_absent_response_expires_old_cookie()
    {
        const string oldName = "idsrv";
        const string newName = "__Host-idsrv";
        const string cookieValue = "encrypted-ticket-value";

        var (_, responseHeaders) = await InvokeMiddleware(
            oldName, newName,
            [$"{oldName}={cookieValue}"]);

        var setCookieHeaders = responseHeaders["Set-Cookie"].ToList();
        // Old cookie should be deleted (expires=epoch / max-age=0)
        setCookieHeaders.ShouldContain(h => h.StartsWith(oldName + "=") && h.Contains("expires="));
    }

    [Fact]
    public async Task host_prefixed_new_cookie_has_secure_attribute()
    {
        const string oldName = "idsrv";
        const string newName = "__Host-idsrv";
        const string cookieValue = "encrypted-ticket-value";

        var (_, responseHeaders) = await InvokeMiddleware(
            oldName, newName,
            [$"{oldName}={cookieValue}"]);

        var newCookieHeader = responseHeaders["Set-Cookie"]
            .FirstOrDefault(h => h.StartsWith(newName + "="));

        newCookieHeader.ShouldNotBeNull();
        newCookieHeader.ShouldContain("secure", Case.Insensitive);
    }

    // --- When both cookies are present, no migration occurs ---

    [Fact]
    public async Task when_both_old_and_new_cookies_present_new_cookie_value_is_not_overwritten()
    {
        const string oldName = "idsrv";
        const string newName = "__Host-idsrv";
        const string oldValue = "old-encrypted-value";
        const string newValue = "new-encrypted-value";

        var (downstreamCookies, _) = await InvokeMiddleware(
            oldName, newName,
            [$"{oldName}={oldValue}", $"{newName}={newValue}"]);

        // New cookie should remain unchanged
        downstreamCookies[newName].ShouldBe(newValue);
    }

    [Fact]
    public async Task when_both_cookies_present_no_set_cookie_headers_are_emitted()
    {
        const string oldName = "idsrv";
        const string newName = "__Host-idsrv";

        var (_, responseHeaders) = await InvokeMiddleware(
            oldName, newName,
            [$"{oldName}=old-value", $"{newName}=new-value"]);

        responseHeaders["Set-Cookie"].Count.ShouldBe(0);
    }

    // --- When neither cookie is present, nothing happens ---

    [Fact]
    public async Task when_neither_cookie_present_downstream_sees_no_cookies()
    {
        const string oldName = "idsrv";
        const string newName = "__Host-idsrv";

        var (downstreamCookies, _) = await InvokeMiddleware(
            oldName, newName,
            []);

        downstreamCookies[oldName].ShouldBeNull();
        downstreamCookies[newName].ShouldBeNull();
    }

    [Fact]
    public async Task when_neither_cookie_present_no_set_cookie_headers_are_emitted()
    {
        const string oldName = "idsrv";
        const string newName = "__Host-idsrv";

        var (_, responseHeaders) = await InvokeMiddleware(
            oldName, newName,
            []);

        responseHeaders["Set-Cookie"].Count.ShouldBe(0);
    }

    // --- Argument validation ---

    [Fact]
    public void null_old_cookie_name_throws_argument_exception()
    {
        var app = new ApplicationBuilder(null!);
        Should.Throw<ArgumentException>(() => app.MigrateIdentityServerCookieName(null!, "__Host-idsrv"));
    }

    [Fact]
    public void null_new_cookie_name_throws_argument_exception()
    {
        var app = new ApplicationBuilder(null!);
        Should.Throw<ArgumentException>(() => app.MigrateIdentityServerCookieName("idsrv", null!));
    }

    [Fact]
    public void empty_old_cookie_name_throws_argument_exception()
    {
        var app = new ApplicationBuilder(null!);
        Should.Throw<ArgumentException>(() => app.MigrateIdentityServerCookieName("", "__Host-idsrv"));
    }

    [Fact]
    public void empty_new_cookie_name_throws_argument_exception()
    {
        var app = new ApplicationBuilder(null!);
        Should.Throw<ArgumentException>(() => app.MigrateIdentityServerCookieName("idsrv", ""));
    }
}
