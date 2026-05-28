// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class EncryptedData
    {
        public string Id { get; set; }
        public Uri Type { get; set; }
        public string MimeType { get; set; }
        public Uri Encoding { get; set; }
        public XEncEncryptionMethod EncryptionMethod { get; set; }
        public DSigKeyInfo KeyInfo { get; set; }
        public CipherData CipherData { get; set; }
        public EncryptionProperties EncryptionProperties { get; set; }
    }
}
