// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Reads Conditions.
    /// </summary>
    /// <param name="source">Source data</param>
    /// <returns>Conditions read</returns>
    protected Conditions ReadConditions(XmlTraverser source)
    {
        var result = Create<Conditions>();

        ReadAttributes(source, result);
        ReadElements(source.GetChildren(), result);

        return result;
    }

    /// <summary>
    /// Read attributes of a Conditions.
    /// </summary>
    /// <param name="source">Source</param>
    /// <param name="conditions">Conditions</param>
    protected virtual void ReadAttributes(XmlTraverser source, Conditions conditions)
    {
        conditions.NotBefore = source.GetDateTimeAttribute(SamlConstants.Attributes.NotBefore);
        conditions.NotOnOrAfter = source.GetDateTimeAttribute(SamlConstants.Attributes.NotOnOrAfter);
    }

    /// <summary>
    /// Reads elements of a Conditions.
    /// </summary>
    /// <param name="source">Source Xml Reader</param>
    /// <param name="conditions">Conditions to populate</param>
    protected virtual void ReadElements(XmlTraverser source, Conditions conditions)
    {
        source.MoveNext(true);

        // The XML schema allows custom conditions. Anyone that wants to support
        // those would have to extend the reader and override this method to handle
        // reading of those.

        while (source.HasName(SamlConstants.Elements.AudienceRestriction, SamlConstants.Namespaces.Assertion))
        {
            conditions.AudienceRestrictions.Add(ReadAudienceRestriction(source));
            source.MoveNext(true);
        }

        if (source.HasName(SamlConstants.Elements.OneTimeUse, SamlConstants.Namespaces.Assertion))
        {
            conditions.OneTimeUse = true;
            source.MoveNext(true);
        }
    }
}
