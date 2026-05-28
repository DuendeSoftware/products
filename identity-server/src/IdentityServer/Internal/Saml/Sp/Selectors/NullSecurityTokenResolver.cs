// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using Duende.IdentityServer.Internal.Saml.Sp.Tokens;

namespace Duende.IdentityServer.Internal.Saml.Sp.Selectors
{
    internal class NullSecurityTokenResolver : SecurityTokenResolver
    {
        private NullSecurityTokenResolver()
        {
        }

        protected override bool TryResolveTokenCore(SecurityKeyIdentifier keyIdentifier, out SecurityToken token)
        {
            token = null;
            return false;
        }

        protected override bool TryResolveTokenCore(SecurityKeyIdentifierClause keyIdentifier, out SecurityToken token)
        {
            token = null;
            return false;
        }

        protected override bool TryResolveSecurityKeyCore(SecurityKeyIdentifierClause keyIdentifier, out SecurityKey key)
        {
            key = null;
            return false;
        }

        public static SecurityTokenResolver Instance { get; } = new NullSecurityTokenResolver();
    }
}
