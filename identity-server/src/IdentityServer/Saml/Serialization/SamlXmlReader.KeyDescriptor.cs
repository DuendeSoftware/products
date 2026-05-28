// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.Xml;
using System.Xml;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Metadata;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Read KeyDescriptor
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected virtual KeyDescriptor ReadKeyDescriptor(XmlTraverser source)
    {
        var result = Create<KeyDescriptor>();

        ReadAttributes(source, result);
        ReadElements(source.GetChildren(), result);

        return result;
    }

    /// <summary>
    /// Reads attributes of a KeyDescriptor
    /// </summary>
    /// <param name="source">Xml traverser to read from</param>
    /// <param name="keyDescriptor">The KeyDescriptor to populate</param>
    protected virtual void ReadAttributes(XmlTraverser source, KeyDescriptor keyDescriptor) => keyDescriptor.Use = source.GetEnumAttribute<KeyUse>(SamlConstants.Attributes.use, true) ?? KeyUse.Both;

    /// <summary>
    /// Reads the child elements of a KeyDescriptor.
    /// </summary>
    /// <param name="source">Xml traverser to read from</param>
    /// <param name="keyDescriptor">KeyDescriptor to populate</param>
    protected virtual void ReadElements(XmlTraverser source, KeyDescriptor keyDescriptor)
    {
        if (source.MoveNext()
          && source.EnsureName(SamlConstants.Elements.KeyInfo, SignedXml.XmlDsigNamespaceUrl))
        {
            source.IgnoreChildren();

            var keyInfo = new KeyInfo();
            keyInfo.LoadXml((XmlElement)source.CurrentNode!);

            keyDescriptor.KeyInfo = keyInfo;

            source.MoveNext(true);
        }
    }
}
