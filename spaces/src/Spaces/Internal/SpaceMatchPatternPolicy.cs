// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Spaces.Internal;

/// <summary>
/// Pure, storage-independent policy helpers for comparing sets of <see cref="SpaceMatchPattern"/>.
/// Extracted so both the admin-facing pre-check (<see cref="SpaceAdmin"/>) and the authoritative,
/// persistence-time check (<see cref="Storage.SpaceRepository"/>) share the exact same identity
/// semantics. The two call sites read the space at different times, so they must agree on what
/// "unchanged patterns" means to avoid a stale-read/authoritative-check mismatch.
/// </summary>
internal static class SpaceMatchPatternPolicy
{
    // Compares two pattern lists for exact (multiset), order-independent equality using the same
    // canonical Origin/Path identity (case-insensitive, invariant culture) as SpaceMatchPatternDskV1,
    // which is what actually gates pattern reservation in storage. A naive "left is a subset of the
    // distinct values in right" check (e.g. via HashSet.Contains) would incorrectly treat a duplicated
    // pattern such as [A, A] as equivalent to the original [A, B], because both left values are present
    // in the set built from right. Counting occurrences of each canonical key closes that gap.
    internal static bool PatternsMatch(IReadOnlyList<SpaceMatchPattern> left, IReadOnlyList<SpaceMatchPattern> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        var leftCounts = ToPatternCounts(left);
        var rightCounts = ToPatternCounts(right);

        if (leftCounts.Count != rightCounts.Count)
        {
            return false;
        }

        foreach (var (key, count) in leftCounts)
        {
            if (!rightCounts.TryGetValue(key, out var rightCount) || rightCount != count)
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<(string? Origin, string? Path), int> ToPatternCounts(IReadOnlyList<SpaceMatchPattern> patterns)
    {
        var counts = new Dictionary<(string? Origin, string? Path), int>();
        foreach (var pattern in patterns)
        {
            var key = NormalizePatternKey(pattern);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts;
    }

    // Matches the canonicalization applied by SpaceMatchPatternDskV1.Create so that identity
    // comparisons here agree with the actual storage-key identity used for reservation.
#pragma warning disable CA1308 // Normalize strings to uppercase — lowercase is appropriate for case-insensitive URL matching
    internal static (string? Origin, string? Path) NormalizePatternKey(SpaceMatchPattern pattern) =>
        (pattern.Origin?.ToLowerInvariant(), pattern.Path?.ToLowerInvariant());
#pragma warning restore CA1308
}
