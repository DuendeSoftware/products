// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Text;
using System.Xml;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Saml.Bindings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace UnitTests.Saml;

public sealed class HttpPostBindingHtmlTests
{
    private const string Destination = "https://sp.example.com/acs";
    private const string SamlPayload = "<samlp:Response xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\">test</samlp:Response>";

    [Fact]
    public async Task BuildsHtmlWithCorrectFormAction()
    {
        var html = await BindAndExtractHtmlAsync(CreateMessage());

        html.ShouldContain($"action=\"{Destination}\"");
    }

    [Fact]
    public async Task Base64EncodesXmlPayload()
    {
        var message = CreateMessage();

        var html = await BindAndExtractHtmlAsync(message);

        var expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(message.Xml.OuterXml));
        html.ShouldContain(expectedBase64);
    }

    [Fact]
    public async Task IncludesRelayStateWhenProvided()
    {
        var html = await BindAndExtractHtmlAsync(CreateMessage(relayState: "https://sp.example.com/deep"));

        html.ShouldContain("RelayState");
        html.ShouldContain("https://sp.example.com/deep");
    }

    [Fact]
    public async Task OmitsRelayStateWhenNull()
    {
        var html = await BindAndExtractHtmlAsync(CreateMessage(relayState: null));

        html.ShouldNotContain("RelayState");
    }

    [Fact]
    public async Task SetsCorrectFormFieldName()
    {
        var html = await BindAndExtractHtmlAsync(CreateMessage());

        html.ShouldContain("name=\"SAMLResponse\"");
    }

    [Fact]
    public async Task ContainsAutoSubmitScript()
    {
        var html = await BindAndExtractHtmlAsync(CreateMessage());

        html.ShouldContain("document.forms.duendeSamlPostBindingSubmit.submit()");
    }

    [Fact]
    public async Task HtmlEncodesRelayStateToPreventXss()
    {
        var maliciousRelayState = "\"/><script>alert(1)</script><input value=\"";
        var html = await BindAndExtractHtmlAsync(CreateMessage(relayState: maliciousRelayState));

        html.ShouldNotContain("<script>");
        html.ShouldContain("&lt;script&gt;");
    }

    [Fact]
    public async Task HtmlEncodesRelayStateQuotesToPreventAttributeBreakout()
    {
        var relayStateWithQuote = "value\" onmouseover=\"alert(1)";
        var html = await BindAndExtractHtmlAsync(CreateMessage(relayState: relayStateWithQuote));

        html.ShouldNotContain("value\" onmouseover");
        html.ShouldContain("&quot;");
    }

    [Fact]
    public async Task HtmlEncodesDestinationToPreventXss()
    {
        var destinationWithSpecialChars = "https://sp.example.com/acs?a=1&b=2";
        var html = await BindAndExtractHtmlAsync(CreateMessage(destination: destinationWithSpecialChars));

        html.ShouldContain("a=1&amp;b=2");
        html.ShouldNotContain("a=1&b=2");
    }

    [Fact]
    public async Task HtmlEncodesMessageNameToPreventXss()
    {
        var doc = new XmlDocument();
        doc.LoadXml(SamlPayload);

        var message = new OutboundSaml2Message
        {
            Name = "\"><script>alert(1)</script><input name=\"",
            Xml = doc.DocumentElement!,
            Destination = Destination,
            Binding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"
        };

        var html = await BindAndExtractHtmlAsync(message);

        html.ShouldNotContain("<script>");
        html.ShouldContain("&lt;script&gt;");
    }

    private static async Task<string> BindAndExtractHtmlAsync(OutboundSaml2Message message)
    {
        var binding = new HttpPostBinding(Options.Create(new IdentityServerOptions()));
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        await binding.BindAsync(ctx.Response, message);

        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        return await new StreamReader(ctx.Response.Body).ReadToEndAsync();
    }

    private static OutboundSaml2Message CreateMessage(
        string? relayState = null,
        string? destination = null)
    {
        var doc = new XmlDocument();
        doc.LoadXml(SamlPayload);

        return new OutboundSaml2Message
        {
            Name = "SAMLResponse",
            Xml = doc.DocumentElement!,
            Destination = destination ?? Destination,
            RelayState = relayState,
            Binding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"
        };
    }
}
