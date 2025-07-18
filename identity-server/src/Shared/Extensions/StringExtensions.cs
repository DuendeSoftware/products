// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;

namespace Duende.IdentityServer.Extensions;

internal static class StringExtensions
{
    [DebuggerStepThrough]
    public static string ToSpaceSeparatedString(this IEnumerable<string>? list)
    {
        if (list == null)
        {
            return string.Empty;
        }

        return string.Join(' ', list);
    }

    [DebuggerStepThrough]
    public static IEnumerable<string> FromSpaceSeparatedString(this string input)
    {
        input = input.Trim();
        return input.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    public static List<string>? ParseScopesString(this string scopes)
    {
        if (scopes.IsMissing())
        {
            return null;
        }

        scopes = scopes.Trim();
        var parsedScopes = scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();

        if (parsedScopes.Any())
        {
            parsedScopes.Sort();
            return parsedScopes;
        }

        return null;
    }

    [DebuggerStepThrough]
    public static bool IsMissing([NotNullWhen(false)] this string? value) => string.IsNullOrWhiteSpace(value);

    [DebuggerStepThrough]
    public static bool IsMissingOrTooLong(this string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (value.Length > maxLength)
        {
            return true;
        }

        return false;
    }

    [DebuggerStepThrough]
    public static bool IsPresent([NotNullWhen(true)] this string? value) => !string.IsNullOrWhiteSpace(value);

    [DebuggerStepThrough]
    [return: NotNullIfNotNull("url")]
    public static string? EnsureLeadingSlash(this string? url)
    {
        if (url != null && !url.StartsWith('/'))
        {
            return '/' + url;
        }

        return url;
    }

    [DebuggerStepThrough]
    [return: NotNullIfNotNull("url")]
    public static string? EnsureTrailingSlash(this string? url)
    {
        if (url != null && !url.EndsWith('/'))
        {
            return url + '/';
        }

        return url;
    }

    [DebuggerStepThrough]
    [return: NotNullIfNotNull("url")]
    public static string? RemoveLeadingSlash(this string? url)
    {
        if (url != null && url.StartsWith('/'))
        {
            url = url.Substring(1);
        }

        return url;
    }

    [DebuggerStepThrough]
    [return: NotNullIfNotNull("url")]
    public static string? RemoveTrailingSlash(this string? url)
    {
        if (url != null && url.EndsWith('/'))
        {
            url = url.Substring(0, url.Length - 1);
        }

        return url;
    }

    [DebuggerStepThrough]
    public static string CleanUrlPath(this string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            url = "/";
        }

        if (url != "/" && url.EndsWith('/'))
        {
            url = url.Substring(0, url.Length - 1);
        }

        return url;
    }

    [DebuggerStepThrough]
    public static bool IsLocalUrl([NotNullWhen(true)] this string? url)
    {
        // This implementation is a copy of a https://github.com/dotnet/aspnetcore/blob/3f1acb59718cadf111a0a796681e3d3509bb3381/src/Mvc/Mvc.Core/src/Routing/UrlHelperBase.cs#L315
        // We originally copied that code to avoid a dependency, but we could potentially remove this entirely by switching to the Microsoft.NET.Sdk.Web sdk.
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        // Allows "/" or "/foo" but not "//" or "/\".
        if (url[0] == '/')
        {
            // url is exactly "/"
            if (url.Length == 1)
            {
                return true;
            }

            // url doesn't start with "//" or "/\"
            if (url[1] != '/' && url[1] != '\\')
            {
                return !HasControlCharacter(url.AsSpan(1));
            }

            return false;
        }

        // Allows "~/" or "~/foo" but not "~//" or "~/\".
        if (url[0] == '~' && url.Length > 1 && url[1] == '/')
        {
            // url is exactly "~/"
            if (url.Length == 2)
            {
                return true;
            }

            // url doesn't start with "~//" or "~/\"
            if (url[2] != '/' && url[2] != '\\')
            {
                return !HasControlCharacter(url.AsSpan(2));
            }

            return false;
        }

        return false;

        static bool HasControlCharacter(ReadOnlySpan<char> readOnlySpan)
        {
            // URLs may not contain ASCII control characters.
            for (var i = 0; i < readOnlySpan.Length; i++)
            {
                if (char.IsControl(readOnlySpan[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [DebuggerStepThrough]
    public static bool IsUri(this string input)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return !uri.IsFile ||
               // no need to check if input starts with {Uri.UriSchemeFile}:// because uri.IsFile ensures it is either '/' or `file://`
               input.StartsWith(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase);
    }

    [DebuggerStepThrough]
    public static string AddQueryString(this string url, string query)
    {
        if (!url.Contains('?', StringComparison.InvariantCulture))
        {
            url += '?';
        }
        else if (!url.EndsWith('&'))
        {
            url += '&';
        }

        return url + query;
    }

    [DebuggerStepThrough]
    public static string AddQueryString(this string url, string name, string value) =>
        url.AddQueryString(name + "=" + UrlEncoder.Default.Encode(value));

    [DebuggerStepThrough]
    public static string AddHashFragment(this string url, string query)
    {
        if (!url.Contains('#', StringComparison.InvariantCulture))
        {
            url += "#";
        }

        return url + query;
    }

    [DebuggerStepThrough]
    public static NameValueCollection ReadQueryStringAsNameValueCollection(this string? url)
    {
        var collection = new NameValueCollection();
        var queryString = url?.QueryString();
        if (queryString == null)
        {
            return collection;
        }

        var pairs = queryString.Split('&');
        foreach (var pair in pairs)
        {
            if (string.IsNullOrEmpty(pair))
            {
                continue;
            }

            var kvp = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(kvp[0]);
            var value = kvp.Length > 1 ? kvp[1] : null;
            var unescaped = value.IsPresent() ? Uri.UnescapeDataString(kvp[1]) : null;
            collection.Add(key, unescaped);
        }

        return collection;
    }

    private static string? QueryString(this string url)
    {
        var queryStringStart = url.IndexOf('?', StringComparison.InvariantCulture);
        if (queryStringStart >= 0)
        {
            return url.Substring(queryStringStart + 1);
        }

        var uri = new Uri(url, UriKind.RelativeOrAbsolute);
        if (uri.IsAbsoluteUri)
        {
            return uri.Query.IsPresent() ? uri.Query.Substring(1) : null;
        }
        else
        {
            return url;
        }
    }

    public static string? GetOrigin(this string? url)
    {
        if (url != null)
        {
            Uri uri;
            try
            {
                uri = new Uri(url);
            }
            catch (Exception)
            {
                return null;
            }

            return $"{uri.Scheme}://{uri.Authority}";
        }

        return null;
    }

    public static string Obfuscate(this string value)
    {
        var last4Chars = "****";
        if (value.IsPresent() && value.Length > 4)
        {
            last4Chars = value.Substring(value.Length - 4);
        }

        return "****" + last4Chars;
    }
}
