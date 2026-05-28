// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal abstract class MetadataBase
    {
        public SigningCredentials SigningCredentials { get; set; }
    }
}
