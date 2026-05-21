// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Specifies the type of a field, corresponding to the typed columns in the search_values table.
/// This ensures QueryFields reads from the correct column instead of checking the first non-null value.
/// </summary>
public enum FieldType
{
    /// <summary>
    /// String field, stored in the string_value column.
    /// </summary>
#pragma warning disable CA1720 // Identifier 'String' contains type name
    String,
#pragma warning restore CA1720

    /// <summary>
    /// Number field, stored in the number_value column.
    /// </summary>
    Number,

    /// <summary>
    /// DateTime field, stored in the datetime_value column.
    /// </summary>
    DateTime,

    /// <summary>
    /// Boolean field, stored in the boolean_value column.
    /// </summary>
    Boolean,

    /// <summary>
    /// Guid field, stored in the guid_value column.
    /// </summary>
#pragma warning disable CA1720 // Identifier 'Guid' contains type name
    Guid
#pragma warning restore CA1720
}
