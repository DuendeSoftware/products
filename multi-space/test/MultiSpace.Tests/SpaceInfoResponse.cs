// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.MultiSpace;

/// <summary>Response model for the /api/info test endpoint.</summary>
internal sealed record SpaceInfoResponse
{
    /// <summary>Gets the resolved space ID.</summary>
    public required string SpaceId { get; init; }

    /// <summary>Gets the request path after middleware processing.</summary>
    public required string Path { get; init; }
}
