// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.SingleLogout;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Saml;

public class LogoutRequestParserTests
{
    private const string Category = "Logout Request Parser";

    private readonly LogoutRequestParser _parser = new(NullLogger<LogoutRequestParser>.Instance);

    private static string CreateLogoutRequest(
        string? id = null,
        string? issuer = null,
        string? destination = null,
        string? nameId = null,
        string? sessionIndex = null)
    {
        id ??= "_test-logout-id";
        issuer ??= "https://sp.example.com";
        destination ??= "https://idp.example.com/saml/logout";
        nameId ??= "user@example.com";
        sessionIndex ??= "_session123";

        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<samlp:LogoutRequest xmlns:samlp=""urn:oasis:names:tc:SAML:2.0:protocol""
                     xmlns:saml=""urn:oasis:names:tc:SAML:2.0:assertion""
                     ID=""{id}""
                     Version=""2.0""
                     IssueInstant=""2024-01-01T00:00:00Z""
                     Destination=""{destination}"">
    <saml:Issuer>{issuer}</saml:Issuer>
    <saml:NameID Format=""urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress"">{nameId}</saml:NameID>
    <samlp:SessionIndex>{sessionIndex}</samlp:SessionIndex>
</samlp:LogoutRequest>";
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_valid_logout_request_returns_success()
    {
        var xmlString = CreateLogoutRequest();
        var doc = XDocument.Parse(xmlString);

        var result = _parser.Parse(doc);

        result.ShouldNotBeNull();
        result.Id.ShouldBe("_test-logout-id");
        result.Issuer.ShouldBe("https://sp.example.com");
        result.Destination!.ToString().ShouldBe("https://idp.example.com/saml/logout");
        result.NameId.Value.ShouldBe("user@example.com");
        result.SessionIndex.ShouldBe("_session123");
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_missing_id_throws_exception()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<samlp:LogoutRequest xmlns:samlp=""urn:oasis:names:tc:SAML:2.0:protocol""
                     xmlns:saml=""urn:oasis:names:tc:SAML:2.0:assertion""
                     Version=""2.0""
                     IssueInstant=""2024-01-01T00:00:00Z"">
    <saml:Issuer>https://sp.example.com</saml:Issuer>
</samlp:LogoutRequest>";
        var doc = XDocument.Parse(xml);

        Should.Throw<FormatException>(() => _parser.Parse(doc))
            .Message.ShouldContain("ID");
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_missing_issuer_throws_exception()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<samlp:LogoutRequest xmlns:samlp=""urn:oasis:names:tc:SAML:2.0:protocol""
                     xmlns:saml=""urn:oasis:names:tc:SAML:2.0:assertion""
                     ID=""_test-id""
                     Version=""2.0""
                     IssueInstant=""2024-01-01T00:00:00Z"">
</samlp:LogoutRequest>";
        var doc = XDocument.Parse(xml);

        Should.Throw<InvalidOperationException>(() => _parser.Parse(doc))
            .Message.ShouldContain("Issuer");
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_invalid_xml_throws_exception()
    {
        var xml = "<InvalidElement></InvalidElement>";
        var doc = XDocument.Parse(xml);

        Should.Throw<FormatException>(() => _parser.Parse(doc));
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_missing_destination_still_succeeds()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<samlp:LogoutRequest xmlns:samlp=""urn:oasis:names:tc:SAML:2.0:protocol""
                     xmlns:saml=""urn:oasis:names:tc:SAML:2.0:assertion""
                     ID=""_test-id""
                     Version=""2.0""
                     IssueInstant=""2024-01-01T00:00:00Z"">
    <saml:Issuer>https://sp.example.com</saml:Issuer>
    <saml:NameID>user@example.com</saml:NameID>
    <samlp:SessionIndex>_session123</samlp:SessionIndex>
</samlp:LogoutRequest>";
        var doc = XDocument.Parse(xml);

        var result = _parser.Parse(doc);

        result.ShouldNotBeNull();
        result.Destination.ShouldBeNull();
    }
}
