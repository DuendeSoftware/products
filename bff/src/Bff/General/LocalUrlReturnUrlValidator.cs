// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;

class LocalUrlReturnUrlValidator : IReturnUrlValidator
{
    /// <inheritdoc/>
    public Task<bool> IsValidAsync(string returnUrl) => Task.FromResult(IsLocalUrl(returnUrl));

    internal static bool IsLocalUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        return url[0] switch
        {
            // Allows "/" or "/foo" but not "//" or "/\".
            // url is exactly "/"
            '/' when url.Length == 1 => true,
            // url doesn't start with "//" or "/\"
            '/' when url[1] != '/' && url[1] != '\\' => !HasControlCharacter(url.AsSpan(1)),
            '/' => false,
            _ => false,
        };

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
