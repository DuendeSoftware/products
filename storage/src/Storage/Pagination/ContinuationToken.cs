// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Querying;

namespace Duende.Storage.Pagination;

/// <summary>
/// An opaque continuation token used for cursor-based pagination.
/// Returned in <see cref="QueryResult{T}"/> and consumed by <see cref="DataRange"/>.
/// </summary>
[StringValue]
public partial record ContinuationToken
{
    /// <summary>The token value representing the beginning of the dataset.</summary>
    public const string Beginning = "Beginning";
}
