// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>How many of the valid permutations a <see cref="MatrixAttribute" /> test runs under (design doc D38).</summary>
public enum MatrixSelection
{
    /// <summary>Enough permutations to cover every pair of values of the declared dimensions. Used on pull requests.</summary>
    Pairwise,

    /// <summary>Every valid permutation of the declared dimensions. Used nightly.</summary>
    Full,
}

/// <summary>Builds the list of permutations for a set of declared dimensions (design doc D32, D34, D38).</summary>
public static class PermutationMatrix
{
    /// <summary>
    /// Set this environment variable to <c>full</c> to run the full product of valid permutations instead of the
    /// pairwise selection, for example in a nightly build.
    /// </summary>
    public const string SelectionVariable = "OUTSIDE_IN_MATRIX";

    public static MatrixSelection SelectionFromEnvironment() =>
        string.Equals(Environment.GetEnvironmentVariable(SelectionVariable), "full", StringComparison.OrdinalIgnoreCase)
            ? MatrixSelection.Full
            : MatrixSelection.Pairwise;

    public static IReadOnlyList<Permutation> Select(Dimensions declared, MatrixSelection selection)
    {
        var candidates = ValidPermutations(declared);
        return selection == MatrixSelection.Full ? candidates : Pairwise(candidates, declared);
    }

    /// <summary>
    /// Every valid combination of the declared dimensions' values, with undeclared dimensions at their default.
    /// When Spaces or Users is declared but Stores isn't, the default store becomes Duende.Storage (D34).
    /// </summary>
    private static List<Permutation> ValidPermutations(Dimensions declared)
    {
        var defaultStore = !declared.HasFlag(Dimensions.Stores) && (declared & (Dimensions.Users | Dimensions.Spaces)) != 0
            ? StoreType.DuendeStorageSchemasInMemory
            : Permutation.Default.Store;

        var stores = declared.HasFlag(Dimensions.Stores) ? Enum.GetValues<StoreType>() : [defaultStore];
        var caching = declared.HasFlag(Dimensions.Caching) ? Enum.GetValues<CachingMode>() : [Permutation.Default.Caching];
        var users = declared.HasFlag(Dimensions.Users) ? Enum.GetValues<UserStore>() : [Permutation.Default.Users];
        var spaces = declared.HasFlag(Dimensions.Spaces) ? Enum.GetValues<SpacesMode>() : [Permutation.Default.Spaces];

        return (from store in stores
                from cache in caching
                from user in users
                from space in spaces
                select new Permutation(store, cache, user, space))
            .Where(permutation => permutation.IsValid)
            .ToList();
    }

    /// <summary>
    /// Greedy pairwise selection: repeatedly picks the candidate that covers the most value pairs not covered yet,
    /// until every pair that occurs in a valid permutation is covered. Candidates that can run are picked before those
    /// that can't. Deterministic, so a run is reproducible.
    /// </summary>
    private static List<Permutation> Pairwise(List<Permutation> candidates, Dimensions declared)
    {
        var axes = new List<Func<Permutation, int>>();
        if (declared.HasFlag(Dimensions.Stores))
        {
            axes.Add(p => (int)p.Store);
        }

        if (declared.HasFlag(Dimensions.Caching))
        {
            axes.Add(p => (int)p.Caching);
        }

        if (declared.HasFlag(Dimensions.Users))
        {
            axes.Add(p => (int)p.Users);
        }

        if (declared.HasFlag(Dimensions.Spaces))
        {
            axes.Add(p => (int)p.Spaces);
        }

        if (axes.Count < 2)
        {
            return candidates;
        }

        IEnumerable<(int, int, int, int)> PairsOf(Permutation permutation) =>
            from a in Enumerable.Range(0, axes.Count)
            from b in Enumerable.Range(a + 1, axes.Count - a - 1)
            select (a, axes[a](permutation), b, axes[b](permutation));

        var uncovered = candidates.SelectMany(PairsOf).ToHashSet();
        var selected = new List<Permutation>();

        // Runnable permutations first, so values that are implemented get run. Only the pairs they can't cover are
        // left to permutations that will be skipped.
        foreach (var pool in new[] { candidates.Where(candidate => candidate.NotImplementedReason is null).ToList(), candidates })
        {
            while (pool.Count > 0 && uncovered.Overlaps(pool.SelectMany(PairsOf)))
            {
                var best = pool.MaxBy(candidate => PairsOf(candidate).Count(uncovered.Contains))!;
                selected.Add(best);
                uncovered.ExceptWith(PairsOf(best));
            }
        }

        return selected.OrderBy(candidates.IndexOf).ToList();
    }
}
