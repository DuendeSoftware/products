// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying.Cursors;

/// <summary>
/// The traversal direction encoded in a cursor token.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal enum CursorDirection
{
    /// <summary>
    /// Traverses forward through the requested order (flag bit0 = 0).
    /// </summary>
    Forward = 0,

    /// <summary>
    /// Traverses backward through the requested order (flag bit0 = 1).
    /// </summary>
    Backward = 1
}
