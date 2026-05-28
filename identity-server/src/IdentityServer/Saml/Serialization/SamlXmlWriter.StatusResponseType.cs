// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <summary>
    /// Append a type derived from StatusResponseType, with the given name
    /// </summary>
    /// <param name="parent">Parent node to append child element to</param>
    /// <param name="statusResponseType">data</param>
    /// <param name="localName">Local name of the new element</param>
    protected virtual XmlElement Append(XmlNode parent, StatusResponseType statusResponseType, string localName)
    {
        var element = AppendElement(parent, SamlConstants.Namespaces.SamlpPrefix, localName);
        element.SetAttribute(SamlConstants.Attributes.ID, statusResponseType.Id);
        element.SetAttribute(SamlConstants.Attributes.Version, statusResponseType.Version);
        element.SetAttribute(SamlConstants.Attributes.IssueInstant, statusResponseType.IssueInstant);
        element.SetAttributeIfValue(SamlConstants.Attributes.Destination, statusResponseType.Destination);
        element.SetAttributeIfValue(SamlConstants.Attributes.InResponseTo, statusResponseType.InResponseTo);

        if (statusResponseType.Issuer != null)
        {
            Append(element, statusResponseType.Issuer, SamlConstants.Elements.Issuer);
        }
        Append(element, statusResponseType.Status);

        return element;
    }
}
