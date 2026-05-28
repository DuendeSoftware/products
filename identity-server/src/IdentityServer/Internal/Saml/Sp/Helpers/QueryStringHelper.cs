// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    /// <summary>
    /// Class implements static methods to help parse a query string.
    /// </summary>
    internal static class QueryStringHelper
    {
        /// <summary>
        /// Splits a query string into its key/value pairs. 
        /// </summary>
        /// <param name="queryString">A query string, with or without the leading '?' character.</param>
        /// <returns>A collection with the parsed keys and values.</returns>
        public static ILookup<string, string> ParseQueryString(string queryString)
        {
            if (queryString == null)
            {
                throw new ArgumentNullException(nameof(queryString));
            }

            if (queryString.Length != 0 && queryString[0] == '?')
            {
                queryString = queryString.Substring(1);
            }

            return queryString.Split('&')
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x =>
                {
                    int indexOfFirstEqualsSign = x.IndexOf("=");
                    if (indexOfFirstEqualsSign == -1)
                    {
                        return new string[] { x };
                    }
                    return new string[]
                    {
                        x.Substring(0, indexOfFirstEqualsSign),
                        x.Substring(indexOfFirstEqualsSign + 1)
                    };
                })
                .ToLookup(y => y[0], y => y.Length > 1 ? Uri.UnescapeDataString(y[1]) : null);
        }
    }
}
