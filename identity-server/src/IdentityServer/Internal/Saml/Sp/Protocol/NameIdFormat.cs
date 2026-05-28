// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    /// <summary>
    /// The NameId Format.
    /// </summary>
    internal enum NameIdFormat
    {
        /// <summary>
        /// No NameId format has been configured. No format will be included
        /// in AuthnRequests and metadata.
        /// </summary>
        NotConfigured = 0,

        /// <summary>
        /// 8.3.1 Unspecified
        /// </summary>
        Unspecified = 1,

        /// <summary>
        /// 8.3.2 Email Address
        /// </summary>
        EmailAddress = 2,

        /// <summary>
        /// 8.3.3 X.509 Subject Name
        /// </summary>
        X509SubjectName = 3,

        /// <summary>
        /// 8.3.4 Windows Domain Qualified Name
        /// </summary>
        WindowsDomainQualifiedName = 4,

        /// <summary>
        /// 8.3.5 Kerberos Principal Name
        /// </summary>
        KerberosPrincipalName = 5,

        /// <summary>
        /// 8.3.6 Entity Identifier
        /// </summary>
        EntityIdentifier = 6,

        /// <summary>
        /// 8.3.7 Persistent Identifier
        /// </summary>
        Persistent = 7,

        /// <summary>
        /// 8.3.8 Transient Identifier
        /// </summary>
        Transient = 8
    }
}
