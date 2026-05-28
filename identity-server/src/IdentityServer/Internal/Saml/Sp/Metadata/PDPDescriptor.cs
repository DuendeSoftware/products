// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class PDPDescriptor : RoleDescriptor
    {
        public ICollection<AuthzService> AuthzServices { get; private set; } =
            new Collection<AuthzService>();
        public ICollection<AssertionIdRequestService> AssertionIdRequestServices { get; private set; } =
            new Collection<AssertionIdRequestService>();
        public ICollection<NameIDFormat> NameIDFormats { get; private set; } =
            new Collection<NameIDFormat>();
    }
}
