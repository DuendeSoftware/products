// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Xml.Linq;

namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    static class XDocumentExtensions
    {
        extension(XDocument xDocument)
        {
            public string ToStringWithXmlDeclaration()
            {
                if (xDocument.Declaration != null)
                {
                    return xDocument.Declaration?.ToString() + "\r\n" + xDocument.ToString();
                }

                return xDocument.ToString();
            }
        }
    }
}
