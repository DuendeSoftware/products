// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.MultiSpace;

/// <summary>
/// Represents a URL pattern used to match incoming requests to a space,
/// and also serves as the criteria for resolving a space from an incoming request.
/// At least one of <see cref="Origin"/> or <see cref="Path"/> must be set — validated in SpaceRepository.
/// </summary>
public sealed record SpaceMatchPattern
{
    /// <summary>Gets the origin (scheme + host + optional port) to match, e.g. <c>https://example.com</c>.</summary>
    public string? Origin { get; init; }

    /// <summary>Gets the path prefix to match, e.g. <c>/tenant-a</c>.</summary>
    public string? Path { get; init; }
}
