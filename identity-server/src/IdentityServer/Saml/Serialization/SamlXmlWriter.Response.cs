// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Saml.Samlp;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <inheritdoc/>
    public virtual XmlDocument Write(Response response)
    {
        var xmlDoc = new XmlDocument();

        Append(xmlDoc, response);

        return xmlDoc;
    }

    /// <summary>
    /// Append the response as a child node
    /// </summary>
    /// <param name="node">Parent node</param>
    /// <param name="response">Saml response</param>
    protected virtual XmlElement Append(XmlNode node, Response response)
    {
        var responseElement = Append(node, response, SamlConstants.Elements.Response);

        foreach (var assertion in response.Assertions)
        {
            Append(responseElement, assertion);
        }

        return responseElement;
    }
}
