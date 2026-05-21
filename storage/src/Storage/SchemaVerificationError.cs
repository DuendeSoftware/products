// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage;

public enum SchemaVerificationErrorKind
{
    MissingTable,
    MissingColumn,
    WrongType,
    MissingIndex,
    MissingForeignKey,
    MissingUserDefinedType,
    Other
}

public sealed record SchemaVerificationError(
    string Table,
    string? Column,
    string ErrorMessage,
    SchemaVerificationErrorKind Kind);
