// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class SingleLogoutService : Endpoint
    {
        public SingleLogoutService()
        {
        }

        public SingleLogoutService(Uri binding, Uri location) :
            base(binding, location)
        {
        }
    }
}
