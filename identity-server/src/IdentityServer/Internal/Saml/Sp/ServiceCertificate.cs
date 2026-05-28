// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Cryptography.X509Certificates;

namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Service Certificate definition
    /// </summary>
    internal class ServiceCertificate
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public ServiceCertificate()
        {
            Use = CertificateUse.Both;
            Status = CertificateStatus.Current;
            MetadataPublishOverride = MetadataPublishOverrideType.None;
        }

        /// <summary>
        /// X509 Certificate
        /// </summary>
        public X509Certificate2 Certificate { get; set; }

        /// <summary>
        /// Is this certificate for current or future use?
        /// </summary>
        public CertificateStatus Status { get; set; }

        /// <summary>
        /// What is the intended use of this certificate.
        /// </summary>
        public CertificateUse Use { get; set; }

        /// <summary>
        /// How should we override the metadata publishing rules?
        /// </summary>
        public MetadataPublishOverrideType MetadataPublishOverride { get; set; }
    }
}
