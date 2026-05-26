// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Duende.UserManagement.Scim.Internal;

/// <summary>
/// Represents a SCIM ETag value (e.g., W/"42"). Wraps the string representation
/// and provides factory methods for creating from entity versions and parsing from headers.
/// </summary>
[ValueOf<int>]
internal partial record ScimETag
{
    /// <summary>
    /// Sentinel value representing the wildcard ETag (<c>*</c>).
    /// <c>If-Match: *</c> means "match any existing resource" (skip version check).
    /// <c>If-None-Match: *</c> means "match if any version exists" (always 304 if resource exists).
    /// </summary>
    internal static readonly ScimETag Any = new(-1);

    /// <summary>
    /// Returns <c>true</c> when this ETag represents the wildcard (<c>*</c>).
    /// </summary>
    internal bool IsAny => Value == -1;

    /// <summary>
    /// Returns <c>true</c> when this ETag matches the given entity version.
    /// A wildcard ETag matches any version.
    /// </summary>
    internal bool Matches(int version) => IsAny || Value == version;

    /// <summary>
    /// Tries to create a ScimETag from an ETag header value and extract the entity version.
    /// Accepts <c>*</c> (wildcard), <c>W/"42"</c> (weak), and <c>"42"</c> (strong) formats.
    /// </summary>
    public static bool TryCreate([NotNullWhen(true)] string? s, [NotNullWhen(true)] out ScimETag? result)
    {
        result = null;
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }

        // Handle wildcard
        if (s == "*")
        {
            result = Any;
            return true;
        }

        // Handle W/"42" format
        if (s.StartsWith("W/\"", StringComparison.Ordinal) && s.EndsWith('"'))
        {
            var versionStr = s[3..^1];
            if (int.TryParse(versionStr, CultureInfo.InvariantCulture, out var version))
            {
                result = version; // implicit conversion
                return true;
            }
            return false;
        }
        // Also accept plain "42" (strong ETag)
        if (s.StartsWith('"') && s.EndsWith('"'))
        {
            var versionStr = s[1..^1];
            if (int.TryParse(versionStr, CultureInfo.InvariantCulture, out var version))
            {
                result = version; // implicit conversion
                return true;
            }
            return false;
        }
        return false;
    }

    /// <summary>
    /// Formats the ETag as a weak ETag header value (e.g., W/"42").
    /// </summary>
    internal string ToHeaderValue() => $"W/\"{Value}\"";
}
