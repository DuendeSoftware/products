// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Serialization;

namespace UnitTests.Saml;

public sealed class SamlXmlWriterLogoutTests
{
    private const string Category = "SamlXmlWriter Logout";
    private const string SamlpNs = SamlConstants.Namespaces.Protocol;
    private const string SamlNs = SamlConstants.Namespaces.Assertion;

    private readonly SamlXmlWriter _writer = new();

    private static XmlNamespaceManager CreateNsMgr(XmlDocument doc)
    {
        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("samlp", SamlpNs);
        nsmgr.AddNamespace("saml", SamlNs);
        return nsmgr;
    }

    private static LogoutResponse CreateLogoutResponse(string? inResponseTo = "_req-id") => new()
    {
        Id = "_resp-id",
        Version = "2.0",
        IssueInstant = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        InResponseTo = inResponseTo,
        Destination = "https://sp.example.com/slo",
        Issuer = new NameId("https://idp.example.com"),
        Status = new SamlStatus
        {
            StatusCode = new StatusCode { Value = SamlStatusCodes.Success }
        }
    };

    // ── LogoutResponse tests ──────────────────────────────────────────────────

    [Fact]
    [Trait("Category", Category)]
    public void WritesLogoutResponseWithAllStatusResponseTypeFields()
    {
        var response = CreateLogoutResponse();
        var doc = _writer.Write(response);
        var nsmgr = CreateNsMgr(doc);

        var root = doc.SelectSingleNode("/samlp:LogoutResponse", nsmgr);
        root.ShouldNotBeNull();
        root!.Attributes!["ID"]!.Value.ShouldBe("_resp-id");
        root.Attributes["Version"]!.Value.ShouldBe("2.0");
        root.Attributes["IssueInstant"]!.Value.ShouldBe("2025-01-01T00:00:00Z");
        root.Attributes["InResponseTo"]!.Value.ShouldBe("_req-id");
        root.Attributes["Destination"]!.Value.ShouldBe("https://sp.example.com/slo");

        var issuer = doc.SelectSingleNode("/samlp:LogoutResponse/saml:Issuer", nsmgr);
        issuer.ShouldNotBeNull();
        issuer!.InnerText.ShouldBe("https://idp.example.com");

        var statusCode = doc.SelectSingleNode("/samlp:LogoutResponse/samlp:Status/samlp:StatusCode", nsmgr);
        statusCode.ShouldNotBeNull();
        statusCode!.Attributes!["Value"]!.Value.ShouldBe(SamlStatusCodes.Success);
    }

