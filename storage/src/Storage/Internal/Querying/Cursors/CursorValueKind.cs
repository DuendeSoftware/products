// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying.Cursors;

/// <summary>
/// The kind of sort value a cursor token's key position carries, encoded in flag bits 2-4.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal enum CursorValueKind
{
    /// <summary>
    /// No sort value: the query is unsorted and only the entity id is carried.
    /// </summary>
    None = 0,

    /// <summary>
    /// The sort field's value was null on the referenced row.
    /// </summary>
    Null = 1,

    /// <summary>
    /// A UTF-8 string value.
    /// </summary>
    String = 2,

    /// <summary>
    /// A decimal number value.
    /// </summary>
    Number = 3,

    /// <summary>
    /// A UTC instant, stored as ticks.
    /// </summary>
    DateTime = 4,

    /// <summary>
    /// A boolean value.
    /// </summary>
    Boolean = 5,

    /// <summary>
    /// A GUID value.
    /// </summary>
    Guid = 6
}
