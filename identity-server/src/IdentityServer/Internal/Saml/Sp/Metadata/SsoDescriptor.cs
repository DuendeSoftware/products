// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class SsoDescriptor : RoleDescriptor
    {
        public ICollection<SingleLogoutService> SingleLogoutServices { get; private set; } =
            new Collection<SingleLogoutService>();
        public ICollection<ManageNameIDService> ManageNameIDServices { get; private set; } =
            new Collection<ManageNameIDService>();
        public ICollection<NameIDFormat> NameIdentifierFormats { get; private set; } =
            new Collection<NameIDFormat>();
    }
}
