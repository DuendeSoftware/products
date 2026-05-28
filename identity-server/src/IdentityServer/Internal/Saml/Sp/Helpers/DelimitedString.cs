// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    /// <summary>
    /// Helpers for delimited string, with support for escaping the delimiter
    /// character.
    /// </summary>
    internal static class DelimitedString
    {
        const string DelimiterString = ",";
        const char DelimiterChar = ',';

        // Use a single / as escape char, avoid \ as that would require
        // all escape chars to be escaped in the source code...
        const char EscapeChar = '/';
        const string EscapeString = "/";

        /// <summary>
        /// Join strings with a delimiter and escape any occurence of the
        /// delimiter and the escape character in the string.
        /// </summary>
        /// <param name="strings">Strings to join</param>
        /// <returns>Joined string</returns>
        public static string Join(params string[] strings)
        {
            return string.Join(
                DelimiterString,
                strings.Select(
                    s => s
                    .Replace(EscapeString, EscapeString + EscapeString)
                    .Replace(DelimiterString, EscapeString + DelimiterString)));
        }

        /// <summary>
        /// Split strings delimited strings, respecting if the delimiter
        /// characters is escaped.
        /// </summary>
        /// <param name="source">Joined string from <see cref="Join(string[])"/></param>
        /// <returns>Unescaped, split strings</returns>
        public static string[] Split(string source)
        {
            var result = new List<string>();

            int segmentStart = 0;
            for (int i = 0; i < source.Length; i++)
            {
                bool readEscapeChar = false;
                if (source[i] == EscapeChar)
                {
                    readEscapeChar = true;
                    i++;
                }

                if (!readEscapeChar && source[i] == DelimiterChar)
                {
                    result.Add(UnEscapeString(
                        source.Substring(segmentStart, i - segmentStart)));
                    segmentStart = i + 1;
                }

                if (i == source.Length - 1)
                {
                    result.Add(UnEscapeString(source.Substring(segmentStart)));
                }
            }

            return result.ToArray();
        }

        static string UnEscapeString(string src)
        {
            return src.Replace(EscapeString + DelimiterString, DelimiterString)
                .Replace(EscapeString + EscapeString, EscapeString);
        }
    }
}