    [Fact]
    [Trait("Category", Category)]
    public void WritesLogoutResponseWithoutOptionalInResponseTo()
    {
        var response = CreateLogoutResponse(inResponseTo: null);
        var doc = _writer.Write(response);
        var nsmgr = CreateNsMgr(doc);

        var root = doc.SelectSingleNode("/samlp:LogoutResponse", nsmgr);
        root.ShouldNotBeNull();
        root!.Attributes!["InResponseTo"].ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void WritesLogoutResponseWithNestedStatusCodes()
    {
        var response = CreateLogoutResponse();
        response.Status = new SamlStatus
        {
            StatusCode = new StatusCode
            {
                Value = SamlStatusCodes.Responder,
                NestedStatusCode = new StatusCode { Value = SamlStatusCodes.RequestDenied }
            }
        };

        var doc = _writer.Write(response);
        var nsmgr = CreateNsMgr(doc);

        var outerCode = doc.SelectSingleNode("/samlp:LogoutResponse/samlp:Status/samlp:StatusCode", nsmgr);
        outerCode!.Attributes!["Value"]!.Value.ShouldBe(SamlStatusCodes.Responder);

        var innerCode = doc.SelectSingleNode("/samlp:LogoutResponse/samlp:Status/samlp:StatusCode/samlp:StatusCode", nsmgr);
        innerCode.ShouldNotBeNull();
        innerCode!.Attributes!["Value"]!.Value.ShouldBe(SamlStatusCodes.RequestDenied);
    }

    [Fact]
    [Trait("Category", Category)]
    public void LogoutResponseUsesCorrectNamespacePrefixes()
    {
        var response = CreateLogoutResponse();
        var doc = _writer.Write(response);
        var nsmgr = CreateNsMgr(doc);

        // Root element should be in samlp namespace
        doc.DocumentElement!.NamespaceURI.ShouldBe(SamlpNs);
        // Issuer should be in saml namespace
        var issuer = doc.SelectSingleNode("/samlp:LogoutResponse/saml:Issuer", nsmgr);
        issuer.ShouldNotBeNull();
        issuer!.NamespaceURI.ShouldBe(SamlNs);
    }

    // ── LogoutRequest tests ───────────────────────────────────────────────────

    [Fact]
    [Trait("Category", Category)]
    public void WritesLogoutRequestWithAllFields()
    {
        var request = new LogoutRequest
        {
            Id = "_req-id",
            Version = "2.0",
            IssueInstant = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Destination = "https://sp.example.com/slo",
            Issuer = new NameId("https://idp.example.com"),
            Reason = "urn:oasis:names:tc:SAML:2.0:logout:user",
            NotOnOrAfter = new DateTime(2025, 1, 1, 0, 5, 0, DateTimeKind.Utc),
            NameId = new NameId
            {
                Value = "user@example.com",
                Format = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                SPNameQualifier = "https://sp.example.com",
                NameQualifier = "https://idp.example.com"
            },
            SessionIndex = "_session-123"
        };

        var doc = _writer.Write(request);
        var nsmgr = CreateNsMgr(doc);

        var root = doc.SelectSingleNode("/samlp:LogoutRequest", nsmgr);
        root.ShouldNotBeNull();
        root!.Attributes!["ID"]!.Value.ShouldBe("_req-id");
        root.Attributes["Version"]!.Value.ShouldBe("2.0");
        root.Attributes["IssueInstant"]!.Value.ShouldBe("2025-01-01T00:00:00Z");
        root.Attributes["Destination"]!.Value.ShouldBe("https://sp.example.com/slo");
        root.Attributes["Reason"]!.Value.ShouldBe("urn:oasis:names:tc:SAML:2.0:logout:user");
        root.Attributes["NotOnOrAfter"]!.Value.ShouldBe("2025-01-01T00:05:00Z");

        var issuer = doc.SelectSingleNode("/samlp:LogoutRequest/saml:Issuer", nsmgr);
        issuer.ShouldNotBeNull();
        issuer!.InnerText.ShouldBe("https://idp.example.com");

        var nameId = doc.SelectSingleNode("/samlp:LogoutRequest/saml:NameID", nsmgr);
        nameId.ShouldNotBeNull();
        nameId!.InnerText.ShouldBe("user@example.com");
        nameId.Attributes!["Format"]!.Value.ShouldBe("urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress");
        nameId.Attributes["SPNameQualifier"]!.Value.ShouldBe("https://sp.example.com");
        nameId.Attributes["NameQualifier"]!.Value.ShouldBe("https://idp.example.com");

        var sessionIndex = doc.SelectSingleNode("/samlp:LogoutRequest/samlp:SessionIndex", nsmgr);
        sessionIndex.ShouldNotBeNull();
        sessionIndex!.InnerText.ShouldBe("_session-123");
    }

    [Fact]
    [Trait("Category", Category)]
    public void WritesLogoutRequestWithoutOptionalFields()
    {
        var request = new LogoutRequest
        {
            Id = "_req-id",
            Version = "2.0",
            IssueInstant = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            NameId = new NameId("user@example.com")
        };

        var doc = _writer.Write(request);
        var nsmgr = CreateNsMgr(doc);

        var root = doc.SelectSingleNode("/samlp:LogoutRequest", nsmgr);
        root.ShouldNotBeNull();
        root!.Attributes!["Reason"].ShouldBeNull();
        root.Attributes["NotOnOrAfter"].ShouldBeNull();
        root.Attributes["Destination"].ShouldBeNull();

        var sessionIndex = doc.SelectSingleNode("/samlp:LogoutRequest/samlp:SessionIndex", nsmgr);
        sessionIndex.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void NameIdFormatSpNameQualifierAndNameQualifierAreIncludedWhenPresent()
    {
        var request = new LogoutRequest
        {
            Id = "_req-id",
            Version = "2.0",
            IssueInstant = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            NameId = new NameId
            {
                Value = "subject",
                Format = "urn:oasis:names:tc:SAML:2.0:nameid-format:persistent",
                SPNameQualifier = "https://sp.example.com",
                NameQualifier = "https://idp.example.com"
            }
        };

        var doc = _writer.Write(request);
        var nsmgr = CreateNsMgr(doc);

        var nameId = doc.SelectSingleNode("/samlp:LogoutRequest/saml:NameID", nsmgr);
        nameId.ShouldNotBeNull();
        nameId!.Attributes!["Format"]!.Value.ShouldBe("urn:oasis:names:tc:SAML:2.0:nameid-format:persistent");
        nameId.Attributes["SPNameQualifier"]!.Value.ShouldBe("https://sp.example.com");
        nameId.Attributes["NameQualifier"]!.Value.ShouldBe("https://idp.example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public void SessionIndexElementUsesProtocolNamespace()
    {
        var request = new LogoutRequest
        {
            Id = "_req-id",
            Version = "2.0",
            IssueInstant = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            NameId = new NameId("user@example.com"),
            SessionIndex = "_session-abc"
        };

        var doc = _writer.Write(request);
        var nsmgr = CreateNsMgr(doc);

        var sessionIndex = doc.SelectSingleNode("/samlp:LogoutRequest/samlp:SessionIndex", nsmgr);
        sessionIndex.ShouldNotBeNull();
        sessionIndex!.NamespaceURI.ShouldBe(SamlpNs);
        sessionIndex.InnerText.ShouldBe("_session-abc");
    }

    [Fact]
    [Trait("Category", Category)]
    public void LogoutRequestUsesCorrectNamespacePrefixes()
    {
        var request = new LogoutRequest
        {
            Id = "_req-id",
            Version = "2.0",
            IssueInstant = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Issuer = new NameId("https://idp.example.com"),
            NameId = new NameId("user@example.com")
        };

        var doc = _writer.Write(request);
        var nsmgr = CreateNsMgr(doc);

        // Root element in samlp namespace
        doc.DocumentElement!.NamespaceURI.ShouldBe(SamlpNs);
        // Issuer in saml namespace
        var issuer = doc.SelectSingleNode("/samlp:LogoutRequest/saml:Issuer", nsmgr);
        issuer.ShouldNotBeNull();
        issuer!.NamespaceURI.ShouldBe(SamlNs);
        // NameID in saml namespace
        var nameId = doc.SelectSingleNode("/samlp:LogoutRequest/saml:NameID", nsmgr);
        nameId.ShouldNotBeNull();
        nameId!.NamespaceURI.ShouldBe(SamlNs);
    }

    [Fact]
    [Trait("Category", Category)]
    public void WritesNestedStatusCode()
    {
        var response = CreateLogoutResponse();
        response.Status = new SamlStatus
        {
            StatusCode = new StatusCode
            {
                Value = SamlStatusCodes.Requester,
                NestedStatusCode = new StatusCode { Value = SamlStatusCodes.RequestDenied }
            }
        };

        var doc = _writer.Write(response);
        var nsmgr = CreateNsMgr(doc);

        var outerStatusCode = doc.SelectSingleNode("//samlp:Status/samlp:StatusCode", nsmgr);
        outerStatusCode.ShouldNotBeNull();
        outerStatusCode!.Attributes!["Value"]!.Value.ShouldBe(SamlStatusCodes.Requester);

        var nestedStatusCode = doc.SelectSingleNode("//samlp:Status/samlp:StatusCode/samlp:StatusCode", nsmgr);
        nestedStatusCode.ShouldNotBeNull();
        nestedStatusCode!.Attributes!["Value"]!.Value.ShouldBe(SamlStatusCodes.RequestDenied);
    }

    [Fact]
    [Trait("Category", Category)]
    public void WritesStatusMessage()
    {
        var response = CreateLogoutResponse();
        response.Status = new SamlStatus
        {
            StatusCode = new StatusCode { Value = SamlStatusCodes.Requester },
            StatusMessage = "The request was denied"
        };

        var doc = _writer.Write(response);
        var nsmgr = CreateNsMgr(doc);

        var statusMessage = doc.SelectSingleNode("//samlp:Status/samlp:StatusMessage", nsmgr);
        statusMessage.ShouldNotBeNull();
        statusMessage!.InnerText.ShouldBe("The request was denied");
    }

    [Fact]
    [Trait("Category", Category)]
    public void StatusMessageWithXmlCharactersIsEscaped()
    {
        var response = CreateLogoutResponse();
        response.Status = new SamlStatus
        {
            StatusCode = new StatusCode { Value = SamlStatusCodes.Requester },
            StatusMessage = "Error: <value> & \"quote\""
        };

        var doc = _writer.Write(response);
        var nsmgr = CreateNsMgr(doc);

        var statusMessage = doc.SelectSingleNode("//samlp:Status/samlp:StatusMessage", nsmgr);
        statusMessage.ShouldNotBeNull();
        statusMessage!.InnerText.ShouldBe("Error: <value> & \"quote\"");
    }

    [Fact]
    [Trait("Category", Category)]
    public void StatusMessageRoundTripsWithReader()
    {
        var response = CreateLogoutResponse();
        response.Status = new SamlStatus
        {
            StatusCode = new StatusCode
            {
                Value = SamlStatusCodes.Requester,
                NestedStatusCode = new StatusCode { Value = SamlStatusCodes.RequestDenied }
            },
            StatusMessage = "Logout denied by policy"
        };

        var doc = _writer.Write(response);

        // Read it back
        var reader = new SamlXmlReader();
        var traverser = new Duende.IdentityServer.Saml.Xml.XmlTraverser(doc.DocumentElement!);
        var readBack = reader.ReadLogoutResponseAsync(traverser, null, CancellationToken.None).GetAwaiter().GetResult();

        readBack.Status.StatusCode.Value.ShouldBe(SamlStatusCodes.Requester);
        readBack.Status.StatusCode.NestedStatusCode.ShouldNotBeNull();
        readBack.Status.StatusCode.NestedStatusCode!.Value.ShouldBe(SamlStatusCodes.RequestDenied);
        readBack.Status.StatusMessage.ShouldBe("Logout denied by policy");
    }
}
