// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Query result for paged data
/// </summary>
public class QueryResult<T>
{
    /// <summary>
    /// The token that indicates these results. This is used for more results in subsequent queries.
    /// If null, then there were no more results.
    /// </summary>
    public string ResultsToken { get; init; } = null!;

    /// <summary>
    /// True if there is a previous set of results.
    /// </summary>
    public bool HasPrevResults { get; set; }

    /// <summary>
    /// True if there is another set of results.
    /// </summary>
    public bool HasNextResults { get; set; }

    /// <summary>
    /// The total count (if available).
    /// </summary>
    public int? TotalCount { get; init; }

    /// <summary>
    /// The total pages (if available).
    /// </summary>
    public int? TotalPages { get; init; }

    /// <summary>
    /// The current (if available).
    /// </summary>
    public int? CurrentPage { get; init; }

    /// <summary>
    /// The results.
    /// </summary>
    public IReadOnlyCollection<T> Results { get; init; } = null!;
}
