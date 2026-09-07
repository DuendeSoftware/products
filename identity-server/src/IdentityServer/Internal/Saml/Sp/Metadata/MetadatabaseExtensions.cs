// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Cryptography.X509Certificates;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    /// <summary>
    /// Extensions for Metadatabase.
    /// </summary>
    internal static class MetadataBaseExtensions
    {
        extension(MetadataBase metadata)
        {
            /// <summary>
            /// Use a MetadataSerializer to create an XML string out of metadata.
            /// </summary>
            /// <param name="signingCertificate">Certificate to sign the metadata
            /// with. Supply null to not sign.</param>
            /// <param name="signingAlgorithm">Algorithm to use when signing.</param>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
            public string ToXmlString(
                X509Certificate2 signingCertificate,
                string signingAlgorithm)
            {
                var serializer = ExtendedMetadataSerializer.WriterInstance;

                var xmlDoc = XmlHelpers.CreateSafeXmlDocument();
                using (var xmlWriter = xmlDoc.CreateNavigator().AppendChild())
                {
                    serializer.WriteMetadata(xmlWriter, metadata);
                }

                if (signingCertificate != null)
                {
                    xmlDoc.Sign(signingCertificate, true, signingAlgorithm);
                }

                return xmlDoc.OuterXml;
            }
        }
    }
}
