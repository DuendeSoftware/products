// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    static class StringHelpers
    {
        extension(string source)
        {
            public string NullIfEmpty()
            {
                if (string.IsNullOrEmpty(source))
                {
                    return null;
                }

                return source;
            }
        }
    }
}
