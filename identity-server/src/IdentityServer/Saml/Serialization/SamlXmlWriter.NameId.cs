// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <summary>
    /// Appends a node if the NameId has a value
    /// </summary>
    /// <param name="parent">Parent to append to</param>
    /// <param name="nameId">The NameId</param>
    /// <param name="localName">Local name of element</param>
    /// <returns></returns>
    public XmlElement? AppendIfValue(XmlNode parent, NameId? nameId, string localName)
    {
        if (nameId != null)
        {
            return Append(parent, nameId, localName);
        }
        return null;
    }

    /// <summary>
    /// Append a NameId
    /// </summary>
    /// <param name="parent">Parent node to append child element to</param>
    /// <param name="nameId">value</param>
    /// <param name="localName">Local name of the new element</param>
    protected virtual XmlElement Append(XmlNode parent, NameId? nameId, string localName)
    {
        var element = AppendElement(parent, SamlConstants.Namespaces.SamlPrefix, localName);
        if (nameId != null)
        {
            element.InnerText = nameId.Value;
            element.SetAttributeIfValue(SamlConstants.Attributes.Format, nameId.Format);
            element.SetAttributeIfValue(SamlConstants.Attributes.SPNameQualifier, nameId.SPNameQualifier);
            element.SetAttributeIfValue(SamlConstants.Attributes.NameQualifier, nameId.NameQualifier);
        }

        return element;
    }
}
