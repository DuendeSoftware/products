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
        public static ILookup<string, string> ParseQueryString(string queryString) =>
            SplitQueryString(queryString)
                .ToLookup(pair => pair.Key, pair => pair.Value == null ? null : Uri.UnescapeDataString(pair.Value));

        /// <summary>
        /// Splits a query string into its key/value pairs and keeps the values as they are in the URL
        /// (still percent-encoded). It uses the same split rules as <see cref="ParseQueryString"/>, so a
        /// raw value always decodes to the value that <see cref="ParseQueryString"/> gives for the same key.
        /// </summary>
        /// <param name="queryString">A query string, with or without the leading '?' character.</param>
        /// <returns>A collection with the parsed keys and the raw values.</returns>
        public static ILookup<string, string> ParseRawQueryString(string queryString) =>
            SplitQueryString(queryString).ToLookup(pair => pair.Key, pair => pair.Value);

        private static IEnumerable<KeyValuePair<string, string>> SplitQueryString(string queryString)
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
                        return new KeyValuePair<string, string>(x, null);
                    }
                    return new KeyValuePair<string, string>(
                        x.Substring(0, indexOfFirstEqualsSign),
                        x.Substring(indexOfFirstEqualsSign + 1));
                });
        }
    }
}
