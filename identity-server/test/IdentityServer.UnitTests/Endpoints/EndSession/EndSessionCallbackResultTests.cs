// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using System.Text.RegularExpressions;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Internal.Saml.SingleLogout;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Testing;

namespace UnitTests.Endpoints.EndSession;

public class EndSessionCallbackResultTests
{
    private const string Category = "End Session Callback Result";

    private readonly EndSessionCallbackValidationResult _validationResult;
    private readonly IdentityServerOptions _options;
    private readonly EndSessionCallbackHttpWriter _subject;

    public EndSessionCallbackResultTests()
    {
        _validationResult = new EndSessionCallbackValidationResult()
        {
            IsError = false,
        };
        _options = new IdentityServerOptions();
        _subject = new EndSessionCallbackHttpWriter(_options, new FakeLogger<EndSessionCallbackHttpWriter>());
    }

    [Fact]
    public async Task default_options_should_emit_frame_src_csp_headers()
    {
        _validationResult.FrontChannelLogoutUrls = new[] { "http://foo" };
        _validationResult.SamlFrontChannelLogouts = [new SamlHttpRedirectFrontChannelLogout(new Uri("http://bar"), string.Empty)];

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_validationResult), ctx);

        ctx.Response.Headers.ContentSecurityPolicy.First().ShouldContain("frame-src http://foo http://bar");
    }

    [Fact]
    public async Task default_options_should_emit_script_src_hash_for_saml_iframe_auto_post()
    {
        _validationResult.FrontChannelLogoutUrls = new[] { "http://foo" };
        _validationResult.SamlFrontChannelLogouts = [new SamlHttpPostFrontChannelLogout(new Uri("http://bar"), string.Empty, null)];

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_validationResult), ctx);

        ctx.Response.Headers.ContentSecurityPolicy.First().ShouldContain($"script-src '{IdentityServerConstants.ContentSecurityPolicyHashes.SamlAutoPostScript}'");
    }

    [Fact]
    public async Task csp_hash_should_match_inline_script()
    {
        _validationResult.SamlFrontChannelLogouts = [new SamlHttpPostFrontChannelLogout(new Uri("http://foo"), string.Empty, null)];

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Response.Body = new MemoryStream();

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_validationResult), ctx);

        ctx.Response.StatusCode.ShouldBe(200);
        ctx.Response.ContentType.ShouldStartWith("text/html");
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(ctx.Response.Body);
        var html = await rdr.ReadToEndAsync();

        var match = Regex.Match(html, "&lt;script&gt;(.*?)&lt;/script&gt;", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        match.Success.ShouldBeTrue();

        var scriptSha256 = "sha256-" + WebUtility.HtmlDecode(match.Groups[1].Value).ToSha256();
        scriptSha256.ShouldBe(IdentityServerConstants.ContentSecurityPolicyHashes.SamlAutoPostScript);
    }

    [Fact]
    public async Task relax_csp_options_should_prevent_frame_src_csp_headers()
    {
        _options.Authentication.RequireCspFrameSrcForSignout = false;
        _validationResult.FrontChannelLogoutUrls = new[] { "http://foo" };
        _validationResult.SamlFrontChannelLogouts = [new SamlHttpRedirectFrontChannelLogout(new Uri("http://bar"), string.Empty)];

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_validationResult), ctx);

        ctx.Response.Headers.ContentSecurityPolicy.FirstOrDefault().ShouldBeNull();
    }
}
