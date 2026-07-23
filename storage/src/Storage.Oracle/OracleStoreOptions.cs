// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace Duende.Storage.Oracle;

/// <summary>
/// Configuration options for the Oracle store.
/// </summary>
public sealed class OracleStoreOptions
{
    /// <summary>
    /// The schema (Oracle user) that owns the store's tables.
    /// When <see langword="null"/> or empty, the connecting user's current schema is resolved
    /// automatically via <c>SYS_CONTEXT('USERENV','CURRENT_SCHEMA')</c> and all object names
    /// are qualified with that schema. When set, the provided value is uppercased and used as
    /// the schema qualifier. Oracle folds unquoted identifiers to uppercase.
    /// </summary>
    [StringLength(128, ErrorMessage = "Schema name must not exceed 128 characters due to Oracle's identifier length limit.")]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_\$\#]*$", ErrorMessage = "Schema name must start with a letter and contain only alphanumeric characters, underscores, dollar signs, and hashes.")]
    public string? SchemaName { get; set; }
}
