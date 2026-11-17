// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// Supplies a theory with one <see cref="Permutation" /> per configuration it runs under (design doc D34). The test
/// declares the dimensions it's sensitive to; the matrix is pairwise by default and the full product when the
/// <see cref="PermutationMatrix.SelectionVariable" /> environment variable is <c>full</c> (D38). Permutations whose
/// values the harness doesn't implement yet are reported as skipped, with the missing values as the reason.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MatrixAttribute(Dimensions dimensions) : DataAttribute
{
    public Dimensions Dimensions { get; } = dimensions;

    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        var rows = PermutationMatrix.Select(Dimensions, PermutationMatrix.SelectionFromEnvironment())
            .Select(permutation => (ITheoryDataRow)new TheoryDataRow<Permutation>(permutation)
            {
                Skip = permutation.NotImplementedReason,
            })
            .ToArray();

        return new ValueTask<IReadOnlyCollection<ITheoryDataRow>>(rows);
    }

    public override bool SupportsDiscoveryEnumeration() => true;
}
