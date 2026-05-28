// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Xml;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Xml;
using SamlAttribute = Duende.IdentityServer.Saml.SamlAttribute;

namespace UnitTests.Saml;

public sealed class SamlAttributeSerializationTests
{
    private const string Category = "SamlAttribute Serialization";

    private readonly SamlXmlWriter _writer = new();
    private readonly SamlXmlReader _reader = new();

    [Fact]
    [Trait("Category", Category)]
    public void WriteAttributeWithNameFormatProducesNameFormatXmlAttribute()
    {
        var response = CreateResponseWithAttribute(new SamlAttribute
        {
            Name = "email",
            NameFormat = "urn:oasis:names:tc:SAML:2.0:attrname-format:uri",
            Values = ["alice@example.com"]
        });

        var doc = _writer.Write(response);

        var attr = SelectAttribute(doc);
        attr.ShouldNotBeNull();
        attr.GetAttribute("NameFormat").ShouldBe("urn:oasis:names:tc:SAML:2.0:attrname-format:uri");
    }

    [Fact]
    [Trait("Category", Category)]
    public void WriteAttributeWithFriendlyNameProducesFriendlyNameXmlAttribute()
    {
        var response = CreateResponseWithAttribute(new SamlAttribute
        {
            Name = "urn:oid:0.9.2342.19200300.100.1.3",
            FriendlyName = "mail",
            Values = ["alice@example.com"]
        });

        var doc = _writer.Write(response);

        var attr = SelectAttribute(doc);
        attr.ShouldNotBeNull();
        attr.GetAttribute("FriendlyName").ShouldBe("mail");
    }

