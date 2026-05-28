// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Xml;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Services;
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
        _validationResult.SamlFrontChannelLogouts = [CreateRedirectMessage("http://bar")];

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_validationResult), ctx);

        ctx.Response.Headers.ContentSecurityPolicy.First().ShouldContain("frame-src http://foo http://bar");
    }

    [Fact]
    public async Task relax_csp_options_should_prevent_frame_src_csp_headers()
    {
        _options.Authentication.RequireCspFrameSrcForSignout = false;
        _validationResult.FrontChannelLogoutUrls = new[] { "http://foo" };
        _validationResult.SamlFrontChannelLogouts = [CreateRedirectMessage("http://bar")];

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";

        await _subject.WriteHttpResponse(new EndSessionCallbackResult(_validationResult), ctx);

        ctx.Response.Headers.ContentSecurityPolicy.FirstOrDefault().ShouldBeNull();
    }

    private static SamlLogoutRequestContext CreateRedirectMessage(string destination) => new(new OutboundSaml2Message
    {
        Name = SamlConstants.RequestProperties.SAMLRequest,
        Destination = destination,
        Binding = SamlConstants.Bindings.HttpRedirect,
        Xml = CreateDummyXml(),
        RelayState = null
    }, "_req-id", "https://sp.example.com");

    private static XmlElement CreateDummyXml()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<samlp:LogoutRequest xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\" xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" ID=\"_test\" Version=\"2.0\" IssueInstant=\"2024-01-01T00:00:00Z\"><saml:Issuer>test</saml:Issuer></samlp:LogoutRequest>");
        return doc.DocumentElement!;
    }
}
