// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Schema;

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
