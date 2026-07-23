// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.MultiSpace;

/// <summary>
/// Read-only store for resolving and looking up spaces.
/// </summary>
public interface ISpaceStore
{
    /// <summary>
    /// Tries to resolve a space from matching criteria (origin/path).
    /// </summary>
    Task<SpaceResolutionResult?> TryResolveSpace(SpaceMatchPattern matchingCriteria, Ct ct);

    /// <summary>
    /// Checks whether the given origin is claimed by any registered space,
    /// regardless of whether that space also requires a path match.
    /// Used to enforce hostname precedence: if an origin is claimed, path-only
    /// resolution must not route the request to a different space.
    /// </summary>
    Task<bool> IsOriginClaimed(string origin, Ct ct);

    /// <summary>
    /// Tries to look up a space by its ID.
    /// </summary>
    Task<Space?> TryGetSpace(SpaceId spaceId, Ct ct);
}
