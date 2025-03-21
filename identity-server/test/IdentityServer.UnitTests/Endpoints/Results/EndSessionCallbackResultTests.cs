// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using UnitTests.Common;

namespace UnitTests.Endpoints.Results;

public class EndSessionCallbackResultTests
{
    private EndSessionCallbackHttpWriter _subject;

    private EndSessionCallbackValidationResult _result = new EndSessionCallbackValidationResult();
    private IdentityServerOptions _options = TestIdentityServerOptions.Create();

    private DefaultHttpContext _context = new DefaultHttpContext();

    public EndSessionCallbackResultTests()
    {
        _context.Request.Scheme = "https";
        _context.Request.Host = new HostString("server");
        _context.Response.Body = new MemoryStream();

        _subject = new EndSessionCallbackHttpWriter(_options);
    }

    [Fact]
    public async Task error_should_return_400()
    {
        _result.IsError = true;

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task success_should_render_html_and_iframes()
    {
        _result.IsError = false;
        _result.FrontChannelLogoutUrls = new string[] { "http://foo.com", "http://bar.com" };

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.ContentType.ShouldStartWith("text/html");
        _context.Response.Headers.CacheControl.First().ShouldContain("no-store");
        _context.Response.Headers.CacheControl.First().ShouldContain("no-cache");
        _context.Response.Headers.CacheControl.First().ShouldContain("max-age=0");
        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain("default-src 'none';");
        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain("style-src 'sha256-e6FQZewefmod2S/5T11pTXjzE2vn3/8GRwWOs917YE4=';");
        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain("frame-src http://foo.com http://bar.com");
        _context.Response.Headers["X-Content-Security-Policy"].First().ShouldContain("default-src 'none';");
        _context.Response.Headers["X-Content-Security-Policy"].First().ShouldContain("style-src 'sha256-e6FQZewefmod2S/5T11pTXjzE2vn3/8GRwWOs917YE4=';");
        _context.Response.Headers["X-Content-Security-Policy"].First().ShouldContain("frame-src http://foo.com http://bar.com");
        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(_context.Response.Body);
        var html = await rdr.ReadToEndAsync();
        html.ShouldContain("<iframe loading='eager' allow='' src='http://foo.com'></iframe>");
        html.ShouldContain("<iframe loading='eager' allow='' src='http://bar.com'></iframe>");
    }

    [Fact]
    public async Task fsuccess_should_add_unsafe_inline_for_csp_level_1()
    {
        _result.IsError = false;

        _options.Csp.Level = CspLevel.One;

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain("style-src 'unsafe-inline' 'sha256-e6FQZewefmod2S/5T11pTXjzE2vn3/8GRwWOs917YE4='");
        _context.Response.Headers["X-Content-Security-Policy"].First().ShouldContain("style-src 'unsafe-inline' 'sha256-e6FQZewefmod2S/5T11pTXjzE2vn3/8GRwWOs917YE4='");
    }

    [Fact]
    public async Task form_post_mode_should_not_add_deprecated_header_when_it_is_disabled()
    {
        _result.IsError = false;

        _options.Csp.AddDeprecatedHeader = false;

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain("style-src 'sha256-e6FQZewefmod2S/5T11pTXjzE2vn3/8GRwWOs917YE4='");
        _context.Response.Headers["X-Content-Security-Policy"].ShouldBeEmpty();
    }
}
