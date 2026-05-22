// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage;

/// <summary>
/// Describes the kind of error found during schema verification.
/// </summary>
public enum SchemaVerificationErrorKind
{
    /// <summary>A required table is missing from the database.</summary>
    MissingTable,
    /// <summary>A required column is missing from a table.</summary>
    MissingColumn,
    /// <summary>A column has an incorrect data type.</summary>
    WrongType,
    /// <summary>A required index is missing.</summary>
    MissingIndex,
    /// <summary>A required foreign key is missing.</summary>
    MissingForeignKey,
    /// <summary>A required user-defined type is missing.</summary>
    MissingUserDefinedType,
    /// <summary>An unclassified schema error.</summary>
    Other
}

/// <summary>
/// Represents an error found during schema verification.
/// </summary>
/// <param name="Table">The name of the table associated with the error.</param>
/// <param name="Column">The name of the column associated with the error, or <c>null</c> if not column-specific.</param>
/// <param name="ErrorMessage">A description of the error.</param>
/// <param name="Kind">The kind of schema verification error.</param>
public sealed record SchemaVerificationError(
    string Table,
    string? Column,
    string ErrorMessage,
    SchemaVerificationErrorKind Kind);
