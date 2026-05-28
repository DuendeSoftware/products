// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <summary>
    /// Append an AttributeStatement element
    /// </summary>
    /// <param name="parent">Parent node</param>
    /// <param name="attributeStatement">data</param>
    protected virtual XmlElement Append(XmlNode parent, AttributeStatement attributeStatement)
    {
        var statementElement = AppendElement(parent, SamlConstants.Namespaces.SamlPrefix, SamlConstants.Elements.AttributeStatement);
        foreach (var attribute in attributeStatement)
        {
            var attributeElement = AppendElement(statementElement, SamlConstants.Namespaces.SamlPrefix, SamlConstants.Elements.Attribute);
            attributeElement.SetAttribute(SamlConstants.Attributes.Name, attribute.Name);
            attributeElement.SetAttributeIfValue(SamlConstants.Attributes.NameFormat, attribute.NameFormat);
            attributeElement.SetAttributeIfValue(SamlConstants.Attributes.FriendlyName, attribute.FriendlyName);
            foreach (var value in attribute.Values)
            {
                var valueElement = AppendElement(attributeElement, SamlConstants.Namespaces.SamlPrefix, SamlConstants.Elements.AttributeValue);

                if (value == null)
                {
                    valueElement.SetAttribute("nil", SamlConstants.Namespaces.Xsi, "true");
                }
                else
                {
                    if (value.Length > 0)
                    {
                        valueElement.InnerText = value;
                    }
                }
            }
        }

        return statementElement;
    }
}
