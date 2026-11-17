// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Specifies the direction for sorting query results.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public enum SortDirection
{
    /// <summary>
    /// Sort in ascending order (A-Z, 0-9, oldest to newest).
    /// </summary>
    Ascending,

    /// <summary>
    /// Sort in descending order (Z-A, 9-0, newest to oldest).
    /// </summary>
    Descending
}
