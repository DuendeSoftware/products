// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.Storage;

public interface IDatabaseSchema
{
    /// <summary>
    /// Checks the schema version of the database.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    Task<CheckSchemaVersionResult> CheckVersionAsync(Ct ct);

    /// <summary>
    /// Migrates the database schema to the current version. Creates the schema if it does not exist,
    /// upgrades it if behind, and is a no-op if already current. Calls <see cref="VerifySchemaAsync"/>
    /// after migration and throws if verification finds errors.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    Task MigrateAsync(Ct ct);

    /// <summary>
    /// Verifies that the actual database schema matches the expected structure.
    /// Returns a list of discrepancies (missing tables, columns, wrong types, missing indexes, etc.).
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    Task<SchemaVerificationResult> VerifySchemaAsync(Ct ct);

    /// <summary>
    /// Builds a SQL migration script that brings the database from <paramref name="fromVersion"/> to the current version.
    /// Pass <see cref="DatabaseSchemaVersion.Zero"/> to generate the full script for a fresh database.
    /// Each migration step is gated on the schema version number, not object existence.
    /// </summary>
    /// <param name="fromVersion">The version to migrate from.</param>
    string BuildMigrationScript(DatabaseSchemaVersion fromVersion);
}
