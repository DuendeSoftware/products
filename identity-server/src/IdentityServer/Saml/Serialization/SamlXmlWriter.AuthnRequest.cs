// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <inheritdoc/>
    public virtual XmlDocument Write(AuthnRequest authnRequest)
    {
        var xmlDoc = new XmlDocument();

        Append(xmlDoc, authnRequest);

        return xmlDoc;
    }

    /// <summary>
    /// Append the authnrequest as a child node
    /// </summary>
    /// <param name="node">parent node</param>
    /// <param name="authnRequest">AuthnRequest</param>
    protected virtual void Append(XmlNode node, AuthnRequest authnRequest)
    {
        var xe = Append(node, authnRequest, SamlConstants.Elements.AuthnRequest);
        xe.SetAttributeIfValue(SamlConstants.Attributes.AssertionConsumerServiceURL, authnRequest.AssertionConsumerServiceUrl);
        AppendIfValue(xe, authnRequest.Issuer, SamlConstants.Elements.Issuer);
    }
}
