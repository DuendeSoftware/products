// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace Duende.Storage.MsSql;

/// <summary>
/// Configuration options for the SQL Server store.
/// </summary>
public sealed class MsSqlStoreOptions
{
    /// <summary>
    /// The database schema name to use for tables.
    /// Default is "dbo".
    /// </summary>
    [Required]
    [RegularExpression(@"^[a-zA-Z0-9_\-\#\@]+$", ErrorMessage = "Schema name must contain only alphanumeric characters, underscores, hyphens, hashes, and at signs.")]
    public string SchemaName { get; set; } = "dbo";
}
