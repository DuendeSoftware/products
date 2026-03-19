// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Text;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleLogout;
using Duende.IdentityServer.Internal.Saml.SingleLogout.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SamlBinding = Duende.IdentityServer.Models.SamlBinding;

namespace UnitTests.Saml;

public class SamlLogoutEndpointParseErrorTests
{
    private const string Category = "SAML Logout Endpoint Parse Error";

    [Fact]
    [Trait("Category", Category)]
    public async Task parse_error_with_known_issuer_returns_validation_problem_result()
    {
        var extractor = new AlwaysFailLogoutExtractor();
        var endpoint = CreateEndpointWithExtractor(extractor);
        var context = CreatePostContextWithIssuer("https://sp.example.com");

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<ValidationProblemResult>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task parse_error_never_returns_a_logout_response()
    {
        var extractor = new AlwaysFailLogoutExtractor();
        var endpoint = CreateEndpointWithExtractor(extractor);
        var context = CreatePostContextWithIssuer("https://sp.example.com");

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<ValidationProblemResult>();
        result.ShouldNotBeOfType<LogoutResponse>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task parse_error_with_malformed_xml_returns_validation_problem_result()
    {
        var realExtractor = new SamlLogoutRequestExtractor(
            new LogoutRequestParser(NullLogger<LogoutRequestParser>.Instance));
        var endpoint = CreateEndpointWithExtractor(realExtractor);
        var context = CreatePostContextWithMalformedXml();

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<ValidationProblemResult>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task parse_error_with_no_issuer_returns_validation_problem_result()
    {
        var realExtractor = new SamlLogoutRequestExtractor(
            new LogoutRequestParser(NullLogger<LogoutRequestParser>.Instance));
        var endpoint = CreateEndpointWithExtractor(realExtractor);

        var xml = """
                  <?xml version="1.0" encoding="UTF-8"?>
                  <samlp:LogoutRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                       xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                       ID="_test-id"
                                       Version="2.0"
                                       IssueInstant="2024-01-01T00:00:00Z">
                  </samlp:LogoutRequest>
                  """;
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));
        var formContent = $"SAMLRequest={Uri.EscapeDataString(encoded)}";
        var bodyBytes = Encoding.UTF8.GetBytes(formContent);

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<ValidationProblemResult>();
        result.ShouldNotBeOfType<LogoutResponse>();
    }

    private sealed class AlwaysFailLogoutExtractor()
        : SamlLogoutRequestExtractor(new LogoutRequestParser(NullLogger<LogoutRequestParser>.Instance))
    {
        protected override LogoutRequest ParseRequest(XDocument xmlDocument) =>
            throw new FormatException("Required attribute 'ID' is missing or empty.");

        protected override SamlLogoutRequest CreateResult(
            LogoutRequest parsedRequest,
            XDocument requestXml,
            SamlBinding binding,
            string? relayState,
            string? signature = null,
            string? signatureAlgorithm = null,
            string? encodedSamlRequest = null) => new()
            {
                Request = parsedRequest,
                RequestXml = requestXml,
                Binding = binding,
                RelayState = relayState,
                Signature = signature,
                SignatureAlgorithm = signatureAlgorithm,
                EncodedSamlRequest = encodedSamlRequest
            };
    }

    private static SamlSingleLogoutEndpoint CreateEndpointWithExtractor(SamlLogoutRequestExtractor extractor) =>
        new(
            extractor: extractor,
            processor: null!,        // never called on parse error path
            responseBuilder: null!,  // never called on parse error path
            logger: NullLogger<SamlSingleLogoutEndpoint>.Instance);

    private static HttpContext CreatePostContextWithIssuer(string issuer)
    {
        var xml = $"""
                   <?xml version="1.0" encoding="UTF-8"?>
                   <samlp:LogoutRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                        xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                        Version="2.0"
                                        IssueInstant="2024-01-01T00:00:00Z">
                       <saml:Issuer>{issuer}</saml:Issuer>
                   </samlp:LogoutRequest>
                   """;
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));
        var formContent = $"SAMLRequest={Uri.EscapeDataString(encoded)}";
        var bodyBytes = Encoding.UTF8.GetBytes(formContent);

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;
        return context;
    }

    private static HttpContext CreatePostContextWithMalformedXml()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("<not valid xml!!!"));
        var formContent = $"SAMLRequest={Uri.EscapeDataString(encoded)}";
        var bodyBytes = Encoding.UTF8.GetBytes(formContent);

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;
        return context;
    }
}
