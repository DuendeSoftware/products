// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <summary>
    /// Append a Conditions element
    /// </summary>
    /// <param name="parent">Parent node</param>
    /// <param name="conditions">value</param>
    protected virtual void Append(XmlNode parent, Conditions conditions)
    {
        var conditionsElement = AppendElement(parent, SamlConstants.Namespaces.SamlPrefix, SamlConstants.Elements.Conditions);

        conditionsElement.SetAttributeIfValue(SamlConstants.Attributes.NotBefore, conditions.NotBefore);
        conditionsElement.SetAttributeIfValue(SamlConstants.Attributes.NotOnOrAfter, conditions.NotOnOrAfter);

        foreach (var restriction in conditions.AudienceRestrictions)
        {
            var audienceRestrictionElement = AppendElement(conditionsElement, SamlConstants.Namespaces.SamlPrefix, SamlConstants.Elements.AudienceRestriction);
            foreach (var audience in restriction.Audiences)
            {
                var audienceElement = AppendElement(audienceRestrictionElement, SamlConstants.Namespaces.SamlPrefix, SamlConstants.Elements.Audience);
                audienceElement.InnerText = audience;
            }
        }
    }
}
