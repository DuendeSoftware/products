// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;

namespace Duende.Storage.Internal;

/// <summary>
/// Controls what happens to a link when one of its endpoint entities is deleted.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public enum LinkDeleteBehavior
{
    /// <summary>
    /// Deleting either the left or the right entity removes the link.
    /// </summary>
    Cascade = 0,

    /// <summary>
    /// Deleting the right entity fails with <see cref="DeleteResult.StillLinked"/> (or
    /// <see cref="OperationOutcome.StillLinked"/> in a batch) while links of this type still exist.
    /// Deleting the left entity still removes the link.
    /// </summary>
    Restrict = 1,
}
