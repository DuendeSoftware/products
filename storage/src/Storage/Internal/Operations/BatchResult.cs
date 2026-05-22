// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Operations;

/// <summary>
/// Represents the result of a batch operation.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
/// <param name="Success">True if all operations succeeded; false if any failed (and all were rolled back).</param>
/// <param name="Results">The outcome of each operation, in order.</param>
public sealed record BatchResult(bool Success, IReadOnlyList<OperationResult> Results)
{
    /// <summary>
    /// Creates a successful <see cref="BatchResult"/> for a batch of the specified size.
    /// </summary>
    /// <param name="count">The number of operations in the batch.</param>
    /// <returns>A <see cref="BatchResult"/> with all operations having <see cref="OperationOutcome.Success"/>.</returns>
    public static BatchResult Successful(int count)
    {
        var results = new OperationResult[count];
        for (var i = 0; i < count; i++)
        {
            results[i] = new OperationResult(i, OperationOutcome.Success);
        }
        return new BatchResult(true, results);
    }
}
