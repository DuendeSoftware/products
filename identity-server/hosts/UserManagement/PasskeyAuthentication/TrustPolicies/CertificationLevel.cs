// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace PasskeyAuthentication.TrustPolicies;

internal static class CertificationLevel
{
    public static bool TryGetRank(string certificationLevel, out int rank)
    {
        rank = Normalize(certificationLevel) switch
        {
            "FIDOCERTIFIED" or "FIDOCERTIFIEDL1" or "L1" => 1,
            "FIDOCERTIFIEDL1PLUS" or "L1PLUS" or "L1+" => 2,
            "FIDOCERTIFIEDL2" or "L2" => 3,
            "FIDOCERTIFIEDL2PLUS" or "L2PLUS" or "L2+" => 4,
            "FIDOCERTIFIEDL3" or "L3" => 5,
            "FIDOCERTIFIEDL3PLUS" or "L3PLUS" or "L3+" => 6,
            "FIDOCERTIFIEDL4" or "L4" => 7,
            "FIDOCERTIFIEDL4PLUS" or "L4PLUS" or "L4+" => 8,
            _ => 0,
        };

        return rank > 0;
    }

    private static string Normalize(string certificationLevel) =>
        certificationLevel
            .Trim()
            .ToUpperInvariant()
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
}
