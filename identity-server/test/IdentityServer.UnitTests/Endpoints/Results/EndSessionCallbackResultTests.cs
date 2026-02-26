// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.RegularExpressions;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Testing;
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

        _subject = new EndSessionCallbackHttpWriter(_options, new FakeLogger<EndSessionCallbackHttpWriter>());
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
        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain($"style-src '{IdentityServerConstants.ContentSecurityPolicyHashes.EndSessionStyle}';");
        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain("frame-src http://foo.com http://bar.com");
        _context.Response.Headers["X-Content-Security-Policy"].First().ShouldContain("default-src 'none';");
        _context.Response.Headers["X-Content-Security-Policy"].First().ShouldContain($"style-src '{IdentityServerConstants.ContentSecurityPolicyHashes.EndSessionStyle}';");
        _context.Response.Headers["X-Content-Security-Policy"].First().ShouldContain("frame-src http://foo.com http://bar.com");
        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(_context.Response.Body);
        var html = await rdr.ReadToEndAsync();
        html.ShouldContain("<iframe loading='eager' allow='' src='http://foo.com'></iframe>");
        html.ShouldContain("<iframe loading='eager' allow='' src='http://bar.com'></iframe>");
    }

    [Fact]
    public async Task csp_hash_should_match_inline_style()
    {
        _result.IsError = false;
        _result.FrontChannelLogoutUrls = new string[] { "http://foo.com", "http://bar.com" };

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.StatusCode.ShouldBe(200);
        _context.Response.ContentType.ShouldStartWith("text/html");
        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(_context.Response.Body);
        var html = await rdr.ReadToEndAsync();

        var match = Regex.Match(html, "<style>(.*?)</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        match.Success.ShouldBeTrue();

        var styleSha256 = "sha256-" + match.Groups[1].Value.ToSha256();
        IdentityServerConstants.ContentSecurityPolicyHashes.EndSessionStyle.ShouldContain(styleSha256);
    }

    [Fact]
    public async Task fsuccess_should_add_unsafe_inline_for_csp_level_1()
    {
        _result.IsError = false;

        _options.Csp.Level = CspLevel.One;

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain($"style-src 'unsafe-inline' '{IdentityServerConstants.ContentSecurityPolicyHashes.EndSessionStyle}'");
        _context.Response.Headers["X-Content-Security-Policy"].First().ShouldContain($"style-src 'unsafe-inline' '{IdentityServerConstants.ContentSecurityPolicyHashes.EndSessionStyle}'");
    }

    [Fact]
    public async Task form_post_mode_should_not_add_deprecated_header_when_it_is_disabled()
    {
        _result.IsError = false;

        _options.Csp.AddDeprecatedHeader = false;

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain($"style-src '{IdentityServerConstants.ContentSecurityPolicyHashes.EndSessionStyle}'");
        _context.Response.Headers["X-Content-Security-Policy"].ShouldBeEmpty();
    }

    [Fact]
    public async Task saml_http_redirect_logout_should_render_iframe()
    {
        _result.IsError = false;
        _result.SamlFrontChannelLogouts =
        [
            new MockSamlFrontChannelLogout
            {
                SamlBinding = SamlBinding.HttpRedirect,
                Destination = new Uri("https://sp.example.com/slo"),
                EncodedContent = "SAMLRequest=abc123&SigAlg=xyz&Signature=sig",
                RelayState = null
            }
        ];

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(_context.Response.Body);
        var html = await rdr.ReadToEndAsync();

        html.ShouldContain("<iframe loading='eager' allow='' src='https://sp.example.com/slo?SAMLRequest=abc123&amp;SigAlg=xyz&amp;Signature=sig'></iframe>");
    }

    [Fact]
    public async Task saml_http_post_logout_should_render_iframe_with_srcdoc()
    {
        _result.IsError = false;
        _result.SamlFrontChannelLogouts =
        [
            new MockSamlFrontChannelLogout
            {
                SamlBinding = SamlBinding.HttpPost,
                Destination = new Uri("https://sp.example.com/slo"),
                EncodedContent = "base64encodedlogoutrequest",
                RelayState = "state123"
            }
        ];

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(_context.Response.Body);
        var html = await rdr.ReadToEndAsync();

        html.ShouldContain("<iframe sandbox=");
        html.ShouldContain("srcdoc=");
        html.ShouldContain("https://sp.example.com/slo");
    }

    [Fact]
    public async Task mixed_oidc_and_saml_logouts_should_render_all_iframes()
    {
        _result.IsError = false;
        _result.FrontChannelLogoutUrls = ["http://oidc-client.com/logout"];
        _result.SamlFrontChannelLogouts =
        [
            new MockSamlFrontChannelLogout
            {
                SamlBinding = SamlBinding.HttpRedirect,
                Destination = new Uri("https://sp.example.com/slo"),
                EncodedContent = "SAMLRequest=abc",
                RelayState = null
            }
        ];

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(_context.Response.Body);
        var html = await rdr.ReadToEndAsync();

        html.ShouldContain("<iframe loading='eager' allow='' src='http://oidc-client.com/logout'></iframe>");
        html.ShouldContain("<iframe loading='eager' allow='' src='https://sp.example.com/slo?SAMLRequest=abc'></iframe>");
    }

    [Fact]
    public async Task multiple_saml_logouts_should_render_multiple_iframes()
    {
        _result.IsError = false;
        _result.SamlFrontChannelLogouts =
        [
            new MockSamlFrontChannelLogout
            {
                SamlBinding = SamlBinding.HttpRedirect,
                Destination = new Uri("https://sp1.example.com/slo"),
                EncodedContent = "SAMLRequest=sp1",
                RelayState = null
            },
            new MockSamlFrontChannelLogout
            {
                SamlBinding = SamlBinding.HttpPost,
                Destination = new Uri("https://sp2.example.com/slo"),
                EncodedContent = "base64sp2",
                RelayState = null
            }
        ];

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(_context.Response.Body);
        var html = await rdr.ReadToEndAsync();

        html.ShouldContain("https://sp1.example.com/slo");
        html.ShouldContain("https://sp2.example.com/slo");
    }

    [Fact]
    public async Task saml_logout_with_unknown_binding_should_be_skipped()
    {
        _result.IsError = false;
        _result.SamlFrontChannelLogouts =
        [
            new MockSamlFrontChannelLogout
            {
                SamlBinding = (SamlBinding)999, // Unknown binding
                Destination = new Uri("https://sp.example.com/slo"),
                EncodedContent = "content",
                RelayState = null
            }
        ];

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(_context.Response.Body);
        var html = await rdr.ReadToEndAsync();

        html.ShouldNotContain("https://sp.example.com/slo");
    }

    private class MockSamlFrontChannelLogout : ISamlFrontChannelLogout
    {
        public required SamlBinding SamlBinding { get; init; }
        public required Uri Destination { get; init; }
        public required string EncodedContent { get; init; }
        public required string RelayState { get; init; }
    }
}
