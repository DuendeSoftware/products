// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage;

/// <summary>
/// Contains the results of a database schema verification.
/// </summary>
/// <param name="Errors">The list of errors found during verification.</param>
public sealed record SchemaVerificationResult(IReadOnlyList<SchemaVerificationError> Errors)
{
    /// <summary>
    /// Gets a value indicating whether the schema is valid (no errors were found).
    /// </summary>
    public bool IsValid => Errors.Count == 0;
}
