// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <inheritdoc/>
    public virtual XmlDocument Write(LogoutRequest logoutRequest)
    {
        var xmlDoc = new XmlDocument();

        Append(xmlDoc, logoutRequest);

        return xmlDoc;
    }

    /// <summary>
    /// Append the LogoutRequest as a child node
    /// </summary>
    /// <param name="node">Parent node</param>
    /// <param name="logoutRequest">LogoutRequest</param>
    protected virtual XmlElement Append(XmlNode node, LogoutRequest logoutRequest)
    {
        var element = Append(node, (RequestAbstractType)logoutRequest, SamlConstants.Elements.LogoutRequest);

        element.SetAttributeIfValue(SamlConstants.Attributes.Reason, logoutRequest.Reason);
        element.SetAttributeIfValue(SamlConstants.Attributes.NotOnOrAfter, logoutRequest.NotOnOrAfter);

        AppendIfValue(element, logoutRequest.Issuer, SamlConstants.Elements.Issuer);
        AppendIfValue(element, logoutRequest.NameId, SamlConstants.Elements.NameID);

        if (logoutRequest.SessionIndex != null)
        {
            var sessionIndexEl = AppendElement(element, SamlConstants.Namespaces.SamlpPrefix, SamlConstants.Elements.SessionIndex);
            sessionIndexEl.InnerText = logoutRequest.SessionIndex;
        }

        return element;
    }
}
