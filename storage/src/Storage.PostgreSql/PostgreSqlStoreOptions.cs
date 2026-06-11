// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace Duende.Storage.PostgreSql;

/// <summary>
/// Configuration options for the PostgreSQL store.
/// </summary>
public sealed class PostgreSqlStoreOptions
{
    /// <summary>
    /// The schema name to use for the store's tables.
    /// </summary>
    [Required]
    [StringLength(63, ErrorMessage = "Schema name must not exceed 63 characters due to PostgreSQL's identifier length limit.")]
    [RegularExpression(@"^[a-zA-Z0-9_\-\#\@]+$", ErrorMessage = "Schema name must contain only alphanumeric characters, underscores, hyphens, hashes, and at signs.")]
    public string SchemaName { get; set; } = "public";
}
