// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    static class UriExtensions
    {
        extension(Uri uri)
        {
            public bool IsHttps()
            {
                return string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
