// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Operations;

/// <summary>
/// Represents the possible outcomes of an update operation.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public enum UpdateResult
{
    /// <summary>
    /// The entity was updated successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The entity does not exist.
    /// </summary>
    DoesNotExist,

    /// <summary>
    /// The entity version did not match the expected version.
    /// </summary>
    UnexpectedVersion,

    /// <summary>
    /// A key conflict occurred with another entity.
    /// </summary>
    KeyConflict
}
