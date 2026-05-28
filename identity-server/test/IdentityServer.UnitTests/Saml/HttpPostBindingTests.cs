// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Text;
using System.Xml;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace UnitTests.Saml;

public sealed class HttpPostBindingTests
{
    private const string Category = "SAML HTTP POST Binding Security";

    private static readonly Func<string, Ct, Task<Saml2Entity?>> NullResolver =
        (_, _) => Task.FromResult<Saml2Entity?>(null);

    private static HttpPostBinding CreateBinding() =>
        new(Options.Create(new IdentityServerOptions()));

    private static HttpRequest CreatePostRequest(string xmlPayload) =>
        CreatePostRequestWithRelayState(xmlPayload, null);

    private static HttpRequest CreatePostRequestWithRelayState(string xmlPayload, string? relayState)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(xmlPayload));
        var formData = new Dictionary<string, StringValues>
        {
            ["SAMLRequest"] = encoded
        };
        if (relayState != null)
        {
            formData["RelayState"] = relayState;
        }
        var form = new FormCollection(formData);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Form = form;
        context.Request.PathBase = "";
        context.Request.Path = "/saml";

        return context.Request;
    }

    private static OutboundSaml2Message CreateOutboundMessage()
    {
        var doc = new XmlDocument();
        doc.LoadXml("""<samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" ID="_test" Version="2.0" IssueInstant="2024-01-01T00:00:00Z"><saml:Issuer xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">https://idp.example.com</saml:Issuer></samlp:Response>""");
        return new OutboundSaml2Message
        {
            Name = "SAMLResponse",
            Xml = doc.DocumentElement!,
            Destination = "https://sp.example.com/acs",
            Binding = SamlConstants.Bindings.HttpPost
        };
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithValidSamlRequestShouldSucceed()
    {
        var binding = CreateBinding();
        var xml = """<samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" ID="_test" Version="2.0" IssueInstant="2024-01-01T00:00:00Z"/>""";
        var request = CreatePostRequest(xml);

        var result = await binding.UnBindAsync(request, NullResolver);

        result.Name.ShouldBe("SAMLRequest");
        result.Xml.LocalName.ShouldBe("AuthnRequest");
        result.Binding.ShouldBe("urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithXxePayloadShouldThrowXmlException()
    {
        var binding = CreateBinding();
        var xml = """<?xml version="1.0"?><!DOCTYPE root [<!ENTITY xxe SYSTEM "file:///etc/passwd">]><root>&xxe;</root>""";
        var request = CreatePostRequest(xml);

        var ex = await Should.ThrowAsync<XmlException>(() =>
            binding.UnBindAsync(request, NullResolver));

        ex.Message.ShouldBe(
            "Failed to parse XML document with secure settings. " +
            "The document may contain prohibited constructs (DTD, external entities) or be malformed.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithDtdPayloadShouldThrowXmlException()
    {
        var binding = CreateBinding();
        var xml = """<?xml version="1.0"?><!DOCTYPE root [<!ELEMENT root ANY>]><root>content</root>""";
        var request = CreatePostRequest(xml);

        var ex = await Should.ThrowAsync<XmlException>(() =>
            binding.UnBindAsync(request, NullResolver));

        ex.Message.ShouldBe(
            "Failed to parse XML document with secure settings. " +
            "The document may contain prohibited constructs (DTD, external entities) or be malformed.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithOversizedPayloadShouldThrow()
    {
        var options = new IdentityServerOptions();
        var maxSize = options.Saml.MaxMessageSize;
        var binding = new HttpPostBinding(Options.Create(options));
        var xml = "<root>" + new string('X', maxSize + 1) + "</root>";
        var request = CreatePostRequest(xml);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            binding.UnBindAsync(request, NullResolver));

        ex.Message.ShouldBe(
            $"SAML message exceeds maximum allowed size of {maxSize} characters.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithBillionLaughsPayloadShouldThrowXmlException()
    {
        var binding = CreateBinding();
        var xml = """
            <?xml version="1.0"?>
            <!DOCTYPE lolz [
              <!ENTITY lol "lol">
              <!ENTITY lol2 "&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;">
              <!ENTITY lol3 "&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;">
              <!ENTITY lol4 "&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;">
              <!ENTITY lol5 "&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;">
            ]>
            <root>&lol5;</root>
            """;
        var request = CreatePostRequest(xml);

        var ex = await Should.ThrowAsync<XmlException>(() =>
            binding.UnBindAsync(request, NullResolver));

        ex.Message.ShouldBe(
            "Failed to parse XML document with secure settings. " +
            "The document may contain prohibited constructs (DTD, external entities) or be malformed.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithRelayStateAtMaxLengthShouldSucceed()
    {
        var binding = CreateBinding();
        var xml = """<samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" ID="_test" Version="2.0" IssueInstant="2024-01-01T00:00:00Z"/>""";
        // 80 ASCII bytes = 80 bytes in UTF-8
        var relayState = new string('a', 80);
        var request = CreatePostRequestWithRelayState(xml, relayState);

        var result = await binding.UnBindAsync(request, NullResolver);

        result.RelayState.ShouldBe(relayState);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithRelayStateExceedingMaxLengthShouldThrow()
    {
        var options = new IdentityServerOptions();
        var binding = new HttpPostBinding(Options.Create(options));
        var xml = """<samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" ID="_test" Version="2.0" IssueInstant="2024-01-01T00:00:00Z"/>""";
        var oversizedRelayState = new string('X', options.Saml.MaxRelayStateLength + 1);
        var request = CreatePostRequestWithRelayState(xml, oversizedRelayState);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            binding.UnBindAsync(request, NullResolver));

        ex.Message.ShouldBe(
            $"RelayState exceeds maximum allowed size of {options.Saml.MaxRelayStateLength} bytes.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithMultibyteRelayStateExceedingMaxBytesShouldThrow()
    {
        var binding = CreateBinding();
        var xml = """<samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" ID="_test" Version="2.0" IssueInstant="2024-01-01T00:00:00Z"/>""";
        // 27 three-byte UTF-8 characters = 81 bytes, exceeds 80-byte limit
        var relayState = new string('€', 27);
        var request = CreatePostRequestWithRelayState(xml, relayState);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            binding.UnBindAsync(request, NullResolver));

        ex.Message.ShouldContain("RelayState exceeds maximum allowed size");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithNullRelayStateShouldSucceed()
    {
        var binding = CreateBinding();
        var xml = """<samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" ID="_test" Version="2.0" IssueInstant="2024-01-01T00:00:00Z"/>""";
        var request = CreatePostRequest(xml);

        var result = await binding.UnBindAsync(request, NullResolver);

        result.RelayState.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task BindAsyncSetsCspHeader()
    {
        var binding = CreateBinding();
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        await binding.BindAsync(ctx.Response, CreateOutboundMessage());

        ctx.Response.Headers["Content-Security-Policy"].ToString().ShouldContain("script-src");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task BindAsyncSetsCacheControlHeader()
    {
        var binding = CreateBinding();
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        await binding.BindAsync(ctx.Response, CreateOutboundMessage());

        ctx.Response.Headers.CacheControl.ToString().ShouldBe("no-cache, no-store");
    }
}
