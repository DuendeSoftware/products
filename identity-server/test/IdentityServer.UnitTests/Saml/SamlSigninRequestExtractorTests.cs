// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.IO.Compression;
using System.Text;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleSignin;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Saml;

public class SamlSigninRequestExtractorTests
{
    private const string Category = "SAML Signin Request Extractor";

    private readonly SamlSigninRequestExtractor _extractor =
        new(new AuthNRequestParser(NullLogger<AuthNRequestParser>.Instance));

    [Fact]
    [Trait("Category", Category)]
    public async Task malformed_xml_throws_saml_parse_exception()
    {
        var context = CreatePostContext("<this is not valid XML!!!");

        var ex = await Should.ThrowAsync<SamlParseException>(
            () => _extractor.ExtractAsync(context).AsTask());

        ex.ShouldNotBeNull();
        ex.ShouldBeOfType<SamlParseException>();
        ex.Issuer.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_required_attribute_throws_saml_parse_exception_with_issuer()
    {
        var xml = """
                  <?xml version="1.0" encoding="UTF-8"?>
                  <samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                      xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                      Version="2.0"
                                      IssueInstant="2024-01-01T00:00:00Z">
                      <saml:Issuer>https://sp.example.com</saml:Issuer>
                  </samlp:AuthnRequest>
                  """;

        var context = CreatePostContext(xml);

        var ex = await Should.ThrowAsync<SamlParseException>(
            () => _extractor.ExtractAsync(context).AsTask());

        ex.ShouldNotBeNull();
        ex.Issuer.ShouldBe("https://sp.example.com");
        ex.InnerException.ShouldBeOfType<FormatException>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_issuer_element_throws_saml_parse_exception_with_null_issuer()
    {
        var xml = """
                  <?xml version="1.0" encoding="UTF-8"?>
                  <samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                      xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                      ID="_test123"
                                      Version="2.0"
                                      IssueInstant="2024-01-01T00:00:00Z">
                  </samlp:AuthnRequest>
                  """;

        var context = CreatePostContext(xml);

        var ex = await Should.ThrowAsync<SamlParseException>(
            () => _extractor.ExtractAsync(context).AsTask());

        ex.ShouldNotBeNull();
        ex.Issuer.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task malformed_xml_never_throws_bad_http_request_exception()
    {
        var context = CreatePostContext("not xml at all");

        var ex = await Should.ThrowAsync<SamlParseException>(
            () => _extractor.ExtractAsync(context).AsTask());

        ex.ShouldNotBeOfType<BadHttpRequestException>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task malformed_xml_redirect_binding_throws_saml_parse_exception()
    {
        var context = CreateRedirectContext("<broken xml");

        var ex = await Should.ThrowAsync<SamlParseException>(
            () => _extractor.ExtractAsync(context).AsTask());

        ex.ShouldNotBeNull();
        ex.Issuer.ShouldBeNull();
    }

    private static HttpContext CreatePostContext(string samlXml)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(samlXml));
        var formContent = $"SAMLRequest={Uri.EscapeDataString(encoded)}";
        var bodyBytes = Encoding.UTF8.GetBytes(formContent);

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;

        return context;
    }

    private static HttpContext CreateRedirectContext(string samlXml)
    {
        var xmlBytes = Encoding.UTF8.GetBytes(samlXml);
        using var compressedStream = new MemoryStream();
        using (var deflate = new DeflateStream(compressedStream, CompressionMode.Compress))
        {
            deflate.Write(xmlBytes, 0, xmlBytes.Length);
        }
        var encoded = Convert.ToBase64String(compressedStream.ToArray());

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.QueryString = new QueryString($"?SAMLRequest={Uri.EscapeDataString(encoded)}");

        return context;
    }
}
