// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    static class UriExtensions
    {
        public static bool IsHttps(this Uri uri)
        {
            return string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase);
        }
    }
}
