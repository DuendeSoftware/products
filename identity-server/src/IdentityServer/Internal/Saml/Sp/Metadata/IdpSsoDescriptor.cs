// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;
using Microsoft.IdentityModel.Tokens.Saml2;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class IdpSsoDescriptor : SsoDescriptor
    {
        public ICollection<SingleSignOnService> SingleSignOnServices { get; private set; } =
            new Collection<SingleSignOnService>();
        public ICollection<NameIDMappingService> NameIDMappingServices { get; private set; } =
            new Collection<NameIDMappingService>();
        public ICollection<AssertionIdRequestService> AssertionIDRequestServices { get; private set; } =
            new Collection<AssertionIdRequestService>();
        public ICollection<AttributeProfile> AttributeProfiles { get; private set; } =
            new Collection<AttributeProfile>();
        public ICollection<Saml2Attribute> SupportedAttributes { get; private set; } =
            new Collection<Saml2Attribute>();
        public bool? WantAuthnRequestsSigned { get; set; }
    }
}