    [Fact]
    [Trait("Category", Category)]
    public void WriteAttributeWithoutNameFormatOmitsNameFormatXmlAttribute()
    {
        var response = CreateResponseWithAttribute(new SamlAttribute
        {
            Name = "email",
            Values = ["alice@example.com"]
        });

        var doc = _writer.Write(response);

        var attr = SelectAttribute(doc);
        attr.ShouldNotBeNull();
        attr.HasAttribute("NameFormat").ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public void WriteAttributeWithoutFriendlyNameOmitsFriendlyNameXmlAttribute()
    {
        var response = CreateResponseWithAttribute(new SamlAttribute
        {
            Name = "email",
            Values = ["alice@example.com"]
        });

        var doc = _writer.Write(response);

        var attr = SelectAttribute(doc);
        attr.ShouldNotBeNull();
        attr.HasAttribute("FriendlyName").ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RoundTripPreservesNameFormat()
    {
        var response = CreateResponseWithAttribute(new SamlAttribute
        {
            Name = "email",
            NameFormat = "urn:oasis:names:tc:SAML:2.0:attrname-format:uri",
            Values = ["alice@example.com"]
        });

        var doc = _writer.Write(response);
        var traverser = new XmlTraverser(doc.DocumentElement!);
        var parsed = await _reader.ReadResponseAsync(traverser, CancellationToken.None);

        var attribute = parsed.Assertions.Single().Attributes.Single();
        attribute.Name.ShouldBe("email");
        attribute.NameFormat.ShouldBe("urn:oasis:names:tc:SAML:2.0:attrname-format:uri");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RoundTripPreservesFriendlyName()
    {
        var response = CreateResponseWithAttribute(new SamlAttribute
        {
            Name = "urn:oid:0.9.2342.19200300.100.1.3",
            FriendlyName = "mail",
            Values = ["alice@example.com"]
        });

        var doc = _writer.Write(response);
        var traverser = new XmlTraverser(doc.DocumentElement!);
        var parsed = await _reader.ReadResponseAsync(traverser, CancellationToken.None);

        var attribute = parsed.Assertions.Single().Attributes.Single();
        attribute.Name.ShouldBe("urn:oid:0.9.2342.19200300.100.1.3");
        attribute.FriendlyName.ShouldBe("mail");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RoundTripWithNullNameFormatReturnsNull()
    {
        var response = CreateResponseWithAttribute(new SamlAttribute
        {
            Name = "email",
            Values = ["alice@example.com"]
        });

        var doc = _writer.Write(response);
        var traverser = new XmlTraverser(doc.DocumentElement!);
        var parsed = await _reader.ReadResponseAsync(traverser, CancellationToken.None);

        var attribute = parsed.Assertions.Single().Attributes.Single();
        attribute.NameFormat.ShouldBeNull();
        attribute.FriendlyName.ShouldBeNull();
    }

    private static Response CreateResponseWithAttribute(SamlAttribute attribute)
    {
        var assertion = new Assertion
        {
            Issuer = "https://idp.example.com",
            IssueInstant = DateTime.UtcNow,
            Subject = new Subject
            {
                NameId = new NameId { Value = "alice", Format = "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified" }
            }
        };
        assertion.Attributes.Add(attribute);

        return new Response
        {
            Issuer = "https://idp.example.com",
            IssueInstant = DateTime.UtcNow,
            Destination = "https://sp.example.com/acs",
            Status = new SamlStatus
            {
                StatusCode = new StatusCode { Value = SamlStatusCodes.Success }
            },
            Assertions = { assertion }
        };
    }

    private static XmlElement? SelectAttribute(XmlDocument doc)
    {
        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("saml", SamlConstants.Namespaces.Assertion);
        return doc.SelectSingleNode("//saml:Attribute", nsmgr) as XmlElement;
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReadAttributeWithXsiNilTrueReturnsNullValue()
    {
        var attribute = await ReadAttributeFromResponseXml(
            """<saml:AttributeValue xsi:nil="true"/>""");

        attribute.Values.ShouldHaveSingleItem();
        attribute.Values[0].ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReadAttributeWithXsiNilOneReturnsNullValue()
    {
        var attribute = await ReadAttributeFromResponseXml(
            """<saml:AttributeValue xsi:nil="1"/>""");

        attribute.Values.ShouldHaveSingleItem();
        attribute.Values[0].ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReadAttributeWithXsiNilFalseReturnsTextContent()
    {
        var attribute = await ReadAttributeFromResponseXml(
            """<saml:AttributeValue xsi:nil="false">alice@example.com</saml:AttributeValue>""");

        attribute.Values.ShouldHaveSingleItem();
        attribute.Values[0].ShouldBe("alice@example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReadAttributeWithoutXsiNilReturnsTextContent()
    {
        var attribute = await ReadAttributeFromResponseXml(
            """<saml:AttributeValue>alice@example.com</saml:AttributeValue>""");

        attribute.Values.ShouldHaveSingleItem();
        attribute.Values[0].ShouldBe("alice@example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReadAttributeWithMultipleValuesIncludingNilParsesAll()
    {
        var attribute = await ReadAttributeFromResponseXml("""
                <saml:AttributeValue>admin</saml:AttributeValue>
                <saml:AttributeValue xsi:nil="true"/>
                <saml:AttributeValue>user</saml:AttributeValue>
            """);

        attribute.Values.Count.ShouldBe(3);
        attribute.Values[0].ShouldBe("admin");
        attribute.Values[1].ShouldBeNull();
        attribute.Values[2].ShouldBe("user");
    }

    private async Task<SamlAttribute> ReadAttributeFromResponseXml(string attributeValuesXml)
    {
        var xml = $"""
            <samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                            xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                            xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                            ID="_resp" Version="2.0" IssueInstant="2024-01-01T00:00:00Z"
                            Destination="https://sp.example.com/acs">
                <saml:Issuer>https://idp.example.com</saml:Issuer>
                <samlp:Status>
                    <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success"/>
                </samlp:Status>
                <saml:Assertion ID="_assert" Version="2.0" IssueInstant="2024-01-01T00:00:00Z">
                    <saml:Issuer>https://idp.example.com</saml:Issuer>
                    <saml:Subject>
                        <saml:NameID>alice</saml:NameID>
                    </saml:Subject>
                    <saml:AttributeStatement>
                        <saml:Attribute Name="test">
                            {attributeValuesXml}
                        </saml:Attribute>
                    </saml:AttributeStatement>
                </saml:Assertion>
            </samlp:Response>
            """;

        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var traverser = new XmlTraverser(doc.DocumentElement!);
        var response = await _reader.ReadResponseAsync(traverser, CancellationToken.None);
        return response.Assertions.Single().Attributes.Single();
    }
}
