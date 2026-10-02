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
}
