// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Constants for system field paths that map to entity-level columns
/// rather than EAV search values.
/// </summary>
public static class SystemFields
{
    /// <summary>
    /// The internal (upper-case) field path for the entity creation timestamp.
    /// </summary>
    public const string Created = "CREATEDAT";

    /// <summary>
    /// The internal (upper-case) field path for the entity last-updated timestamp.
    /// </summary>
    public const string LastUpdated = "LASTUPDATEDAT";

    /// <summary>
    /// The public attribute name for the entity creation timestamp (lowercase, used in SCIM filters and sort).
    /// </summary>
    public const string CreatedAttributeName = "created_at";

    /// <summary>
    /// The public attribute name for the entity last-updated timestamp (lowercase, used in SCIM filters and sort).
    /// </summary>
    public const string LastUpdatedAttributeName = "last_updated_at";

    /// <summary>
    /// Ready-made <see cref="DateTimeField"/> for querying/sorting by creation timestamp.
    /// </summary>
    public static readonly DateTimeField CreatedAtField = new(Created);

    /// <summary>
    /// Ready-made <see cref="DateTimeField"/> for querying/sorting by last-updated timestamp.
    /// </summary>
    public static readonly DateTimeField LastUpdatedAtField = new(LastUpdated);

    /// <summary>
    /// Returns true if the given field path is a system field (case-insensitive).
    /// Matches both internal paths (CREATEDAT, LASTUPDATEDAT) and public attribute names (created_at, last_updated_at).
    /// </summary>
    public static bool IsSystemField(string fieldPath) =>
        string.Equals(fieldPath, Created, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fieldPath, LastUpdated, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fieldPath, CreatedAttributeName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fieldPath, LastUpdatedAttributeName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if the given attribute name is a reserved system attribute.
    /// Checks both public aliases (created_at, last_updated_at) and internal paths (CREATEDAT, LASTUPDATEDAT)
    /// using case-insensitive comparison to prevent user-defined attributes from colliding
    /// with system fields after upper-case normalization.
    /// </summary>
    public static bool IsReservedAttributeName(string attributeName) =>
        string.Equals(attributeName, CreatedAttributeName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(attributeName, LastUpdatedAttributeName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(attributeName, Created, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(attributeName, LastUpdated, StringComparison.OrdinalIgnoreCase);
}
