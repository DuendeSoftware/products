// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Xml;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class DisplayClaim
    {
        public string ClaimType { get; private set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string DisplayValue { get; set; }
        public bool? Optional { get; set; }
        public bool WriteOptionalAttribute { get; set; }
        public string Value { get; set; }
        public ICollection<XmlElement> StructuredValue { get; set; }
        public EncryptedValue EncryptedValue { get; set; }
        public ConstrainedValue ConstrainedValue { get; set; }

        public DisplayClaim(string claimType)
        {
            if (claimType == null)
            {
                throw new ArgumentNullException(nameof(claimType));
            }
            ClaimType = claimType;
        }
    }
}
