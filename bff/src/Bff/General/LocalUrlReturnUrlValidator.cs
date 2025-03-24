// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;

internal class LocalUrlReturnUrlValidator : IReturnUrlValidator
{
    /// <inheritdoc/>
    public Task<bool> IsValidAsync(string returnUrl)
    {
        return Task.FromResult(IsLocalUrl(returnUrl));
    }

    internal static bool IsLocalUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        switch (url[0])
        {
            // Allows "/" or "/foo" but not "//" or "/\".
            // url is exactly "/"
            case '/' when url.Length == 1:
                return true;
            // url doesn't start with "//" or "/\"
            case '/' when url[1] != '/' && url[1] != '\\':
                return !HasControlCharacter(url.AsSpan(1));
            case '/':
                return false;
        }

        return false;


        static bool HasControlCharacter(ReadOnlySpan<char> readOnlySpan)
        {
            // URLs may not contain ASCII control characters.
            foreach (var t in readOnlySpan)
            {
                if (char.IsControl(t))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
