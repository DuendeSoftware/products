// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <summary>
    /// Append an AuthnStatement element
    /// </summary>
    /// <param name="parent">Parent node</param>
    /// <param name="authnStatement">authnStatement</param>
    protected virtual void Append(XmlNode parent, AuthnStatement authnStatement)
    {
        var authnStatementElement = AppendElement(parent, SamlConstants.Namespaces.SamlPrefix, SamlConstants.Elements.AuthnStatement);

        authnStatementElement.SetAttribute(SamlConstants.Attributes.AuthnInstant, authnStatement.AuthnInstant);
        authnStatementElement.SetAttributeIfValue(SamlConstants.Attributes.SessionIndex, authnStatement.SessionIndex);

        if (authnStatement.AuthnContext != null)
        {
            var authnContextElement = AppendElement(authnStatementElement, SamlConstants.Namespaces.SamlPrefix, SamlConstants.Elements.AuthnContext);

            if (authnStatement.AuthnContext.AuthnContextClassRef != null)
            {
                var authnContextClassRefElement = AppendElement(authnContextElement, SamlConstants.Namespaces.SamlPrefix, SamlConstants.Elements.AuthnContextClassRef);
                authnContextClassRefElement.InnerText = authnStatement.AuthnContext.AuthnContextClassRef;
            }
        }
    }
}
