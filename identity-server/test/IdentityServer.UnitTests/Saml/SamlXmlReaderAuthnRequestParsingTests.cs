// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Cryptography.Xml;
using System.Xml;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Xml;

namespace UnitTests.Saml;

public sealed class SamlXmlReaderAuthnRequestParsingTests
{
    private const string Category = "SamlXmlReader AuthnRequest Parsing";

    [Fact]
    [Trait("Category", Category)]
    public async Task SignedAuthnRequestWithoutExtensionsParses()
    {
        var reader = CreateReader();
        var xml = CreateAuthnRequestXml(includeSignature: true, includeExtensions: false);
        var traverser = new XmlTraverser(xml);

        var result = await reader.ReadAuthnRequestAsync(traverser, null, CancellationToken.None);

        result.Issuer!.Value.ShouldBe("https://sp.example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SignedAuthnRequestWithExtensionsParses()
    {
        var reader = CreateReader();
        var xml = CreateAuthnRequestXml(includeSignature: true, includeExtensions: true);
        var traverser = new XmlTraverser(xml);

        var result = await reader.ReadAuthnRequestAsync(traverser, null, CancellationToken.None);

        result.Issuer!.Value.ShouldBe("https://sp.example.com");
        result.Extensions.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnsignedAuthnRequestParses()
    {
        var reader = CreateReader();
        var xml = CreateAuthnRequestXml(includeSignature: false, includeExtensions: false);
        var traverser = new XmlTraverser(xml);

        var result = await reader.ReadAuthnRequestAsync(traverser, null, CancellationToken.None);

        result.Issuer!.Value.ShouldBe("https://sp.example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SignedAuthnRequestWithOptionalElementsParses()
    {
        var reader = CreateReader();
        var xml = CreateAuthnRequestXml(
            includeSignature: true, includeExtensions: false, includeNameIdPolicy: true);
        var traverser = new XmlTraverser(xml);

        var result = await reader.ReadAuthnRequestAsync(traverser, null, CancellationToken.None);

        result.Issuer!.Value.ShouldBe("https://sp.example.com");
        result.NameIdPolicy.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ParsesAttributeConsumingServiceIndexDistinctFromAssertionConsumerServiceIndex()
    {
        var reader = CreateReader();
        var doc = new XmlDocument();
        var authnRequest = doc.CreateElement(
            "samlp", "AuthnRequest", SamlConstants.Namespaces.Protocol);
        authnRequest.SetAttribute("ID", "_test-id");
        authnRequest.SetAttribute("Version", "2.0");
        authnRequest.SetAttribute("IssueInstant", "2025-01-01T00:00:00Z");
        authnRequest.SetAttribute("AssertionConsumerServiceIndex", "3");
        authnRequest.SetAttribute("AttributeConsumingServiceIndex", "7");
        doc.AppendChild(authnRequest);

        var issuer = doc.CreateElement(
            "saml", "Issuer", SamlConstants.Namespaces.Assertion);
        issuer.InnerText = "https://sp.example.com";
        authnRequest.AppendChild(issuer);

        var traverser = new XmlTraverser(doc.DocumentElement!);
        var result = await reader.ReadAuthnRequestAsync(traverser, null, CancellationToken.None);

        result.AssertionConsumerServiceIndex.ShouldBe(3);
        result.AttributeConsumingServiceIndex.ShouldBe(7);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ParsesConditionsWithBothNotBeforeAndNotOnOrAfter()
    {
        var reader = CreateReader();
        var doc = new XmlDocument();
        var authnRequest = doc.CreateElement(
            "samlp", "AuthnRequest", SamlConstants.Namespaces.Protocol);
        authnRequest.SetAttribute("ID", "_test-id");
        authnRequest.SetAttribute("Version", "2.0");
        authnRequest.SetAttribute("IssueInstant", "2025-01-01T00:00:00Z");
        doc.AppendChild(authnRequest);

        var issuer = doc.CreateElement(
            "saml", "Issuer", SamlConstants.Namespaces.Assertion);
        issuer.InnerText = "https://sp.example.com";
        authnRequest.AppendChild(issuer);

        var conditions = doc.CreateElement(
            "saml", "Conditions", SamlConstants.Namespaces.Assertion);
        conditions.SetAttribute("NotBefore", "2025-01-01T00:00:00Z");
        conditions.SetAttribute("NotOnOrAfter", "2025-01-01T00:05:00Z");
        authnRequest.AppendChild(conditions);

        var traverser = new XmlTraverser(doc.DocumentElement!);
        var result = await reader.ReadAuthnRequestAsync(traverser, null, CancellationToken.None);

        result.Conditions.ShouldNotBeNull();
        result.Conditions.NotBefore.ShouldNotBeNull();
        result.Conditions.NotBefore.Value.ToString().ShouldBe("2025-01-01T00:00:00Z");
        result.Conditions.NotOnOrAfter.ShouldNotBeNull();
        result.Conditions.NotOnOrAfter.Value.ToString().ShouldBe("2025-01-01T00:05:00Z");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ConditionsWithProxyRestrictionReportsError()
    {
        var reader = CreateReader();
        var doc = new XmlDocument();
        var authnRequest = doc.CreateElement(
            "samlp", "AuthnRequest", SamlConstants.Namespaces.Protocol);
        authnRequest.SetAttribute("ID", "_test-id");
        authnRequest.SetAttribute("Version", "2.0");
        authnRequest.SetAttribute("IssueInstant", "2025-01-01T00:00:00Z");
        doc.AppendChild(authnRequest);

        var issuer = doc.CreateElement(
            "saml", "Issuer", SamlConstants.Namespaces.Assertion);
        issuer.InnerText = "https://sp.example.com";
        authnRequest.AppendChild(issuer);

        var conditions = doc.CreateElement(
            "saml", "Conditions", SamlConstants.Namespaces.Assertion);
        var proxyRestriction = doc.CreateElement(
            "saml", "ProxyRestriction", SamlConstants.Namespaces.Assertion);
        proxyRestriction.SetAttribute("Count", "2");
        conditions.AppendChild(proxyRestriction);
        authnRequest.AppendChild(conditions);

        var traverser = new XmlTraverser(doc.DocumentElement!);

        var ex = await Should.ThrowAsync<SamlXmlException>(
            () => reader.ReadAuthnRequestAsync(traverser, null, CancellationToken.None));
        ex.Message.ShouldBe("All child nodes under Conditions have not been processed.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RequestedAuthnContextWithBothClassRefAndDeclRefReportsError()
    {
        var reader = CreateReader();
        var doc = new XmlDocument();
        var authnRequest = doc.CreateElement(
            "samlp", "AuthnRequest", SamlConstants.Namespaces.Protocol);
        authnRequest.SetAttribute("ID", "_test-id");
        authnRequest.SetAttribute("Version", "2.0");
        authnRequest.SetAttribute("IssueInstant", "2025-01-01T00:00:00Z");
        doc.AppendChild(authnRequest);

        var issuer = doc.CreateElement(
            "saml", "Issuer", SamlConstants.Namespaces.Assertion);
        issuer.InnerText = "https://sp.example.com";
        authnRequest.AppendChild(issuer);

        var requestedAuthnContext = doc.CreateElement(
            "samlp", "RequestedAuthnContext", SamlConstants.Namespaces.Protocol);
        var classRef = doc.CreateElement(
            "saml", "AuthnContextClassRef", SamlConstants.Namespaces.Assertion);
        classRef.InnerText = "urn:oasis:names:tc:SAML:2.0:ac:classes:Password";
        requestedAuthnContext.AppendChild(classRef);
        var declRef = doc.CreateElement(
            "saml", "AuthnContextDeclRef", SamlConstants.Namespaces.Assertion);
        declRef.InnerText = "urn:example:decl";
        requestedAuthnContext.AppendChild(declRef);
        authnRequest.AppendChild(requestedAuthnContext);

        var traverser = new XmlTraverser(doc.DocumentElement!);

        var ex = await Should.ThrowAsync<SamlXmlException>(
            () => reader.ReadAuthnRequestAsync(traverser, null, CancellationToken.None));
        ex.Message.ShouldBe("RequestedAuthnContext must contain either AuthnContextClassRef or AuthnContextDeclRef elements, but not both");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RequestedAuthnContextWithOnlyClassRefsHasNoErrors()
    {
        var reader = CreateReader();
        var doc = new XmlDocument();
        var authnRequest = doc.CreateElement(
            "samlp", "AuthnRequest", SamlConstants.Namespaces.Protocol);
        authnRequest.SetAttribute("ID", "_test-id");
        authnRequest.SetAttribute("Version", "2.0");
        authnRequest.SetAttribute("IssueInstant", "2025-01-01T00:00:00Z");
        doc.AppendChild(authnRequest);

        var issuer = doc.CreateElement(
            "saml", "Issuer", SamlConstants.Namespaces.Assertion);
        issuer.InnerText = "https://sp.example.com";
        authnRequest.AppendChild(issuer);

        var requestedAuthnContext = doc.CreateElement(
            "samlp", "RequestedAuthnContext", SamlConstants.Namespaces.Protocol);
        requestedAuthnContext.SetAttribute("Comparison", "exact");
        var classRef = doc.CreateElement(
            "saml", "AuthnContextClassRef", SamlConstants.Namespaces.Assertion);
        classRef.InnerText = "urn:oasis:names:tc:SAML:2.0:ac:classes:Password";
        requestedAuthnContext.AppendChild(classRef);
        authnRequest.AppendChild(requestedAuthnContext);

        var traverser = new XmlTraverser(doc.DocumentElement!);
        var result = await reader.ReadAuthnRequestAsync(traverser, null, CancellationToken.None);

        traverser.Errors.ShouldBeEmpty();
        result.RequestedAuthnContext.ShouldNotBeNull();
        result.RequestedAuthnContext.AuthnContextClassRef.ShouldContain("urn:oasis:names:tc:SAML:2.0:ac:classes:Password");
    }

    private static SamlXmlReader CreateReader() => new();

    private static XmlElement CreateAuthnRequestXml(
        bool includeSignature,
        bool includeExtensions,
        bool includeNameIdPolicy = false)
    {
        var doc = new XmlDocument();
        var authnRequest = doc.CreateElement(
            "samlp", "AuthnRequest", SamlConstants.Namespaces.Protocol);
        authnRequest.SetAttribute("ID", "_test-id");
        authnRequest.SetAttribute("Version", "2.0");
        authnRequest.SetAttribute("IssueInstant", "2025-01-01T00:00:00Z");
        doc.AppendChild(authnRequest);

        var issuer = doc.CreateElement(
            "saml", "Issuer", SamlConstants.Namespaces.Assertion);
        issuer.InnerText = "https://sp.example.com";
        authnRequest.AppendChild(issuer);

        if (includeSignature)
        {
            var signature = doc.CreateElement(
                "ds", "Signature", SignedXml.XmlDsigNamespaceUrl);
            authnRequest.AppendChild(signature);
        }

        if (includeExtensions)
        {
            var extensions = doc.CreateElement(
                "samlp", "Extensions", SamlConstants.Namespaces.Protocol);
            var custom = doc.CreateElement("custom", "Data", "urn:custom");
            extensions.AppendChild(custom);
            authnRequest.AppendChild(extensions);
        }

        if (includeNameIdPolicy)
        {
            var nameIdPolicy = doc.CreateElement(
                "samlp", "NameIDPolicy", SamlConstants.Namespaces.Protocol);
            nameIdPolicy.SetAttribute("Format", "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress");
            authnRequest.AppendChild(nameIdPolicy);
        }

        return doc.DocumentElement!;
    }
}
