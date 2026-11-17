// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.OutsideIn.Tests;

/// <summary>Unit tests for the greedy pairwise selector (design doc D32, D38). No snapshots: plain assertions on counts and coverage.</summary>
public sealed class PermutationMatrixTests
{
    // All four dimensions: D32 says 30 of the 48 combinations are valid (Spaces/Users only work on Duende.Storage).
    private static readonly Dimensions AllDimensions = Dimensions.Stores | Dimensions.Caching | Dimensions.Users | Dimensions.Spaces;

    [Fact]
    public void Full_selects_exactly_the_valid_permutations()
    {
        var full = PermutationMatrix.Select(AllDimensions, MatrixSelection.Full);

        full.Count.ShouldBe(30);
        full.ShouldAllBe(permutation => permutation.IsValid);
    }

    [Theory]
    [InlineData(Dimensions.Stores | Dimensions.Caching | Dimensions.Users | Dimensions.Spaces, 30)]
    [InlineData(Dimensions.Stores | Dimensions.Caching | Dimensions.Spaces, 18)]
    [InlineData(Dimensions.Stores | Dimensions.Caching, 12)]
    [InlineData(Dimensions.Stores, 4)]
    [InlineData(Dimensions.None, 1)]
    public void Full_count_matches_the_declared_dimensions(Dimensions declared, int expectedCount)
    {
        var full = PermutationMatrix.Select(declared, MatrixSelection.Full);

        full.Count.ShouldBe(expectedCount);
        full.ShouldAllBe(permutation => permutation.IsValid);
    }

    [Fact]
    public void Pairwise_never_selects_an_invalid_permutation()
    {
        var pairwise = PermutationMatrix.Select(AllDimensions, MatrixSelection.Pairwise);

        pairwise.ShouldAllBe(permutation => permutation.IsValid);
    }

    [Fact]
    public void Pairwise_covers_every_value_pair_that_occurs_in_a_valid_permutation()
    {
        var full = PermutationMatrix.Select(AllDimensions, MatrixSelection.Full);
        var pairwise = PermutationMatrix.Select(AllDimensions, MatrixSelection.Pairwise);

        var requiredPairs = PairsOf(full);
        var coveredPairs = PairsOf(pairwise);

        requiredPairs.ExceptWith(coveredPairs);
        requiredPairs.ShouldBeEmpty();
    }

    [Fact]
    public void Pairwise_is_deterministic()
    {
        var first = PermutationMatrix.Select(AllDimensions, MatrixSelection.Pairwise);
        var second = PermutationMatrix.Select(AllDimensions, MatrixSelection.Pairwise);

        first.Count.ShouldBe(second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            first[i].ToString().ShouldBe(second[i].ToString());
        }
    }

    [Fact]
    public void Undeclared_dimensions_stay_at_their_default()
    {
        var permutations = PermutationMatrix.Select(Dimensions.None, MatrixSelection.Full);

        permutations.Count.ShouldBe(1);
        var only = permutations[0];
        only.Store.ShouldBe(Permutation.Default.Store);
        only.Caching.ShouldBe(Permutation.Default.Caching);
        only.Users.ShouldBe(Permutation.Default.Users);
        only.Spaces.ShouldBe(Permutation.Default.Spaces);
    }

    [Fact]
    public void Declaring_users_without_stores_defaults_the_store_to_duende_storage_in_memory()
    {
        var permutations = PermutationMatrix.Select(Dimensions.Users, MatrixSelection.Full);

        permutations.ShouldAllBe(permutation => permutation.Store == StoreType.DuendeStorageSchemasInMemory);
    }

    [Fact]
    public void Declaring_spaces_without_stores_defaults_the_store_to_duende_storage_in_memory()
    {
        var permutations = PermutationMatrix.Select(Dimensions.Spaces, MatrixSelection.Full);

        permutations.ShouldAllBe(permutation => permutation.Store == StoreType.DuendeStorageSchemasInMemory);
    }

    // Every (axis, value, axis, value) pair that occurs across the declared dimensions, matching how PermutationMatrix
    // itself defines "pair" internally, so pairwise coverage can be checked from the outside without reaching into it.
    private static HashSet<(int DimensionA, object ValueA, int DimensionB, object ValueB)> PairsOf(IReadOnlyList<Permutation> permutations)
    {
        var axes = new Func<Permutation, object>[]
        {
            p => p.Store,
            p => p.Caching,
            p => p.Users,
            p => p.Spaces,
        };

        var pairs = new HashSet<(int, object, int, object)>();
        foreach (var permutation in permutations)
        {
            for (var a = 0; a < axes.Length; a++)
            {
                for (var b = a + 1; b < axes.Length; b++)
                {
                    pairs.Add((a, axes[a](permutation), b, axes[b](permutation)));
                }
            }
        }

        return pairs;
    }
}
