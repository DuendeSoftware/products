// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.Storage.Sqlite.Internal;

namespace Duende.Storage.IntegrationTests;

public partial class MigrationTests
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new SqliteMigrationFixtureFactory();

    [Fact]
    public async Task verify_accepts_not_null_integer_rowid_primary_key_but_rejects_missing_key()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);
        await fixture.ExecuteSqlAsync(RecreateOutboxSql(sequenceNumberDeclaration: "INTEGER PRIMARY KEY NOT NULL"), _ct);

        var result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();

        await fixture.ExecuteSqlAsync(RecreateOutboxSql(sequenceNumberDeclaration: "INTEGER NOT NULL"), _ct);
        result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeFalse();
        var error = result.Errors.ShouldHaveSingleItem();
        error.Kind.ShouldBe(SchemaVerificationErrorKind.Other);
        error.Table.ShouldBe("outbox_subscriber_queue");
        error.ErrorMessage.ShouldContain("Primary key on table 'outbox_subscriber_queue' is missing");
        error.ErrorMessage.ShouldContain("expected (sequence_number)");
    }

    [Fact]
    public async Task verify_accepts_foreign_key_that_omits_referenced_primary_key_columns()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);
        await fixture.ExecuteSqlAsync(
            """
            DROP TABLE entity_keys;
            CREATE TABLE entity_keys (
                pool_id INTEGER NOT NULL,
                entity_type_id INTEGER NOT NULL,
                key_type_id INTEGER NOT NULL,
                key_type_version INTEGER NOT NULL,
                key_type_name TEXT NOT NULL,
                key_value TEXT NOT NULL,
                key_json TEXT NULL,
                entity_id TEXT NOT NULL,
                timestamp TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                PRIMARY KEY (pool_id, entity_type_id, key_type_id, key_type_version, key_value),
                FOREIGN KEY (pool_id, entity_type_id, entity_id)
                    REFERENCES entities ON DELETE CASCADE
            );
            CREATE INDEX entity_keys_entity_type_id_entity_id_index ON entity_keys (entity_type_id, entity_id);
            """, _ct);

        var result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task verify_rejects_foreign_key_that_uses_wrong_entity_type_column()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);
        await fixture.ExecuteSqlAsync(RecreateEntityLinksWithWrongRightFkTypeColumnSql(), _ct);

        var result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        result.IsValid.ShouldBeFalse();
        var error = result.Errors.ShouldHaveSingleItem();
        error.Kind.ShouldBe(SchemaVerificationErrorKind.MissingForeignKey);
        error.Table.ShouldBe("entity_links");
    }

    [Fact]
    public async Task verify_and_version_check_accept_case_variant_table_and_column_names()
    {
        await using var fixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await fixture.StorageInstanceSchema.MigrateAsync(_ct);
        await fixture.ExecuteSqlAsync(
            """
            ALTER TABLE entities RENAME TO temporary_entities;
            ALTER TABLE temporary_entities RENAME TO ENTITIES;
            ALTER TABLE __schema_info RENAME TO temporary_schema_info;
            ALTER TABLE temporary_schema_info RENAME TO __SCHEMA_INFO;
            ALTER TABLE entity_links RENAME COLUMN right_entity_type_id TO temporary_type_id;
            ALTER TABLE entity_links RENAME COLUMN temporary_type_id TO RIGHT_ENTITY_TYPE_ID;
            """, _ct);

        var version = await fixture.StorageInstanceSchema.CheckVersionAsync(_ct);
        var result = await fixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        version.CurrentVersion.ShouldBe(fixture.RequiredVersion);
        result.Errors.ShouldBeEmpty();
    }

    private static Task ApplyVersionOneAsync(IMigrationFixture fixture, Ct ct)
    {
        var script = MigrationScriptLoader.GetScripts(typeof(SqliteStorageEngine).Assembly, DatabaseSchemaVersion.Zero)
            .Single(s => s.TargetVersion == 1).Sql;
        return fixture.ExecuteMigrationScriptAsync(script, ct);
    }

    private static string InsertLegacyOutboxSql(IMigrationFixture fixture) =>
        $$"""
        INSERT INTO {{fixture.SchemaName}}.outbox_subscriber_queue
            (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name)
        VALUES ('{{LegacyMessageId}}', '{{LegacyEventId}}', '2024-01-02T03:04:05+00:00', 'LegacyEvent',
            '{{LegacySubjectId}}', 12, 'LegacyEntity', 7, '{"value":"preserved"}', 'legacy-subscriber')
        """;

    private static string ChangeNullabilitySql(IMigrationFixture _) =>
        RecreateOutboxSql(eventNameNullability: "NULL", versionNullability: "NOT NULL");

    private static string DropPrimaryKeySql(IMigrationFixture _) => RecreateEntityLinksSql(primaryKey: null);

    private static string AddWrongPrimaryKeySql(IMigrationFixture _) =>
        RecreateEntityLinksSql("link_type_id, pool_id, left_entity_id, right_entity_id");

    private static string AddPrimaryKeyWithWrongColumnSql(IMigrationFixture _) =>
        RecreateEntityLinksSql("pool_id, link_type_id, left_entity_id, right_entity_type_id");

    private static string DropUniqueConstraintSql(IMigrationFixture _) => RecreateOutboxSql(uniqueColumn: null);

    private static string AddWrongUniqueConstraintSql(IMigrationFixture _) => RecreateOutboxSql(uniqueColumn: "event_id");

    private static string ChangeForeignKeyDeleteRuleSql(IMigrationFixture _) =>
        """
        DROP TABLE entity_keys;
        CREATE TABLE entity_keys (
            pool_id INTEGER NOT NULL,
            entity_type_id INTEGER NOT NULL,
            key_type_id INTEGER NOT NULL,
            key_type_version INTEGER NOT NULL,
            key_type_name TEXT NOT NULL,
            key_value TEXT NOT NULL,
            key_json TEXT NULL,
            entity_id TEXT NOT NULL,
            timestamp TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
            PRIMARY KEY (pool_id, entity_type_id, key_type_id, key_type_version, key_value),
            FOREIGN KEY (pool_id, entity_type_id, entity_id)
                REFERENCES entities (pool_id, entity_type_id, entity_id) ON DELETE NO ACTION
        );
        CREATE INDEX entity_keys_entity_type_id_entity_id_index ON entity_keys (entity_type_id, entity_id);
        """;

    private static string CreatedAtIndexName(IMigrationFixture _) => "entities_created_at_index";

    private static string DropCreatedAtIndexSql(IMigrationFixture _) => "DROP INDEX entities_created_at_index";

    private static string CreateWrongCreatedAtIndexSql(IMigrationFixture _) =>
        "CREATE INDEX entities_created_at_index ON entities (pool_id, created_at, entity_type_id)";

    private static string CreateIndexWithWrongColumnSql(IMigrationFixture _) =>
        "CREATE INDEX entities_created_at_index ON entities (pool_id, entity_type_id, expires_at)";

    private static string CreateIndexWithExtraKeySql(IMigrationFixture _) =>
        "CREATE INDEX entities_created_at_index ON entities (pool_id, entity_type_id, created_at, value_version)";

    private static string RecreateEntityLinksSql(string? primaryKey) =>
        $"""
        DROP TABLE entity_links;
        CREATE TABLE entity_links (
            pool_id INTEGER NOT NULL,
            link_type_id INTEGER NOT NULL,
            left_entity_type_id INTEGER NOT NULL,
            left_entity_id TEXT NOT NULL,
            right_entity_type_id INTEGER NOT NULL,
            right_entity_id TEXT NOT NULL,
            restrict_delete INTEGER NOT NULL DEFAULT 0,
            created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            {(primaryKey is null ? "" : $", PRIMARY KEY ({primaryKey})")},
            FOREIGN KEY (pool_id, left_entity_type_id, left_entity_id)
                REFERENCES entities (pool_id, entity_type_id, entity_id) ON DELETE NO ACTION,
            FOREIGN KEY (pool_id, right_entity_type_id, right_entity_id)
                REFERENCES entities (pool_id, entity_type_id, entity_id) ON DELETE NO ACTION
        );
        CREATE INDEX entity_links_right_entity_index ON entity_links (pool_id, link_type_id, right_entity_id);
        CREATE INDEX entity_links_left_fk_index ON entity_links (pool_id, left_entity_id, left_entity_type_id);
        CREATE INDEX entity_links_right_fk_index
            ON entity_links (pool_id, right_entity_id, right_entity_type_id, link_type_id, restrict_delete, left_entity_id, left_entity_type_id);
        """;

    // Recreates entity_links with the right-side FK wired to the wrong source column
    // (left_entity_type_id instead of right_entity_type_id): it still references 'entities' with a
    // valid NO ACTION delete rule, so only validating the full ordered column mapping catches it.
    private static string RecreateEntityLinksWithWrongRightFkTypeColumnSql() =>
        """
        DROP TABLE entity_links;
        CREATE TABLE entity_links (
            pool_id INTEGER NOT NULL,
            link_type_id INTEGER NOT NULL,
            left_entity_type_id INTEGER NOT NULL,
            left_entity_id TEXT NOT NULL,
            right_entity_type_id INTEGER NOT NULL,
            right_entity_id TEXT NOT NULL,
            restrict_delete INTEGER NOT NULL DEFAULT 0,
            created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
            PRIMARY KEY (pool_id, link_type_id, left_entity_id, right_entity_id),
            FOREIGN KEY (pool_id, left_entity_type_id, left_entity_id)
                REFERENCES entities (pool_id, entity_type_id, entity_id) ON DELETE NO ACTION,
            FOREIGN KEY (pool_id, left_entity_type_id, right_entity_id)
                REFERENCES entities (pool_id, entity_type_id, entity_id) ON DELETE NO ACTION
        );
        CREATE INDEX entity_links_right_entity_index ON entity_links (pool_id, link_type_id, right_entity_id);
        CREATE INDEX entity_links_left_fk_index ON entity_links (pool_id, left_entity_id, left_entity_type_id);
        CREATE INDEX entity_links_right_fk_index
            ON entity_links (pool_id, right_entity_id, right_entity_type_id, link_type_id, restrict_delete, left_entity_id, left_entity_type_id);
        """;

    private static string RecreateOutboxSql(
        string eventNameNullability = "NOT NULL",
        string versionNullability = "NULL",
        string? uniqueColumn = "message_id",
        string sequenceNumberDeclaration = "INTEGER PRIMARY KEY AUTOINCREMENT") =>
        $"""
        DROP TABLE outbox_subscriber_queue;
        CREATE TABLE outbox_subscriber_queue (
            sequence_number {sequenceNumberDeclaration},
            message_id TEXT NOT NULL,
            event_id TEXT NOT NULL,
            timestamp TEXT NOT NULL,
            event_name TEXT {eventNameNullability},
            subject_id TEXT NOT NULL,
            entity_type_id INTEGER NOT NULL,
            entity_type_name TEXT NOT NULL,
            pool_id INTEGER NOT NULL,
            payload TEXT NOT NULL,
            subscriber_name TEXT NOT NULL,
            dso_type_schema_version INTEGER {versionNullability}
            {(uniqueColumn is null ? "" : $", UNIQUE ({uniqueColumn})")}
        );
        CREATE INDEX outbox_subscriber_queue_subscriber_index
            ON outbox_subscriber_queue (subscriber_name, sequence_number);
        CREATE INDEX outbox_subscriber_queue_pool_id_index
            ON outbox_subscriber_queue (pool_id);
        """;
}

public partial class StoreLinkConcurrencyTests
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new SqliteMigrationFixtureFactory();
}

public partial class StoreLinkDeadlockStressTests
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new SqliteMigrationFixtureFactory();
}

public partial class LinkMigrationTests
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new SqliteMigrationFixtureFactory();
}
