// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

/// <summary>
/// Xml writer for Saml classes
/// </summary>
public partial class SamlXmlWriter : ISamlXmlWriter
{
    /// <inheritdoc/>
    public virtual X509Certificate2? AssertionSigningCertificate { get; set; }

    /// <summary>
    /// Map of namespace prefixes to full namespace Uris.
    /// </summary>
    protected virtual IDictionary<string, string> NamespacePrefixMap { get; set; } =
        new Dictionary<string, string>
        {
            { SamlConstants.Namespaces.MetadataPrefix, SamlConstants.Namespaces.Metadata },
            { SamlConstants.Namespaces.SamlPrefix, SamlConstants.Namespaces.Assertion },
            { SamlConstants.Namespaces.SamlpPrefix, SamlConstants.Namespaces.Protocol },
            { SamlConstants.Namespaces.XmlSignaturePrefix, SamlConstants.Namespaces.XmlSignature }
        };

    /// <summary>
    /// Append an element with a specified namespace prefix, using the writer's
    /// NamespacePrefixMap
    /// </summary>
    /// <param name="node">Parent node</param>
    /// <param name="localName">local name of new element</param>
    /// <param name="namespacePrefix">Namespace prefix. The actual namespace URL is
    /// looked up in <see cref="NamespacePrefixMap"/></param>
    /// <returns>The new element</returns>
    protected virtual XmlElement AppendElement(XmlNode node, string namespacePrefix, string localName)
    {
        var ownerDoc = node as XmlDocument ?? node.OwnerDocument ??
            throw new InvalidOperationException("Owning document cannot be resolved");

        if (!NamespacePrefixMap.TryGetValue(namespacePrefix, out var namespaceUri))
        {
            throw new ArgumentException($"Namespace prefix {namespacePrefix} is not mapped", nameof(namespacePrefix));
        }

        var element = ownerDoc.CreateElement(namespacePrefix, localName, namespaceUri);

        node.AppendChild(element);

        return element;
    }
}
