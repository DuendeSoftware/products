// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage;

/// <summary>
/// Represents a version number for the database schema.
/// </summary>
public sealed record DatabaseSchemaVersion
{
    /// <summary>
    /// Gets the numeric version value.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DatabaseSchemaVersion"/> with the specified version number.
    /// </summary>
    /// <param name="value">The version number. Must be non-negative.</param>
    public DatabaseSchemaVersion(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>
    /// A schema version representing no schema (version zero).
    /// </summary>
    public static readonly DatabaseSchemaVersion Zero = new(0);
}
