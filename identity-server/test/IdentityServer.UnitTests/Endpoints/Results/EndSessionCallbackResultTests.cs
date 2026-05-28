// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.RegularExpressions;
using System.Xml;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Services;
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
        _result.SamlFrontChannelLogouts = [CreateRedirectMessage("https://sp.example.com/slo")];

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(_context.Response.Body);
        var html = await rdr.ReadToEndAsync();

        // Should render a redirect iframe with the destination and a signed query string
        var match = Regex.Match(html, @"<iframe loading='eager' allow='' src='(https://sp\.example\.com/slo\?[^']+)'></iframe>");
        match.Success.ShouldBeTrue("Expected redirect iframe with full URL");
        var src = match.Groups[1].Value;
        src.ShouldContain("SAMLRequest=");
        src.ShouldContain("SigAlg=");
        src.ShouldContain("Signature=");
    }

    [Fact]
    public async Task mixed_oidc_and_saml_logouts_should_render_all_iframes()
    {
        _result.IsError = false;
        _result.FrontChannelLogoutUrls = ["http://oidc-client.com/logout"];
        _result.SamlFrontChannelLogouts = [CreateRedirectMessage("https://sp.example.com/slo")];

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(_context.Response.Body);
        var html = await rdr.ReadToEndAsync();

        html.ShouldContain("<iframe loading='eager' allow='' src='http://oidc-client.com/logout'></iframe>");
        // SAML redirect iframe should have full signed query string
        var match = Regex.Match(html, @"<iframe loading='eager' allow='' src='(https://sp\.example\.com/slo\?[^']+)'></iframe>");
        match.Success.ShouldBeTrue("Expected SAML redirect iframe");
        match.Groups[1].Value.ShouldContain("SAMLRequest=");
    }

    [Fact]
    public async Task multiple_saml_logouts_should_render_multiple_iframes()
    {
        _result.IsError = false;
        _result.SamlFrontChannelLogouts =
        [
            CreateRedirectMessage("https://sp1.example.com/slo"),
            CreateRedirectMessage("https://sp2.example.com/slo")
        ];

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(_context.Response.Body);
        var html = await rdr.ReadToEndAsync();

        // Both redirect iframes
        var sp1Match = Regex.Match(html, @"<iframe loading='eager' allow='' src='(https://sp1\.example\.com/slo\?[^']+)'></iframe>");
        sp1Match.Success.ShouldBeTrue("Expected redirect iframe for sp1");
        sp1Match.Groups[1].Value.ShouldContain("SAMLRequest=");

        var sp2Match = Regex.Match(html, @"<iframe loading='eager' allow='' src='(https://sp2\.example\.com/slo\?[^']+)'></iframe>");
        sp2Match.Success.ShouldBeTrue("Expected redirect iframe for sp2");
        sp2Match.Groups[1].Value.ShouldContain("SAMLRequest=");
    }

    [Fact]
    public async Task SamlFrontChannelLogoutsShouldIncludeSelfInFrameSrc()
    {
        _result.IsError = false;
        _result.SamlFrontChannelLogouts = [CreateRedirectMessage("https://sp.example.com/slo")];

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        var csp = _context.Response.Headers.ContentSecurityPolicy.First();
        csp.ShouldContain("frame-src https://sp.example.com 'self'");
    }

    [Fact]
    public async Task OidcOnlyLogoutsShouldNotIncludeSelfInFrameSrc()
    {
        _result.IsError = false;
        _result.FrontChannelLogoutUrls = ["http://oidc-client.com/logout"];
        _result.SamlFrontChannelLogouts = [];

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        var csp = _context.Response.Headers.ContentSecurityPolicy.First();
        csp.ShouldContain("frame-src http://oidc-client.com");
        csp.ShouldNotContain("'self'");
    }

    [Fact]
    public async Task SamlRedirectLogoutWithExistingQueryStringShouldProduceValidUrl()
    {
        _result.IsError = false;
        _result.SamlFrontChannelLogouts = [CreateRedirectMessage("https://sp.example.com/slo?existing=value")];

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(_context.Response.Body);
        var html = await rdr.ReadToEndAsync();

        // The destination already has a query string, so SAML params should be appended with &
        var match = Regex.Match(html, @"<iframe loading='eager' allow='' src='(https://sp\.example\.com/slo\?[^']+)'></iframe>");
        match.Success.ShouldBeTrue("Expected redirect iframe with full URL");
        var src = match.Groups[1].Value;
        src.ShouldContain("existing=value&amp;SAMLRequest=");
        src.ShouldNotContain("?&");
        src.ShouldNotContain("??");
    }

    [Fact]
    public async Task saml_logout_with_unknown_binding_should_be_skipped()
    {
        _result.IsError = false;
        _result.SamlFrontChannelLogouts =
        [
            new SamlLogoutRequestContext(new OutboundSaml2Message
            {
                Name = SamlConstants.RequestProperties.SAMLRequest,
                Destination = "https://sp.example.com/slo",
                Binding = "urn:oasis:names:tc:SAML:2.0:bindings:UNKNOWN",
                Xml = CreateDummyXml(),
                RelayState = null
            }, "_req-id", "https://sp.example.com")
        ];

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_result), _context);

        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(_context.Response.Body);
        var html = await rdr.ReadToEndAsync();

        html.ShouldNotContain("https://sp.example.com/slo");
    }

    private static SamlLogoutRequestContext CreateRedirectMessage(string destination) => new(new OutboundSaml2Message
    {
        Name = SamlConstants.RequestProperties.SAMLRequest,
        Destination = destination,
        Binding = SamlConstants.Bindings.HttpRedirect,
        Xml = CreateDummyXml(),
        RelayState = null,
        SigningCertificate = TestCert.Load()
    }, "_req-id", "https://sp.example.com");

    private static XmlElement CreateDummyXml()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<samlp:LogoutRequest xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\" xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" ID=\"_test\" Version=\"2.0\" IssueInstant=\"2024-01-01T00:00:00Z\"><saml:Issuer>test</saml:Issuer></samlp:LogoutRequest>");
        return doc.DocumentElement!;
    }
}
