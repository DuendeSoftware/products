// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using Duende.IdentityServer.Internal.Saml.Sp.Protocol;

namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Extensions for NameIdFormat enum.
    /// </summary>
    internal static class NameIdFormatExtension
    {
        static Dictionary<NameIdFormat, Uri> enumToUri
            = new Dictionary<NameIdFormat, Uri>()
            {
                { NameIdFormat.Unspecified, new Uri("urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified") },
                { NameIdFormat.EmailAddress, new Uri("urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress") },
                { NameIdFormat.X509SubjectName, new Uri("urn:oasis:names:tc:SAML:1.1:nameid-format:X509SubjectName") },
                { NameIdFormat.WindowsDomainQualifiedName, new Uri("urn:oasis:names:tc:SAML:1.1:nameid-format:WindowsDomainQualifiedName") },
                { NameIdFormat.KerberosPrincipalName, new Uri("urn:oasis:names:tc:SAML:2.0:nameid-format:kerberos") },
                { NameIdFormat.EntityIdentifier, new Uri("urn:oasis:names:tc:SAML:2.0:nameid-format:entity") },
                { NameIdFormat.Persistent, new Uri("urn:oasis:names:tc:SAML:2.0:nameid-format:persistent") },
                { NameIdFormat.Transient, new Uri("urn:oasis:names:tc:SAML:2.0:nameid-format:transient") }
            };

        /// <summary>
        /// Get the full Uri for a NameIdFormat.
        /// </summary>
        /// <param name="nameIdFormat">NameIdFormat to get Uri for</param>
        /// <returns>Uri</returns>
        public static Uri GetUri(this NameIdFormat nameIdFormat)
        {
            return enumToUri[nameIdFormat];
        }
    }
}
