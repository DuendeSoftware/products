// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    static class StringHelpers
    {
        public static string NullIfEmpty(this string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return null;
            }

            return source;
        }
    }
}
