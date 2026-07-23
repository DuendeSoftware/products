// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.MultiSpace.Internal;

internal static class SpaceCacheKeys
{
    public static string ForPattern(string? origin, string? path)
        => $"Duende.MultiSpace:p:{origin?.ToUpperInvariant() ?? ""}:{path?.ToUpperInvariant() ?? ""}";

    public static string ForSpaceId(SpaceId spaceId) => $"Duende.MultiSpace:s:{spaceId.Value}";

    public static string ForOriginClaim(string origin) => $"Duende.MultiSpace:oc:{origin.ToUpperInvariant()}";
}
