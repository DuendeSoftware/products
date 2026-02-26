// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Internal.Saml.SingleSignin;
using Duende.IdentityServer.Saml.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Saml;

public class RequestedAuthnContextParsingTests
{
    private const string Category = "Requested AuthN Context Parsing";

    private readonly AuthNRequestParser _parser = new(NullLogger<AuthNRequestParser>.Instance);

    private const string BaseAuthNRequest = """
        <samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                            xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                            ID="_12345"
                            Version="2.0"
                            IssueInstant="2024-01-01T00:00:00Z">
            <saml:Issuer>https://sp.example.com</saml:Issuer>
            {0}
        </samlp:AuthnRequest>
        """;

    [Fact]
    [Trait("Category", Category)]
    public void parse_single_authn_context_class_ref_succeeds()
    {
        var requestedAuthnContext = """
            <samlp:RequestedAuthnContext Comparison="exact">
                <saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:Password</saml:AuthnContextClassRef>
            </samlp:RequestedAuthnContext>
            """;
        var xml = string.Format(BaseAuthNRequest, requestedAuthnContext);
        var doc = System.Xml.Linq.XDocument.Parse(xml);

        var result = _parser.Parse(doc);

        result.RequestedAuthnContext.ShouldNotBeNull();
        result.RequestedAuthnContext.AuthnContextClassRefs.Count.ShouldBe(1);
        result.RequestedAuthnContext.AuthnContextClassRefs.First().ShouldBe("urn:oasis:names:tc:SAML:2.0:ac:classes:Password");
        result.RequestedAuthnContext.Comparison.ShouldBe(AuthnContextComparison.Exact);
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_multiple_authn_context_class_ref_succeeds()
    {
        var requestedAuthnContext = """
            <samlp:RequestedAuthnContext Comparison="minimum">
                <saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:Password</saml:AuthnContextClassRef>
                <saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport</saml:AuthnContextClassRef>
                <saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:X509</saml:AuthnContextClassRef>
            </samlp:RequestedAuthnContext>
            """;
        var xml = string.Format(BaseAuthNRequest, requestedAuthnContext);
        var doc = System.Xml.Linq.XDocument.Parse(xml);

        var result = _parser.Parse(doc);

        result.RequestedAuthnContext.ShouldNotBeNull();
        result.RequestedAuthnContext.AuthnContextClassRefs.Count.ShouldBe(3);
        result.RequestedAuthnContext.AuthnContextClassRefs.ShouldContain("urn:oasis:names:tc:SAML:2.0:ac:classes:Password");
        result.RequestedAuthnContext.AuthnContextClassRefs.ShouldContain("urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport");
        result.RequestedAuthnContext.AuthnContextClassRefs.ShouldContain("urn:oasis:names:tc:SAML:2.0:ac:classes:X509");
        result.RequestedAuthnContext.Comparison.ShouldBe(AuthnContextComparison.Minimum);
    }

    [Theory]
    [InlineData("exact", AuthnContextComparison.Exact)]
    [InlineData("minimum", AuthnContextComparison.Minimum)]
    [InlineData("maximum", AuthnContextComparison.Maximum)]
    [InlineData("better", AuthnContextComparison.Better)]
    [InlineData("EXACT", AuthnContextComparison.Exact)]
    [InlineData("MINIMUM", AuthnContextComparison.Minimum)]
    [InlineData("Exact", AuthnContextComparison.Exact)]
    [Trait("Category", Category)]
    public void parse_comparison_attribute_succeeds(string comparisonValue, AuthnContextComparison expected)
    {
        var requestedAuthnContext = $"""
            <samlp:RequestedAuthnContext Comparison="{comparisonValue}">
                <saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:Password</saml:AuthnContextClassRef>
            </samlp:RequestedAuthnContext>
            """;
        var xml = string.Format(BaseAuthNRequest, requestedAuthnContext);
        var doc = System.Xml.Linq.XDocument.Parse(xml);

        var result = _parser.Parse(doc);

        result.RequestedAuthnContext.ShouldNotBeNull();
        result.RequestedAuthnContext.Comparison.ShouldBe(expected);
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_omitted_comparison_defaults_to_exact()
    {
        var requestedAuthnContext = """
            <samlp:RequestedAuthnContext>
                <saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:Password</saml:AuthnContextClassRef>
            </samlp:RequestedAuthnContext>
            """;
        var xml = string.Format(BaseAuthNRequest, requestedAuthnContext);
        var doc = System.Xml.Linq.XDocument.Parse(xml);

        var result = _parser.Parse(doc);

        result.RequestedAuthnContext.ShouldNotBeNull();
        result.RequestedAuthnContext.Comparison.ShouldBe(AuthnContextComparison.Exact);
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_invalid_comparison_throws()
    {
        var requestedAuthnContext = """
            <samlp:RequestedAuthnContext Comparison="invalid">
                <saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:Password</saml:AuthnContextClassRef>
            </samlp:RequestedAuthnContext>
            """;
        var xml = string.Format(BaseAuthNRequest, requestedAuthnContext);
        var doc = System.Xml.Linq.XDocument.Parse(xml);

        var result = Should.Throw<ArgumentException>(() => _parser.Parse(doc));

        result.Message.ShouldBe("Unknown AuthnContextComparison: invalid");
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_no_authn_context_class_ref_throws()
    {
        var requestedAuthnContext = """
            <samlp:RequestedAuthnContext Comparison="exact">
            </samlp:RequestedAuthnContext>
            """;
        var xml = string.Format(BaseAuthNRequest, requestedAuthnContext);
        var doc = System.Xml.Linq.XDocument.Parse(xml);

        var result = Should.Throw<InvalidOperationException>(() => _parser.Parse(doc));

        result.Message.ShouldBe("No AuthnContextClassRef element found in requestedAuthnContext");
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_missing_requested_authn_context_returns_null()
    {
        var xml = string.Format(BaseAuthNRequest, "");
        var doc = System.Xml.Linq.XDocument.Parse(xml);

        var result = _parser.Parse(doc);

        result.RequestedAuthnContext.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_empty_authn_context_class_ref_skipped()
    {
        var requestedAuthnContext = """
            <samlp:RequestedAuthnContext Comparison="exact">
                <saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:Password</saml:AuthnContextClassRef>
                <saml:AuthnContextClassRef>   </saml:AuthnContextClassRef>
                <saml:AuthnContextClassRef></saml:AuthnContextClassRef>
                <saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:X509</saml:AuthnContextClassRef>
            </samlp:RequestedAuthnContext>
            """;
        var xml = string.Format(BaseAuthNRequest, requestedAuthnContext);
        var doc = System.Xml.Linq.XDocument.Parse(xml);

        var result = _parser.Parse(doc);

        result.RequestedAuthnContext.ShouldNotBeNull();
        result.RequestedAuthnContext.AuthnContextClassRefs.Count.ShouldBe(2);
        result.RequestedAuthnContext.AuthnContextClassRefs.ShouldContain("urn:oasis:names:tc:SAML:2.0:ac:classes:Password");
        result.RequestedAuthnContext.AuthnContextClassRefs.ShouldContain("urn:oasis:names:tc:SAML:2.0:ac:classes:X509");
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_whitespace_authn_context_class_ref_trimmed()
    {
        var requestedAuthnContext = """
            <samlp:RequestedAuthnContext>
                <saml:AuthnContextClassRef>  urn:oasis:names:tc:SAML:2.0:ac:classes:Password  </saml:AuthnContextClassRef>
            </samlp:RequestedAuthnContext>
            """;
        var xml = string.Format(BaseAuthNRequest, requestedAuthnContext);
        var doc = System.Xml.Linq.XDocument.Parse(xml);

        var result = _parser.Parse(doc);

        result.RequestedAuthnContext.ShouldNotBeNull();
        result.RequestedAuthnContext.AuthnContextClassRefs.First().ShouldBe("urn:oasis:names:tc:SAML:2.0:ac:classes:Password");
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_only_empty_authn_context_class_ref_throws()
    {
        var requestedAuthnContext = """
            <samlp:RequestedAuthnContext>
                <saml:AuthnContextClassRef></saml:AuthnContextClassRef>
                <saml:AuthnContextClassRef>  </saml:AuthnContextClassRef>
            </samlp:RequestedAuthnContext>
            """;
        var xml = string.Format(BaseAuthNRequest, requestedAuthnContext);
        var doc = System.Xml.Linq.XDocument.Parse(xml);

        var result = Should.Throw<InvalidOperationException>(() => _parser.Parse(doc));

        result.Message.ShouldBe("No AuthnContextClassRef element found in requestedAuthnContext");
    }
}
