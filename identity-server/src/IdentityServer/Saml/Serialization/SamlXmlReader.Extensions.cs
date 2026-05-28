// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Read Extensions node.
    /// </summary>
    /// <param name="source">Soure to read from</param>
    /// <returns>Extensions</returns>
    protected virtual Common.Extensions ReadExtensions(XmlTraverser source)
    {
        source.IgnoreChildren();

        return Create<Common.Extensions>();
    }
}
