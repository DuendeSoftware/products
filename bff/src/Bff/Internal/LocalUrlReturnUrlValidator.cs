// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Endpoints;

namespace Duende.Bff.Internal;

internal class LocalUrlReturnUrlValidator : IReturnUrlValidator
{
    /// <inheritdoc/>
    public bool IsValidAsync(string returnUrl) => IsLocalUrl(returnUrl);

    internal static bool IsLocalUrl(string url)
    {
        // Loosely based on https://github.com/dotnet/aspnetcore/blob/3f1acb59718cadf111a0a796681e3d3509bb3381/src/Mvc/Mvc.Core/src/Routing/UrlHelperBase.cs
        // Do not replace the current logic with Uri.TryParse for local URL validation.
        // The current implementation is specifically designed to prevent open redirect vulnerabilities and other attacks by strictly validating the format of the URL.

        // The current IsLocalUrl method:
        // •	Only allows URLs that start with a single / (not // or /\).
        // •	Disallows control characters.
        // •	Does not allow absolute URLs, protocol - relative URLs, or malformed paths.

        // This is a strict check that prevents open redirect vulnerabilities by ensuring only local, relative paths are accepted.

        // Issues with Uri.TryParse
        // •	Uri.TryParse will successfully parse both absolute and relative URLs, including dangerous ones like //evil.com (protocol-relative) or http://evil.com.
        // •	If you only check Uri.IsAbsoluteUri == false, you might still allow protocol - relative URLs(//evil.com), which are not local and can be exploited for open redirects.
        // •	Uri.TryParse does not check for control characters or other subtle issues.

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
            _ => false
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
