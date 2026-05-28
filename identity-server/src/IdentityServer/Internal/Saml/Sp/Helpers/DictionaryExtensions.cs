// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    static class DictionaryExtensions
    {
        public static string GetValueOrEmpty<T>(this IDictionary<T, string> dictionary, T key)
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
