// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Operations;

/// <summary>
/// Represents the possible outcomes of a delete operation.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public enum DeleteResult
{
    /// <summary>
    /// The entity was deleted successfully.
    /// </summary>
    Success,

    /// <summary>
    /// A concurrency conflict occurred during deletion.
    /// </summary>
    ConcurrencyConflict
}
