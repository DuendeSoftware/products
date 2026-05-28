// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Saml.Samlp;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <summary>
    /// Append a Saml status
    /// </summary>
    /// <param name="parent">Parent node to append child element to</param>
    /// <param name="status">value</param>
    protected virtual void Append(XmlNode parent, SamlStatus status)
    {
        var statusElement = AppendElement(parent, SamlConstants.Namespaces.SamlpPrefix, SamlConstants.Elements.Status);

        AppendStatusCode(statusElement, status.StatusCode);

        if (status.StatusMessage != null)
        {
            var messageElement = AppendElement(statusElement, SamlConstants.Namespaces.SamlpPrefix, SamlConstants.Elements.StatusMessage);
            // InnerText assignment is safe — the DOM automatically escapes XML-significant characters.
            messageElement.InnerText = status.StatusMessage;
        }
    }

    /// <summary>
    /// Appends status code element
    /// </summary>
    /// <param name="parent">Parent node to append the child element to</param>
    /// <param name="statusCode">Status code from which to create the status element</param>
    protected virtual void AppendStatusCode(XmlNode parent, StatusCode statusCode)
    {
        var element = AppendElement(parent, SamlConstants.Namespaces.SamlpPrefix, SamlConstants.Elements.StatusCode);
        element.SetAttribute(SamlConstants.Attributes.Value, statusCode.Value);

        if (statusCode.NestedStatusCode != null)
        {
            AppendStatusCode(element, statusCode.NestedStatusCode);
        }
    }
}
