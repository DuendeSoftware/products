// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;
using Microsoft.IdentityModel.Tokens.Saml2;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class AttributeAuthorityDescriptor : RoleDescriptor
    {
        public ICollection<AttributeService> AttributeServices { get; private set; } =
            new Collection<AttributeService>();
        public ICollection<AssertionIdRequestService> AssertionIdRequestServices { get; private set; } =
            new Collection<AssertionIdRequestService>();
        public ICollection<NameIDFormat> NameIDFormats { get; private set; } =
            new Collection<NameIDFormat>();
        public ICollection<AttributeProfile> AttributeProfiles { get; private set; } =
            new Collection<AttributeProfile>();
        public ICollection<Saml2Attribute> Attributes { get; private set; } =
            new Collection<Saml2Attribute>();
    }
}
