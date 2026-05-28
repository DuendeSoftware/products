// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Reads a NameIdPolicy.
    /// </summary>
    /// <param name="source">Source data</param>
    /// <returns>read</returns>
    protected NameIdPolicy ReadNameIdPolicy(XmlTraverser source)
    {
        var result = Create<NameIdPolicy>();
        ReadAttributes(source, result);

        return result;
    }

    /// <summary>
    /// Read NameIdPolicy attributes.
    /// </summary>
    /// <param name="source">Source</param>
    /// <param name="nameIdPolicy">NameIdPolicy</param>
    protected virtual void ReadAttributes(XmlTraverser source, NameIdPolicy nameIdPolicy)
    {
        nameIdPolicy.Format = source.GetAbsoluteUriAttribute(SamlConstants.Attributes.Format);
        nameIdPolicy.SPNameQualifier = source.GetAttribute(SamlConstants.Attributes.SPNameQualifier);
        nameIdPolicy.AllowCreate = source.GetBoolAttribute(SamlConstants.Attributes.AllowCreate);
    }
}
