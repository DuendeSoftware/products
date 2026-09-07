// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    static class DictionaryExtensions
    {
        extension<T>(IDictionary<T, string> dictionary)
        {
            public string GetValueOrEmpty(T key)
            {
                string value;
                if (dictionary.TryGetValue(key, out value))
                {
                    return value;
                }
                return "";
            }
        }
    }
}
