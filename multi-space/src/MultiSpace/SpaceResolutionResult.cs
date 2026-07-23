// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.MultiSpace;

/// <summary>
/// The result of a space resolution attempt.
/// </summary>
public sealed class SpaceResolutionResult
{
    /// <summary>Gets the resolved space ID.</summary>
    public required SpaceId SpaceId { get; init; }

    /// <summary>
    /// The matched path (e.g. <c>/t/acme</c>), or <c>null</c> if matched by origin only.
    /// </summary>
    public PathString? MatchedPath { get; init; }
}
