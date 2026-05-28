// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

public partial class SamlXmlReader
{
    /// <summary>
    /// Read an Attribute
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <returns>Attribute</returns>
    protected SamlAttribute ReadAttribute(XmlTraverser source)
    {
        var attribute = Create<SamlAttribute>();

        ReadAttributes(source, attribute);
        ReadElements(source.GetChildren(), attribute);

        return attribute;
    }

    /// <summary>
    /// Read attributes of a SamlAttribute
    /// </summary>
    /// <param name="source">Source data</param>
    /// <param name="attribute">Attribute to populate</param>
    protected virtual void ReadAttributes(XmlTraverser source, SamlAttribute attribute)
    {
        attribute.Name = source.GetRequiredAttribute(SamlConstants.Attributes.Name);
        attribute.NameFormat = source.GetAttribute(SamlConstants.Attributes.NameFormat);
        attribute.FriendlyName = source.GetAttribute(SamlConstants.Attributes.FriendlyName);
    }

    /// <summary>
    /// Read elements of a Saml attribute.
    /// </summary>
    /// <param name="source">Source data</param>
    /// <param name="attribute">Attribute to populate</param>
    protected virtual void ReadElements(XmlTraverser source, SamlAttribute attribute)
    {
        while (source.MoveNext(true))
        {
            if (source.EnsureName(SamlConstants.Elements.AttributeValue, SamlConstants.Namespaces.Assertion))
            {
                var isNil = source.GetBoolAttribute(SamlConstants.Attributes.nil, SamlConstants.Namespaces.Xsi);

                if (isNil == true)
                {
                    attribute.Values.Add(null);
                }
                else
                {
                    attribute.Values.Add(source.GetTextContents());
                }
            }
        }
    }
}
