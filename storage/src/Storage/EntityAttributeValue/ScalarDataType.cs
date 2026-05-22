// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Represents a schema attribute data type.
/// </summary>
#pragma warning disable CA1720 // Identifiers should not contain type names
public enum ScalarDataType
{
    /// <summary>A boolean (true/false) value.</summary>
    Boolean,

    /// <summary>A date-only value without time component.</summary>
    Date,

    /// <summary>A date and time value with time zone offset.</summary>
    DateTime,

    /// <summary>A decimal numeric value.</summary>
    Decimal,

    /// <summary>A 32-bit integer value.</summary>
    Integer,

    /// <summary>A text string value.</summary>
    String,
}
