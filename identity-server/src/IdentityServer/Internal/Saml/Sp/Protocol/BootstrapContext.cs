// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    internal class BootstrapContext
    {
        public SecurityTokenHandler SecurityTokenHandler { get; private set; }
        public SecurityToken SecurityToken { get; private set; }

        public BootstrapContext(SecurityToken token, SecurityTokenHandler handler)
        {
            SecurityToken = token;
            SecurityTokenHandler = handler;
        }

    }
}
