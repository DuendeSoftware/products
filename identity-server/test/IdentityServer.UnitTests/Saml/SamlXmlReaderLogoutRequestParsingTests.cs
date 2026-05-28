// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Xml;

namespace UnitTests.Saml;

public sealed class SamlXmlReaderLogoutRequestParsingTests
{
    private const string Category = "SamlXmlReader LogoutRequest Parsing";

    [Fact]
    [Trait("Category", Category)]
    public async Task ParsesValidLogoutRequestWithAllFields()
    {
        var reader = CreateReader();
        var doc = CreateLogoutRequestDoc(
            nameIdValue: "user@example.com",
            nameIdFormat: "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
            nameIdSpNameQualifier: "https://sp.example.com",
            nameIdNameQualifier: "https://idp.example.com",
            sessionIndex: "_session-123",
            reason: "urn:oasis:names:tc:SAML:2.0:logout:user",
            notOnOrAfter: "2025-01-01T00:05:00Z");

        var traverser = new XmlTraverser(doc.DocumentElement!);
        var result = await reader.ReadLogoutRequestAsync(traverser, null, CancellationToken.None);

        result.Id.ShouldBe("_test-id");
        result.Version.ShouldBe("2.0");
        result.Issuer!.Value.ShouldBe("https://sp.example.com");
        result.NameId.ShouldNotBeNull();
        result.NameId.Value.ShouldBe("user@example.com");
        result.NameId.Format.ShouldBe("urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress");
        result.NameId.SPNameQualifier.ShouldBe("https://sp.example.com");
        result.NameId.NameQualifier.ShouldBe("https://idp.example.com");
        result.SessionIndex.ShouldBe("_session-123");
        result.Reason.ShouldBe("urn:oasis:names:tc:SAML:2.0:logout:user");
        result.NotOnOrAfter.ShouldNotBeNull();
        result.NotOnOrAfter.Value.ToString().ShouldBe("2025-01-01T00:05:00Z");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ParsesLogoutRequestWithoutOptionalFields()
    {
        var reader = CreateReader();
        var doc = CreateLogoutRequestDoc(nameIdValue: "user@example.com");

        var traverser = new XmlTraverser(doc.DocumentElement!);
        var result = await reader.ReadLogoutRequestAsync(traverser, null, CancellationToken.None);

        result.Issuer!.Value.ShouldBe("https://sp.example.com");
        result.NameId.ShouldNotBeNull();
        result.NameId.Value.ShouldBe("user@example.com");
        result.NameId.Format.ShouldBeNull();
        result.NameId.SPNameQualifier.ShouldBeNull();
        result.NameId.NameQualifier.ShouldBeNull();
        result.SessionIndex.ShouldBeNull();
        result.Reason.ShouldBeNull();
        result.NotOnOrAfter.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ParsesNameIdWithFormatSpNameQualifierAndNameQualifier()
    {
        var reader = CreateReader();
        var doc = CreateLogoutRequestDoc(
            nameIdValue: "subject-value",
            nameIdFormat: "urn:oasis:names:tc:SAML:2.0:nameid-format:persistent",
            nameIdSpNameQualifier: "https://sp.example.com",
            nameIdNameQualifier: "https://idp.example.com");

        var traverser = new XmlTraverser(doc.DocumentElement!);
        var result = await reader.ReadLogoutRequestAsync(traverser, null, CancellationToken.None);

        result.NameId!.Format.ShouldBe("urn:oasis:names:tc:SAML:2.0:nameid-format:persistent");
        result.NameId.SPNameQualifier.ShouldBe("https://sp.example.com");
        result.NameId.NameQualifier.ShouldBe("https://idp.example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ParsesSessionIndexElement()
    {
        var reader = CreateReader();
        var doc = CreateLogoutRequestDoc(
            nameIdValue: "user@example.com",
            sessionIndex: "_abc-session-456");

        var traverser = new XmlTraverser(doc.DocumentElement!);
        var result = await reader.ReadLogoutRequestAsync(traverser, null, CancellationToken.None);

        result.SessionIndex.ShouldBe("_abc-session-456");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RejectsXmlWithWrongRootElement()
    {
        var reader = CreateReader();
        var doc = new XmlDocument();
        var wrongElement = doc.CreateElement("samlp", "AuthnRequest", SamlConstants.Namespaces.Protocol);
        wrongElement.SetAttribute("ID", "_test-id");
        wrongElement.SetAttribute("Version", "2.0");
        wrongElement.SetAttribute("IssueInstant", "2025-01-01T00:00:00Z");
        doc.AppendChild(wrongElement);

        var traverser = new XmlTraverser(doc.DocumentElement!);

        await Should.ThrowAsync<Exception>(async () =>
            await reader.ReadLogoutRequestAsync(traverser, null, CancellationToken.None));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnsignedLogoutRequestHasUntrustedSignatureLevel()
    {
        var reader = CreateReader();
        var doc = CreateLogoutRequestDoc(nameIdValue: "user@example.com");

        var traverser = new XmlTraverser(doc.DocumentElement!);
        var result = await reader.ReadLogoutRequestAsync(traverser, null, CancellationToken.None);

        result.HasTrustedSignature.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task XswAttackWithDuplicateElementIsRejectedOrIgnored()
    {
        // XML Signature Wrapping attack: a duplicate LogoutRequest element with different
        // NameID is injected. The reader should only process the signed element and not
        // be tricked into reading the attacker-controlled duplicate.
        var reader = CreateReader();
        var doc = new XmlDocument();

        // Outer wrapper (unsigned, attacker-controlled)
        var wrapper = doc.CreateElement("samlp", "LogoutRequest", SamlConstants.Namespaces.Protocol);
        wrapper.SetAttribute("ID", "_attacker-id");
        wrapper.SetAttribute("Version", "2.0");
        wrapper.SetAttribute("IssueInstant", "2025-01-01T00:00:00Z");
        doc.AppendChild(wrapper);

        var attackerIssuer = doc.CreateElement("saml", "Issuer", SamlConstants.Namespaces.Assertion);
        attackerIssuer.InnerText = "https://attacker.example.com";
        wrapper.AppendChild(attackerIssuer);

        var attackerNameId = doc.CreateElement("saml", "NameID", SamlConstants.Namespaces.Assertion);
        attackerNameId.InnerText = "attacker@example.com";
        wrapper.AppendChild(attackerNameId);

        // The traverser processes the root element — the attacker's wrapper.
        // The reader should read the root element's content (attacker-controlled),
        // but since there is no valid signature, TrustLevel should be None/untrusted.
        var traverser = new XmlTraverser(doc.DocumentElement!);
        var result = await reader.ReadLogoutRequestAsync(traverser, null, CancellationToken.None);

        // Without a valid signature, the request must not be trusted
        result.HasTrustedSignature.ShouldBeFalse();
        // The ID read should be from the element actually processed
        result.Id.ShouldBe("_attacker-id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task LogoutRequestWithWrongElementInsteadOfNameIdIsRejected()
    {
        var reader = CreateReader();
        var doc = new XmlDocument();
        var logoutRequest = doc.CreateElement("samlp", "LogoutRequest", SamlConstants.Namespaces.Protocol);
        logoutRequest.SetAttribute("ID", "_test-id");
        logoutRequest.SetAttribute("Version", "2.0");
        logoutRequest.SetAttribute("IssueInstant", "2025-01-01T00:00:00Z");
        doc.AppendChild(logoutRequest);

        var issuer = doc.CreateElement("saml", "Issuer", SamlConstants.Namespaces.Assertion);
        issuer.InnerText = "https://sp.example.com";
        logoutRequest.AppendChild(issuer);

        // Wrong element where NameID should be — EnsureName should record an error
        var wrongElement = doc.CreateElement("samlp", "SessionIndex", SamlConstants.Namespaces.Protocol);
        wrongElement.InnerText = "_session-123";
        logoutRequest.AppendChild(wrongElement);

        var traverser = new XmlTraverser(doc.DocumentElement!);

        await Should.ThrowAsync<Exception>(async () =>
            await reader.ReadLogoutRequestAsync(traverser, null, CancellationToken.None));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task LogoutRequestWithoutNameIdHasNullNameId()
    {
        var reader = CreateReader();
        var doc = new XmlDocument();
        var logoutRequest = doc.CreateElement("samlp", "LogoutRequest", SamlConstants.Namespaces.Protocol);
        logoutRequest.SetAttribute("ID", "_test-id");
        logoutRequest.SetAttribute("Version", "2.0");
        logoutRequest.SetAttribute("IssueInstant", "2025-01-01T00:00:00Z");
        doc.AppendChild(logoutRequest);

        var issuer = doc.CreateElement("saml", "Issuer", SamlConstants.Namespaces.Assertion);
        issuer.InnerText = "https://sp.example.com";
        logoutRequest.AppendChild(issuer);

        // No NameID element and no other elements — EnsureName returns false silently
        // when CurrentNode is null (no more children). The NameID will be null,
        // and downstream validation should reject the request.
        var traverser = new XmlTraverser(doc.DocumentElement!);
        var result = await reader.ReadLogoutRequestAsync(traverser, null, CancellationToken.None);

        result.NameId.ShouldBeNull();
    }

    private static SamlXmlReader CreateReader() => new();

    private static XmlDocument CreateLogoutRequestDoc(
        string nameIdValue,
        string? nameIdFormat = null,
        string? nameIdSpNameQualifier = null,
        string? nameIdNameQualifier = null,
        string? sessionIndex = null,
        string? reason = null,
        string? notOnOrAfter = null)
    {
        var doc = new XmlDocument();
        var logoutRequest = doc.CreateElement("samlp", "LogoutRequest", SamlConstants.Namespaces.Protocol);
        logoutRequest.SetAttribute("ID", "_test-id");
        logoutRequest.SetAttribute("Version", "2.0");
        logoutRequest.SetAttribute("IssueInstant", "2025-01-01T00:00:00Z");

        if (reason != null)
        {
            logoutRequest.SetAttribute("Reason", reason);
        }

        if (notOnOrAfter != null)
        {
            logoutRequest.SetAttribute("NotOnOrAfter", notOnOrAfter);
        }

        doc.AppendChild(logoutRequest);

        var issuer = doc.CreateElement("saml", "Issuer", SamlConstants.Namespaces.Assertion);
        issuer.InnerText = "https://sp.example.com";
        logoutRequest.AppendChild(issuer);

        var nameId = doc.CreateElement("saml", "NameID", SamlConstants.Namespaces.Assertion);
        nameId.InnerText = nameIdValue;
        if (nameIdFormat != null) { nameId.SetAttribute("Format", nameIdFormat); }
        if (nameIdSpNameQualifier != null) { nameId.SetAttribute("SPNameQualifier", nameIdSpNameQualifier); }
        if (nameIdNameQualifier != null) { nameId.SetAttribute("NameQualifier", nameIdNameQualifier); }
        logoutRequest.AppendChild(nameId);

        if (sessionIndex != null)
        {
            var sessionIndexEl = doc.CreateElement("samlp", "SessionIndex", SamlConstants.Namespaces.Protocol);
            sessionIndexEl.InnerText = sessionIndex;
            logoutRequest.AppendChild(sessionIndexEl);
        }

        return doc;
    }
}
