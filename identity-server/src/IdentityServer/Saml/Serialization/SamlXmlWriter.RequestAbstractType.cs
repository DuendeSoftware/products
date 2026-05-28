// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <summary>
    /// Append a type derived from RequestAbstractType, with the given name.
    /// </summary>
    /// <param name="parent">Parent node to append child element to</param>
    /// <param name="request">data</param>
    /// <param name="localName">Local name of the new element.</param>
    protected virtual XmlElement Append(XmlNode parent, RequestAbstractType request, string localName)
    {
        var element = AppendElement(parent, SamlConstants.Namespaces.SamlpPrefix, localName);
        element.SetAttribute(SamlConstants.Attributes.ID, request.Id);
        element.SetAttribute(SamlConstants.Attributes.IssueInstant, request.IssueInstant);
        element.SetAttribute(SamlConstants.Attributes.Version, request.Version);
        element.SetAttributeIfValue(SamlConstants.Attributes.Destination, request.Destination);
        element.SetAttributeIfValue(SamlConstants.Attributes.Consent, request.Consent);

        return element;
    }
}
