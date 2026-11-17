// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Globalization;

namespace Duende.Spaces.Internal;

/// <summary>
/// The format a <see cref="SpaceMatchPattern"/> must have to be registered through <see cref="ISpaceAdmin"/>.
/// </summary>
/// <remarks>
/// Only lowercase ASCII is accepted, so every registered pattern is already in its canonical form:
/// no case folding of any culture can turn two registered patterns into the same value. Some non-ASCII
/// characters fold to an ASCII letter: U+017F (long s) is lowercase but upper-cases to 'S', and U+212A
/// (Kelvin sign) lower-cases to 'k'. That is why "lowercase" alone is not enough and the check is
/// restricted to ASCII, which also keeps casing rules that vary by culture or .NET version out of the keys.
/// </remarks>
internal static class SpaceMatchPatternFormat
{
    private const int MaxPortNumber = 65535;
    private const int MaxHostLength = 253;
    private const int MaxLabelLength = 63;
    private const string HttpsScheme = "https://";
    private const string HttpScheme = "http://";

    public const string PathRule =
        "A path must be '/' followed by a single segment of lowercase ASCII letters (a-z), digits (0-9) and hyphens, for example '/contoso'.";

    public const string OriginRule =
        "An origin must be 'http://' or 'https://' followed by a host name of lowercase ASCII letters (a-z), digits (0-9), hyphens and dots, " +
        "and an optional port, with no path or trailing slash, for example 'https://contoso.example.com' or 'https://localhost:5001'. " +
        "Each dot-separated label is 1 to 63 characters and doesn't start or end with a hyphen, and the host is at most 253 characters. " +
        "Leave out the scheme's default port (443 for https, 80 for http). " +
        "Internationalized host names must be given in their punycode (xn--) form.";

    /// <summary>
    /// Returns <c>true</c> when <paramref name="path"/> is <c>/</c> followed by one or more of <c>a-z</c>, <c>0-9</c> or <c>-</c>.
    /// </summary>
    public static bool IsValidPath(string path)
    {
        if (path.Length < 2 || path[0] != '/')
        {
            return false;
        }

        for (var i = 1; i < path.Length; i++)
        {
            if (!IsLowerAsciiLetterDigitOrHyphen(path[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="origin"/> is <c>http://</c> or <c>https://</c>, a DNS host name of
    /// dot-separated labels of <c>a-z</c>, <c>0-9</c> or <c>-</c>, and an optional <c>:port</c> between 1 and 65535
    /// that isn't the scheme's default port.
    /// </summary>
    public static bool IsValidOrigin(string origin)
    {
        string authority;
        int defaultPort;
        if (origin.StartsWith(HttpsScheme, StringComparison.Ordinal))
        {
            authority = origin[HttpsScheme.Length..];
            defaultPort = 443;
        }
        else if (origin.StartsWith(HttpScheme, StringComparison.Ordinal))
        {
            authority = origin[HttpScheme.Length..];
            defaultPort = 80;
        }
        else
        {
            return false;
        }

        var host = authority;
        var colon = authority.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            host = authority[..colon];

            // An explicit default port is a second spelling of the same origin, so only the spelling without it is accepted.
            if (!TryParsePort(authority[(colon + 1)..], out var port) || port == defaultPort)
            {
                return false;
            }
        }

        return IsValidHost(host);
    }

    private static bool IsValidHost(string host)
    {
        if (host.Length is 0 or > MaxHostLength)
        {
            return false;
        }

        foreach (var label in host.Split('.'))
        {
            if (!IsValidLabel(label))
            {
                return false; // also covers empty labels: a leading, trailing or doubled dot
            }
        }

        return true;
    }

    private static bool IsValidLabel(string label)
    {
        if (label.Length is 0 or > MaxLabelLength || label[0] == '-' || label[^1] == '-')
        {
            return false;
        }

        foreach (var c in label)
        {
            if (!IsLowerAsciiLetterDigitOrHyphen(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParsePort(string text, out int port)
    {
        port = 0;
        if (text.Length is 0 or > 5 || text[0] == '0')
        {
            return false; // empty, too long, or a leading zero (one canonical spelling per port)
        }

        foreach (var c in text)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        port = int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
        return port <= MaxPortNumber;
    }

    private static bool IsLowerAsciiLetterDigitOrHyphen(char c) =>
        c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-';
}
