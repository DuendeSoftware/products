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
    extension(IEnumerable<string>? list)
    {
        [DebuggerStepThrough]
        public string ToSpaceSeparatedString()
        {
            if (list == null)
            {
                return string.Empty;
            }

            return string.Join(' ', list);
        }
    }

    extension(string url)
    {
        [DebuggerStepThrough]
        public IEnumerable<string> FromSpaceSeparatedString()
        {
            url = url.Trim();
            return url.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        public List<string>? ParseScopesString()
        {
            if (url.IsMissing())
            {
                return null;
            }

            url = url.Trim();
            var parsedScopes = url.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();

            if (parsedScopes.Count > 0)
            {
                parsedScopes.Sort();
                return parsedScopes;
            }

            return null;
        }

        [DebuggerStepThrough]
        public bool IsUri()
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return !uri.IsFile ||
                   // no need to check if input starts with {Uri.UriSchemeFile}:// because uri.IsFile ensures it is either '/' or `file://`
                   url.StartsWith(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase);
        }

        [DebuggerStepThrough]
        public string AddQueryString(string query)
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
        public string AddQueryString(string name, string value) =>
            url.AddQueryString(name + "=" + UrlEncoder.Default.Encode(value));

        [DebuggerStepThrough]
        public string AddHashFragment(string query)
        {
            if (!url.Contains('#', StringComparison.InvariantCulture))
            {
                url += "#";
            }

            return url + query;
        }

        private string? QueryString()
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

        public string Obfuscate()
        {
            var last4Chars = "****";
            if (url.IsPresent() && url.Length > 4)
            {
                last4Chars = url.Substring(url.Length - 4);
            }

            return "****" + last4Chars;
        }
    }

    extension([NotNullWhen(false)] string? value)
    {
        [DebuggerStepThrough]
        public bool IsMissing() => string.IsNullOrWhiteSpace(value);
    }

    extension(string? url)
    {
        [DebuggerStepThrough]
        public bool IsMissingOrTooLong(int maxLength)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return true;
            }

            if (url.Length > maxLength)
            {
                return true;
            }

            return false;
        }

        [DebuggerStepThrough]
        [return: NotNullIfNotNull("url")]
        public string? EnsureLeadingSlash()
        {
            if (url != null && !url.StartsWith('/'))
            {
                return '/' + url;
            }

            return url;
        }

        [DebuggerStepThrough]
        [return: NotNullIfNotNull("url")]
        public string? EnsureTrailingSlash()
        {
            if (url != null && !url.EndsWith('/'))
            {
                return url + '/';
            }

            return url;
        }

        [DebuggerStepThrough]
        [return: NotNullIfNotNull("url")]
        public string? RemoveLeadingSlash()
        {
            if (url != null && url.StartsWith('/'))
            {
                url = url.Substring(1);
            }

            return url;
        }

        [DebuggerStepThrough]
        [return: NotNullIfNotNull("url")]
        public string? RemoveTrailingSlash()
        {
            if (url != null && url.EndsWith('/'))
            {
                url = url.Substring(0, url.Length - 1);
            }

            return url;
        }

        [DebuggerStepThrough]
        public string CleanUrlPath()
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
        public NameValueCollection ReadQueryStringAsNameValueCollection()
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
                var key = kvp[0];
                var value = kvp.Length > 1 ? kvp[1] : null;
                if (value.IsPresent())
                {
                    collection.Add(Decode(key), Decode(value));
                }
            }

            return collection;
        }

        public string? GetOrigin()
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
    }

    extension([NotNullWhen(true)] string? url)
    {
        [DebuggerStepThrough]
        public bool IsPresent() => !string.IsNullOrWhiteSpace(url);

        [DebuggerStepThrough]
        public bool IsLocalUrl()
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
    }

    private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
}
