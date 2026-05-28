// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Saml.Samlp;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <inheritdoc/>
    public virtual XmlDocument Write(LogoutResponse logoutResponse)
    {
        var xmlDoc = new XmlDocument();

        Append(xmlDoc, logoutResponse);

        return xmlDoc;
    }

    /// <summary>
    /// Append the LogoutResponse as a child node
    /// </summary>
    /// <param name="node">Parent node</param>
    /// <param name="logoutResponse">LogoutResponse</param>
    protected virtual XmlElement Append(XmlNode node, LogoutResponse logoutResponse) =>
        Append(node, logoutResponse, SamlConstants.Elements.LogoutResponse);
}
