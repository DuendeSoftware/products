// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Xml;

namespace UnitTests.Saml;

public sealed class XmlTraverserTrustLevelTests
{
    private const string Category = "XmlTraverser TrustLevel";

    private static XmlNode CreateMinimalXmlNode()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<Root/>");
        return doc.DocumentElement!;
    }

    [Fact]
    [Trait("Category", Category)]
    public void ConstructorStripsHasSignatureFromInheritedTrustLevel()
    {
        var traverser = new XmlTraverser(CreateMinimalXmlNode(), TrustLevel.ConfiguredKey | TrustLevel.HasSignature);

        traverser.TrustLevel.ShouldBe(TrustLevel.ConfiguredKey);
    }

    [Fact]
    [Trait("Category", Category)]
    public void ConstructorPreservesBaseTrustLevelWhenNoHasSignature()
    {
        var traverser = new XmlTraverser(CreateMinimalXmlNode(), TrustLevel.TLS);

        traverser.TrustLevel.ShouldBe(TrustLevel.TLS);
    }

    [Fact]
    [Trait("Category", Category)]
    public void ConstructorWithNoneRemainsNone()
    {
        var traverser = new XmlTraverser(CreateMinimalXmlNode(), TrustLevel.None);

        traverser.TrustLevel.ShouldBe(TrustLevel.None);
    }

    [Fact]
    [Trait("Category", Category)]
    public void ConstructorStripsHasSignatureFromHttpLevel()
    {
        var traverser = new XmlTraverser(CreateMinimalXmlNode(), TrustLevel.Http | TrustLevel.HasSignature);

        traverser.TrustLevel.ShouldBe(TrustLevel.Http);
    }
}
