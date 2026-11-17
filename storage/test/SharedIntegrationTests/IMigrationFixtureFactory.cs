// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Abstraction for creating a migration test fixture in provider-agnostic tests.
/// Each provider implements this to wire up its own DI and database.
/// </summary>
public interface IMigrationFixtureFactory
{
    /// <summary>
    /// Creates a fresh migration fixture backed by a new (empty) database.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="configureServices">Optional additional service registrations (e.g. DSO registrations
    /// needed to create, update or query entities through the fixture's storage, beyond schema/migration checks).</param>
    Task<IMigrationFixture> CreateAsync(Ct ct, Action<IServiceCollection>? configureServices = null);
}

/// <summary>
/// A disposable fixture that exposes the <see cref="IStorageInstanceSchema"/> and
/// a way to execute raw SQL against the same database.
/// </summary>
public interface IMigrationFixture : IAsyncDisposable
{
    // The version that the latest value of the storage is at. 
    uint RequiredVersion { get; }

    /// <summary>
    /// The schema containing this fixture's tables.
    /// </summary>
    string SchemaName { get; }

    IStorageInstanceSchema StorageInstanceSchema { get; }

    IPartitionedStorageFactory PartitionedStorageFactory { get; }

    ICrossPartitionStorageFactory CrossPartitionStorageFactory { get; }

    /// <summary>
    /// Executes raw SQL against the database backing this fixture.
    /// </summary>
    Task ExecuteSqlAsync(string sql, Ct ct);

    /// <summary>
    /// Executes a migration script (e.g. from <see cref="IStorageInstanceSchema.BuildMigrationScript"/>
    /// or a provider's own migration steps) against the database backing this fixture, verbatim.
    /// Unlike <see cref="ExecuteSqlAsync"/>, this never rewrites the script's SQL — a migration
    /// script already qualifies its own table references by schema, and some providers' scripts
    /// (e.g. SQL Server's V002, which checks <c>INFORMATION_SCHEMA.COLUMNS</c> for a literal table
    /// name) contain string literals that a textual qualification pass could mistake for table
    /// references and corrupt.
    /// </summary>
    Task ExecuteMigrationScriptAsync(string sql, Ct ct);

    /// <summary>
    /// Executes a scalar query against the database backing this fixture.
    /// </summary>
    Task<object?> ExecuteScalarAsync(string sql, Ct ct);

    /// <summary>
    /// Drops a column from a Storage table in this fixture's schema.
    /// </summary>
    Task DropColumnAsync(string table, string column, Ct ct);

    /// <summary>
    /// Runs only the migration steps up to (and including) <paramref name="version"/> against the fresh
    /// (unmigrated) database backing this fixture, without verifying the schema. Used to put a database at
    /// an older schema version (e.g. V2) so a test can seed data before running <see cref="IStorageInstanceSchema.MigrateAsync"/>
    /// to bring it to the latest version.
    /// </summary>
    Task MigrateToAsync(uint version, Ct ct);

    /// <summary>
    /// Executes a scalar raw SQL query (e.g. a row count) against the database backing this fixture.
    /// </summary>
    Task<long> ExecuteScalarCountAsync(string sql, Ct ct);

    /// <summary>
    /// Reads every row currently in the <c>entity_links</c> table, as plain data — used to compare
    /// the full row set before and after a migration, not just a row count.
    /// </summary>
    Task<IReadOnlyList<EntityLinkRow>> DumpEntityLinksAsync(Ct ct);

    /// <summary>
    /// Inserts a row directly into <c>entity_links</c> as it exists at schema version 2 (no
    /// <c>restrict_delete</c> column, no foreign keys), for migration tests that must seed data a
    /// V2 database could hold without going through <see cref="IPartitionedStorage.LinkAsync"/> (which
    /// always writes the V3 <c>restrict_delete</c> column and so cannot run against a V2 schema).
    /// </summary>
    Task InsertLegacyLinkAsync(int poolId, int linkTypeId, int leftEntityTypeId, Guid leftEntityId, int rightEntityTypeId, Guid rightEntityId, Ct ct);

    /// <summary>
    /// Counts raw <c>entity_links</c> rows matching the given tuple exactly, via a parameterized
    /// query against the table directly (no join to <c>entities</c>). Unlike <see cref="IPartitionedStorage.QueryLinksAsync"/>,
    /// this still sees a row whose left or right endpoint entity has been deleted — which, given the
    /// <c>entity_links</c> foreign keys, should never happen, so this is how a test proves a dangling
    /// link truly didn't survive a race, instead of relying on a join-based query that can't tell
    /// "no link" apart from "link exists, but its entity is gone so the join hides it".
    /// </summary>
    Task<long> CountEntityLinksAsync(int poolId, int linkTypeId, int leftEntityTypeId, Guid leftEntityId, int rightEntityTypeId, Guid rightEntityId, Ct ct);

    /// <summary>
    /// True if this fixture's provider gates every migration step on the stored schema version, so
    /// rerunning the full migration script against an already-migrated database always completes as a
    /// no-op (PostgreSQL, SQL Server, Oracle). False for SQLite, whose V002/V003 steps use an unguarded
    /// <c>ALTER TABLE ADD COLUMN</c> (SQLite has no "ADD COLUMN IF NOT EXISTS") and so fail partway
    /// through a rerun — see <see cref="IsExpectedRerunFailure"/> for the one, specific failure that is.
    /// </summary>
    bool MigrationScriptsAreRerunnable { get; }

    /// <summary>
    /// Tells whether <paramref name="exception"/> is the one, specific failure this fixture's provider is
    /// known to produce when the full migration script is rerun against an already-migrated database:
    /// true only for SQLite's "duplicate column name" error from its unguarded <c>ALTER TABLE ADD COLUMN</c>
    /// steps (see the V003 script's own comment). Every other provider gates each step on the stored
    /// version and never throws on a rerun (<see cref="MigrationScriptsAreRerunnable"/> is true), so they
    /// return false for every exception. A test asserting a rerun's outcome must not swallow any exception
    /// this returns false for — doing so would hide a real regression (e.g. a broken version gate) behind
    /// what looks like the known, harmless SQLite failure.
    /// </summary>
    bool IsExpectedRerunFailure(Exception exception);
}

/// <summary>
/// A raw dump of one row of the <c>entity_links</c> table, for full before/after row-set comparisons
/// in migration tests (counts alone can't tell an orphan row apart from a different valid row).
/// </summary>
public sealed record EntityLinkRow(
    int PoolId,
    int LinkTypeId,
    int LeftEntityTypeId,
    Guid LeftEntityId,
    int RightEntityTypeId,
    Guid RightEntityId);
