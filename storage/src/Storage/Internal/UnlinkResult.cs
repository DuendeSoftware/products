// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// The result of an Unlink operation on <see cref="IStore"/>.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public enum UnlinkResult
{
    /// <summary>The link was removed, or did not exist (idempotent).</summary>
    Success,
}
