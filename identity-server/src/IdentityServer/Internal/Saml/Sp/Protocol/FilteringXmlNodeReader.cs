// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Xml;

namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    class FilteringXmlNodeReader : XmlNodeReader
    {
        string filterNamespace;
        string filterNode;

        public FilteringXmlNodeReader(string filterNamespace, string filterNode, XmlNode source)
            : base(source)
        {
            this.filterNamespace = filterNamespace;
            this.filterNode = filterNode;
        }

        public override bool Read()
        {
            var result = base.Read();

            if (result
                && LocalName == filterNode
                && NamespaceURI == filterNamespace)
            {
                Skip();
                // Skip calls read assume that the result was true.
                result = true;
            }

            return result;
        }
    }
}
