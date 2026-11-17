// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.Spaces;

/// <summary>
/// Represents a URL pattern used to match incoming requests to a space,
/// and also serves as the criteria for resolving a space from an incoming request.
/// At least one of <see cref="Origin"/> or <see cref="Path"/> must be set.
/// </summary>
/// <remarks>
/// <see cref="ISpaceAdmin"/> only registers patterns written in lowercase ASCII:
/// <see cref="Origin"/> is <c>http://</c> or <c>https://</c>, a DNS host name of <c>a-z</c>, <c>0-9</c>, <c>-</c> and <c>.</c>
/// (internationalized host names in their punycode <c>xn--</c> form) and an optional port that isn't the scheme's default port,
/// with no path or trailing slash;
/// <see cref="Path"/> is <c>/</c> followed by a single segment of <c>a-z</c>, <c>0-9</c> and <c>-</c>.
/// Any other value, including one with an uppercase letter, is rejected with a validation error.
/// </remarks>
public sealed record SpaceMatchPattern
{
    /// <summary>Gets the origin (scheme + host + optional port) to match, e.g. <c>https://example.com</c>.</summary>
    public string? Origin { get; init; }

    /// <summary>Gets the path segment to match, e.g. <c>/tenant-a</c>.</summary>
    public string? Path { get; init; }
}
