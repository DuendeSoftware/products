// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;

namespace Duende.Spaces.Internal;

internal static class SpaceCacheKeys
{
    public static string ForPattern(string? origin, string? path)
        => $"Duende.Spaces:p:{origin?.ToUpperInvariant() ?? ""}:{path?.ToUpperInvariant() ?? ""}";

    public static string ForSpaceId(SpaceId spaceId) => $"Duende.Spaces:s:{spaceId.Value}";

    public static string ForOriginClaim(string origin) => $"Duende.Spaces:oc:{origin.ToUpperInvariant()}";

    public static string ForPoolId(PoolId poolId) => $"Duende.Spaces:pool:{poolId.Value}";

    public static string ForSpaceIdRouting(SpaceId spaceId) => $"Duende.Spaces:rs:{spaceId.Value}";

    /// <summary>
    /// Partitions a caller-supplied <see cref="SpaceAwareHybridCache"/> key by space, so entries
    /// cached from different spaces never collide, without the caller needing to do so itself.
    /// </summary>
    public static string ForCacheEntry(SpaceId spaceId, string key) => $"{spaceId.Value}:{key}";

    /// <summary>
    /// The tag <see cref="SpaceAwareHybridCache"/> adds to every entry it writes, so all entries
    /// for a space can be evicted together with <c>RemoveByTagAsync</c>.
    /// </summary>
    public static string ForSpaceTag(SpaceId spaceId) => $"space:{spaceId.Value}";
}
