// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;

namespace Duende.IdentityServer.Stores.Storage.Clients;

/// <summary>
/// Validation rules for client URI fields used by the Admin API.
/// Each Validate* method returns null when the value is valid, or a short reason
/// string (no trailing period, no value echo) that the caller uses to build the
/// final error message.
/// </summary>
internal static class ClientUriRules
{
    private static bool TryParseAbsolute(string value, out Uri? uri, out string? reason)
    {
        if (value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            reason = "must not contain whitespace or control characters";
            uri = null;
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
        {
            reason = "is not an absolute URI";
            return false;
        }

        // On Linux, .NET parses a value like "/cb" as "file:///cb".
        if (uri.IsFile || string.Equals(uri.Scheme, "file", StringComparison.OrdinalIgnoreCase))
        {
            reason = "must not be a file URI";
            uri = null;
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Fragment) || value.Contains('#', StringComparison.Ordinal))
        {
            reason = "must not contain a fragment";
            uri = null;
            return false;
        }

        reason = null;
        return true;
    }

    private static bool IsHttp(Uri uri) => string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);

    private static bool IsHttps(Uri uri) => string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the URI's host is an explicit loopback host: "localhost" (any case),
    /// an IPv4 dotted-quad with a first octet of 127 (as written in the original string, not the
    /// value .NET normalizes shorthand forms like "127.1" or "2130706433" to), or the IPv6 loopback
    /// address [::1]. IPv4-mapped IPv6 addresses such as [::ffff:127.0.0.1] are not loopback.
    /// </summary>
    internal static bool IsLoopbackHost(Uri uri)
    {
        switch (uri.HostNameType)
        {
            case UriHostNameType.Dns:
                return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);

            case UriHostNameType.IPv4:
                var rawHost = ExtractRawHost(uri);
                return rawHost is not null
                    && string.Equals(rawHost, uri.Host, StringComparison.OrdinalIgnoreCase)
                    && uri.Host.StartsWith("127.", StringComparison.Ordinal);

            case UriHostNameType.IPv6:
                var hostText = uri.Host.Trim('[', ']');
                return IPAddress.TryParse(hostText, out var address) && address.Equals(IPAddress.IPv6Loopback);

            default:
                return false;
        }
    }

    /// <summary>
    /// Extracts the host exactly as written in the original string, before Uri normalizes
    /// shorthand or alternate forms (e.g. "2130706433", "0x7f000001", "127.1") to a canonical
    /// dotted-quad address.
    /// </summary>
    private static string? ExtractRawHost(Uri uri)
    {
        var original = uri.OriginalString;
        var schemeSeparator = original.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparator < 0)
        {
            return null;
        }

        var afterScheme = original.AsSpan(schemeSeparator + 3);
        var authorityEnd = afterScheme.IndexOfAny('/', '?', '#');
        var authority = authorityEnd < 0 ? afterScheme : afterScheme[..authorityEnd];

        var userInfoSeparator = authority.LastIndexOf('@');
        var hostAndPort = userInfoSeparator < 0 ? authority : authority[(userInfoSeparator + 1)..];

        if (hostAndPort.Length > 0 && hostAndPort[0] == '[')
        {
            // IPv6 literal; not handled here (callers use uri.Host for IPv6).
            return null;
        }

        var portSeparator = hostAndPort.IndexOf(':');
        var host = portSeparator < 0 ? hostAndPort : hostAndPort[..portSeparator];
        return host.ToString();
    }

    internal static bool IsDenied(string value, Uri? parsed, IEnumerable<string> deniedPrefixes)
    {
        foreach (var prefix in deniedPrefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (parsed is not null && string.Equals(parsed.Scheme + ":", prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static string? ValidateRedirectUri(string value, IEnumerable<string> deniedPrefixes)
    {
        if (IsDenied(value, null, deniedPrefixes))
        {
            return "uses a scheme that is not allowed (ValidationOptions.InvalidRedirectUriPrefixes)";
        }

        if (!TryParseAbsolute(value, out var uri, out var reason))
        {
            return reason;
        }

        if (IsDenied(value, uri, deniedPrefixes))
        {
            return "uses a scheme that is not allowed (ValidationOptions.InvalidRedirectUriPrefixes)";
        }

        if (IsHttp(uri!) && !IsLoopbackHost(uri!))
        {
            return "must use https; http is only allowed for loopback hosts (localhost, 127.0.0.0/8, [::1])";
        }

        return null;
    }

    internal static string? ValidateWebUrl(string value)
    {
        if (!TryParseAbsolute(value, out var uri, out var reason))
        {
            return reason;
        }

        if (IsHttps(uri!))
        {
            return null;
        }

        if (IsHttp(uri!) && IsLoopbackHost(uri!))
        {
            return null;
        }

        return "must use https (http is only allowed for loopback hosts)";
    }

    internal static string? ValidateBackChannelUri(string value)
    {
        if (!TryParseAbsolute(value, out var uri, out var reason))
        {
            return reason;
        }

        if (IsHttp(uri!) || IsHttps(uri!))
        {
            return null;
        }

        return "must use http or https";
    }

    internal static string? ValidateCorsOrigin(string value)
    {
        var reason = ValidateWebUrl(value);
        if (reason is not null)
        {
            return reason;
        }

        Uri.TryCreate(value, UriKind.Absolute, out var uri);

        var canonical = BuildCanonicalOrigin(uri!);
        if (!string.Equals(value, canonical, StringComparison.OrdinalIgnoreCase))
        {
            return $"must be a canonical origin ('{canonical}')";
        }

        return null;
    }

    /// <summary>
    /// Builds the canonical form of a URI's origin: lowercase scheme, punycode host
    /// (IPv6 in brackets), the port only when it is not the scheme's default port, and
    /// no path, query, userinfo or trailing slash.
    /// </summary>
    private static string BuildCanonicalOrigin(Uri uri)
    {
        var host = uri.HostNameType == UriHostNameType.IPv6
            ? $"[{uri.IdnHost}]"
            : uri.IdnHost;

        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";

        return $"{uri.Scheme}://{host}{port}";
    }

    internal static bool HaveSameOrigin(Uri a, Uri b) =>
        string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.IdnHost, b.IdnHost, StringComparison.OrdinalIgnoreCase)
        && a.Port == b.Port;
}
