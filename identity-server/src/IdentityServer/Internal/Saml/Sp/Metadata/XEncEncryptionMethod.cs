// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class XEncEncryptionMethod
    {
        public int KeySize { get; set; }
        public byte[] OAEPparams { get; set; }
        public Uri Algorithm { get; set; }
    }
}
