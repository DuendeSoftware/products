// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class EncryptedValue
    {
        public Uri DecryptionCondition { get; set; }
        public EncryptedData EncryptedData { get; set; }
    }
}
