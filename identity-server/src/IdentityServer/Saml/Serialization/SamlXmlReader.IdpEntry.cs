// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Read a IdpEntry.
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <returns>IdpEntry</returns>
    protected IdpEntry ReadIdpEntry(XmlTraverser source)
    {
        var result = Create<IdpEntry>();

        ReadAttributes(source, result);

        return result;
    }
    /// <summary>
    /// Read IdpEntry attributes.
    /// </summary>
    /// <param name="source">Source</param>
    /// <param name="result">result</param>
    protected virtual void ReadAttributes(XmlTraverser source, IdpEntry result)
    {
        result.ProviderId = source.GetRequiredAbsoluteUriAttribute(SamlConstants.Attributes.ProviderID);
        result.Name = source.GetAttribute(SamlConstants.Attributes.Name);
        result.Loc = source.GetAbsoluteUriAttribute(SamlConstants.Attributes.Loc);
    }
}
