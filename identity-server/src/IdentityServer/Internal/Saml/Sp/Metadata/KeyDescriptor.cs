// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class KeyDescriptor
    {
        public DSigKeyInfo KeyInfo { get; set; }
        public KeyType Use { get; set; } = KeyType.Unspecified;
        public ICollection<EncryptionMethod> EncryptionMethods { get; private set; } =
            new Collection<EncryptionMethod>();

        public KeyDescriptor()
        {
        }
    }
}
