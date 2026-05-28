// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;
using System.Xml;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class CipherReference
    {
        public Uri Uri { get; set; }
        public ICollection<XmlElement> Transforms { get; private set; } =
            new Collection<XmlElement>();
    }
}
