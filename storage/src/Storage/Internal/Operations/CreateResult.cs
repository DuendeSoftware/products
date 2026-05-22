// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Operations;

/// <summary>
/// Represents the possible outcomes of a create operation.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public enum CreateResult
{
    /// <summary>
    /// The entity was created successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The entity already exists with the same identifier.
    /// </summary>
    AlreadyExists,

    /// <summary>
    /// A key conflict occurred with another entity.
    /// </summary>
    KeyConflict,

    /// <summary>
    /// A concurrency conflict occurred during creation.
    /// </summary>
    ConcurrencyConflict
}
