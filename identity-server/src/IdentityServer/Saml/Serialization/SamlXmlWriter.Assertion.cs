// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <summary>
    /// Append an Assertion element
    /// </summary>
    /// <param name="parent">Parent node</param>
    /// <param name="assertion">Saml assertion</param>
    /// <returns>XmlElement</returns>
    protected virtual XmlElement Append(XmlNode parent, Assertion assertion)
    {
        var element = AppendElement(parent, SamlConstants.Namespaces.SamlPrefix, SamlConstants.Elements.Assertion);
        element.SetAttribute(SamlConstants.Attributes.ID, assertion.Id);
        element.SetAttribute(SamlConstants.Attributes.Version, assertion.Version);
        element.SetAttribute(SamlConstants.Attributes.IssueInstant, assertion.IssueInstant);

        Append(element, assertion.Issuer, SamlConstants.Elements.Issuer);

        if (assertion.Subject != null)
        {
            Append(element, assertion.Subject);
        }

        if (assertion.Conditions != null)
        {
            Append(element, assertion.Conditions);
        }

        if (assertion.AuthnStatement != null)
        {
            Append(element, assertion.AuthnStatement);
        }

        if (assertion.Attributes.Count > 0)
        {
            Append(element, assertion.Attributes);
        }

        if (AssertionSigningCertificate is { } cert)
        {
            var issuerElement = element[SamlConstants.Elements.Issuer, SamlConstants.Namespaces.Assertion]
                ?? throw new InvalidOperationException("Assertion element must contain an Issuer element.");
            element.Sign(cert, issuerElement);
        }

        return element;
    }
}
