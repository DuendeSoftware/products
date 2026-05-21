// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace Duende.Storage.Sqlite;

/// <summary>
/// Configuration options for the SQLite store.
/// </summary>
public sealed class SqliteStoreOptions
{
    /// <summary>
    /// The connection string for the SQLite database.
    /// </summary>
    [Required]
    public string? ConnectionString { get; set; }
}
