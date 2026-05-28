// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class AuthnAuthorityDescriptor : RoleDescriptor
    {
        public ICollection<AuthnQueryService> AuthnQueryServices { get; private set; } =
            new Collection<AuthnQueryService>();
        public ICollection<AssertionIdRequestService> AssertionIdRequestServices { get; private set; } =
            new Collection<AssertionIdRequestService>();
        public ICollection<NameIDFormat> NameIDFormats { get; private set; } =
            new Collection<NameIDFormat>();
    }
}
