// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer;

internal static class LogParameterExtensions
{
    extension(object value)
    {
        public object SanitizeLogParameter()
        {
            if (value is not string s || string.IsNullOrEmpty(s))
            {
                return value;
            }

            s = s.ReplaceLineEndings(string.Empty);
            var builder = new System.Text.StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (!IsUnsafeLogChar(c))
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();

            static bool IsUnsafeLogChar(char c)
            {
                return char.IsControl(c)
                       || c == '\u0085'  // NEXT LINE
                       || c == '\u2028'  // LINE SEPARATOR
                       || c == '\u2029'; // PARAGRAPH SEPARATOR
            }
        }
    }
}
