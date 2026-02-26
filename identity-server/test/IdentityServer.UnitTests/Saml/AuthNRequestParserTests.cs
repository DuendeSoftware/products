// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.SingleSignin;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Saml;

/// <summary>
/// Unit tests for AuthNRequestParser, focusing on NameIDPolicy parsing
/// </summary>
public class AuthNRequestParserTests
{
    private const string Category = "SAML AuthN Request Parser";

    private readonly AuthNRequestParser _parser = new(NullLogger<AuthNRequestParser>.Instance);

    private static XDocument CreateAuthNRequest(string? nameIdPolicyXml = null)
    {
        var xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<samlp:AuthnRequest xmlns:samlp=""urn:oasis:names:tc:SAML:2.0:protocol""
                    xmlns:saml=""urn:oasis:names:tc:SAML:2.0:assertion""
                    ID=""_test123""
                    Version=""2.0""
                    IssueInstant=""2024-01-01T00:00:00Z""
                    Destination=""https://idp.example.com/saml/sso"">
    <saml:Issuer>https://sp.example.com</saml:Issuer>
    {nameIdPolicyXml ?? ""}
</samlp:AuthnRequest>";

        return XDocument.Parse(xml);
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_with_format_only_should_succeed()
    {
        // Arrange
        var nameIdPolicyXml = @"<samlp:NameIDPolicy xmlns:samlp=""urn:oasis:names:tc:SAML:2.0:protocol""
                                                    Format=""urn:oasis:names:tc:SAML:2.0:nameid-format:persistent""/>";
        var doc = CreateAuthNRequest(nameIdPolicyXml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        result.NameIdPolicy.ShouldNotBeNull();
        result.NameIdPolicy.Format.ShouldBe("urn:oasis:names:tc:SAML:2.0:nameid-format:persistent");
        result.NameIdPolicy.SPNameQualifier.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_with_sp_name_qualifier_should_succeed()
    {
        // Arrange
        var nameIdPolicyXml = @"<samlp:NameIDPolicy xmlns:samlp=""urn:oasis:names:tc:SAML:2.0:protocol""
                                                    SPNameQualifier=""https://custom.sp.com""/>";
        var doc = CreateAuthNRequest(nameIdPolicyXml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        result.NameIdPolicy.ShouldNotBeNull();
        result.NameIdPolicy.SPNameQualifier.ShouldBe("https://custom.sp.com");
        result.NameIdPolicy.Format.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_with_all_attributes_should_succeed()
    {
        // Arrange
        var nameIdPolicyXml = @"<samlp:NameIDPolicy xmlns:samlp=""urn:oasis:names:tc:SAML:2.0:protocol""
                                                    Format=""urn:oasis:names:tc:SAML:2.0:nameid-format:transient""
                                                    SPNameQualifier=""https://sp.example.com""/>";
        var doc = CreateAuthNRequest(nameIdPolicyXml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        result.NameIdPolicy.ShouldNotBeNull();
        result.NameIdPolicy.Format.ShouldBe("urn:oasis:names:tc:SAML:2.0:nameid-format:transient");
        result.NameIdPolicy.SPNameQualifier.ShouldBe("https://sp.example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_without_name_id_policy_should_return_null()
    {
        // Arrange
        var doc = CreateAuthNRequest(null);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        result.NameIdPolicy.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_with_empty_name_id_policy_element_should_return_non_null()
    {
        // Arrange
        var nameIdPolicyXml = @"<samlp:NameIDPolicy xmlns:samlp=""urn:oasis:names:tc:SAML:2.0:protocol""/>";
        var doc = CreateAuthNRequest(nameIdPolicyXml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        result.NameIdPolicy.ShouldNotBeNull();
        result.NameIdPolicy.Format.ShouldBeNull();
        result.NameIdPolicy.SPNameQualifier.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_with_whitespace_in_format_should_trim()
    {
        // Arrange
        var nameIdPolicyXml = @"<samlp:NameIDPolicy xmlns:samlp=""urn:oasis:names:tc:SAML:2.0:protocol""
                                                    Format=""  urn:oasis:names:tc:SAML:2.0:nameid-format:persistent  ""/>";
        var doc = CreateAuthNRequest(nameIdPolicyXml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        result.NameIdPolicy.ShouldNotBeNull();
        result.NameIdPolicy.Format.ShouldBe("urn:oasis:names:tc:SAML:2.0:nameid-format:persistent");
    }

    [Fact]
    [Trait("Category", Category)]
    public void parse_with_whitespace_in_sp_name_qualifier_should_trim()
    {
        // Arrange
        var nameIdPolicyXml = @"<samlp:NameIDPolicy xmlns:samlp=""urn:oasis:names:tc:SAML:2.0:protocol""
                                                    SPNameQualifier=""  https://sp.example.com  ""/>";
        var doc = CreateAuthNRequest(nameIdPolicyXml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        result.NameIdPolicy.ShouldNotBeNull();
        result.NameIdPolicy.SPNameQualifier.ShouldBe("https://sp.example.com");
    }
}
