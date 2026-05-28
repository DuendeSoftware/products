// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class EncryptionProperties
    {
        // EncryptionProperty
        public string Id { get; set; }
        public ICollection<EncryptionProperty> Properties { get; private set; } =
            new Collection<EncryptionProperty>();
    }
}
