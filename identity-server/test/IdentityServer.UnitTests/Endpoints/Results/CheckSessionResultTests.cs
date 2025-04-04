// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Http;

namespace UnitTests.Endpoints.Results;

public class CheckSessionResultTests
{
    private CheckSessionHttpWriter _subject;

    private IdentityServerOptions _options = new IdentityServerOptions();

    private DefaultHttpContext _context = new DefaultHttpContext();

    public CheckSessionResultTests()
    {
        _context.Request.Scheme = "https";
        _context.Request.Host = new HostString("server");
        _context.Response.Body = new MemoryStream();

        _options.Authentication.CheckSessionCookieName = "foobar";

        _subject = new CheckSessionHttpWriter(_options);
    }

    [Fact]
    public async Task should_pass_results_in_body()
    {
        await _subject.WriteHttpResponse(new CheckSessionResult(), _context);

        _context.Response.StatusCode.ShouldBe(200);
        _context.Response.ContentType.ShouldStartWith("text/html");
        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain("default-src 'none';");
        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain($"script-src '{IdentityServerConstants.ContentSecurityPolicyHashes.CheckSessionScript}'");
        _context.Response.Headers["X-Content-Security-Policy"].First().ShouldContain("default-src 'none';");
        _context.Response.Headers["X-Content-Security-Policy"].First().ShouldContain($"script-src '{IdentityServerConstants.ContentSecurityPolicyHashes.CheckSessionScript}'");
        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(_context.Response.Body);
        var html = await rdr.ReadToEndAsync();
        html.ShouldContain("<script id='cookie-name' type='application/json'>foobar</script>");
    }

    [Fact]
    public async Task form_post_mode_should_add_unsafe_inline_for_csp_level_1()
    {
        _options.Csp.Level = CspLevel.One;

        await _subject.WriteHttpResponse(new CheckSessionResult(), _context);

        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain($"script-src 'unsafe-inline' '{IdentityServerConstants.ContentSecurityPolicyHashes.CheckSessionScript}'");
        _context.Response.Headers["X-Content-Security-Policy"].First().ShouldContain($"script-src 'unsafe-inline' '{IdentityServerConstants.ContentSecurityPolicyHashes.CheckSessionScript}'");
    }

    [Fact]
    public async Task form_post_mode_should_not_add_deprecated_header_when_it_is_disabled()
    {
        _options.Csp.AddDeprecatedHeader = false;

        await _subject.WriteHttpResponse(new CheckSessionResult(), _context);

        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain($"script-src '{IdentityServerConstants.ContentSecurityPolicyHashes.CheckSessionScript}'");
        _context.Response.Headers["X-Content-Security-Policy"].ShouldBeEmpty();
    }

    [Theory]
    [InlineData("foobar")]
    [InlineData("morefoobar")]
    public async Task can_change_cached_cookiename(string cookieName)
    {
        _options.Authentication.CheckSessionCookieName = cookieName;
        await _subject.WriteHttpResponse(new CheckSessionResult(), _context);
        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(_context.Response.Body);
        var html = await rdr.ReadToEndAsync();
        html.ShouldContain($"<script id='cookie-name' type='application/json'>{cookieName}</script>");
    }
}
