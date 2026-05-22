// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Operations;

/// <summary>
/// Represents the outcome of an individual operation within a batch.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public enum OperationOutcome
{
    /// <summary>
    /// The operation succeeded.
    /// </summary>
    Success,

    /// <summary>
    /// Create failed: an entity with the same ID already exists.
    /// </summary>
    AlreadyExists,

    /// <summary>
    /// Create or Update failed: a key is already used by another entity.
    /// </summary>
    KeyConflict,

    /// <summary>
    /// Update or Delete failed: the entity was not found.
    /// </summary>
    DoesNotExist,

    /// <summary>
    /// Update failed: the entity version did not match the expected version.
    /// </summary>
    UnexpectedVersion,

    /// <summary>
    /// Link failed: an identical link (same LinkType, LeftId, RightId) already exists.
    /// </summary>
    AlreadyLinked
}
