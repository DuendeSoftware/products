// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.MsSql;
using Duende.Storage.MsSql.Internal;
using Duende.Storage.Schema;

namespace Duende.Storage.IntegrationTests;

[Collection("MsSqlIntegration")]
public partial class MigrationTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new MsSqlMigrationFixtureFactory(fixture);

    [Fact]
    public async Task verify_allows_included_index_columns()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await migrationFixture.ExecuteSqlAsync(DropCreatedAtIndexSql(migrationFixture), _ct);
        await migrationFixture.ExecuteSqlAsync(
            $"""
            CREATE INDEX [{CreatedAtIndexName(migrationFixture)}]
                ON [{migrationFixture.SchemaName}].entities (pool_id, entity_type_id, created_at)
                INCLUDE (entity_type_name, value_version)
            """, _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task verify_rejects_disabled_required_index()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await migrationFixture.ExecuteSqlAsync(
            $"ALTER INDEX [{CreatedAtIndexName(migrationFixture)}] ON [{migrationFixture.SchemaName}].entities DISABLE",
            _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        result.Errors.ShouldContain(error =>
            error.Kind == SchemaVerificationErrorKind.MissingIndex &&
            error.ErrorMessage.Contains(CreatedAtIndexName(migrationFixture), StringComparison.Ordinal));
    }

    [Fact]
    public async Task verify_rejects_renamed_identifiers_in_a_case_sensitive_database()
    {
        await using var migrationFixture =
            await ((MsSqlMigrationFixtureFactory)MigrationFixtureFactory).CreateAsync(caseSensitiveIdentifiers: true, _ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await RenameIdentifiersAsync(migrationFixture, _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        result.Errors.ShouldContain(error =>
            error.Kind == SchemaVerificationErrorKind.MissingTable && error.Table == "entities");
        result.Errors.ShouldContain(error =>
            error.Kind == SchemaVerificationErrorKind.MissingColumn &&
            error.Table == "entity_links" && error.Column == "right_entity_type_id");
    }

    [Fact]
    public async Task verify_accepts_renamed_identifiers_in_a_case_insensitive_database()
    {
        await using var migrationFixture =
            await ((MsSqlMigrationFixtureFactory)MigrationFixtureFactory).CreateAsync(caseSensitiveIdentifiers: false, _ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await RenameIdentifiersAsync(migrationFixture, _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task verify_rejects_case_distinct_index_columns_and_foreign_key_schemas()
    {
        await using var migrationFixture =
            await ((MsSqlMigrationFixtureFactory)MigrationFixtureFactory).CreateAsync(caseSensitiveIdentifiers: true, _ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        var schema = migrationFixture.SchemaName;
        var otherSchema = schema.ToUpperInvariant();
        await migrationFixture.ExecuteSqlAsync(
            $"""
            ALTER TABLE [{schema}].entities ADD [CREATED_AT] DATETIMEOFFSET NULL;
            DROP INDEX [{CreatedAtIndexName(migrationFixture)}] ON [{schema}].entities;
            CREATE INDEX [{CreatedAtIndexName(migrationFixture)}]
                ON [{schema}].entities (pool_id, entity_type_id, [CREATED_AT]);
            EXEC('CREATE SCHEMA [{otherSchema}]');
            CREATE TABLE [{otherSchema}].entities (
                pool_id INT NOT NULL,
                entity_type_id INT NOT NULL,
                entity_id UNIQUEIDENTIFIER NOT NULL,
                PRIMARY KEY (pool_id, entity_type_id, entity_id)
            );
            ALTER TABLE [{schema}].entity_keys DROP CONSTRAINT [FK_{schema}_entity_keys_entities];
            ALTER TABLE [{schema}].entity_keys ADD CONSTRAINT [FK_{schema}_entity_keys_entities]
                FOREIGN KEY (pool_id, entity_type_id, entity_id)
                REFERENCES [{otherSchema}].entities (pool_id, entity_type_id, entity_id) ON DELETE CASCADE;
            """, _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        result.Errors.ShouldContain(error =>
            error.Table == "entities" && error.ErrorMessage.Contains("CREATED_AT", StringComparison.Ordinal));
        result.Errors.ShouldContain(error =>
            error.Kind == SchemaVerificationErrorKind.MissingForeignKey && error.Table == "entity_keys");
    }

    [Fact]
    public async Task verify_rejects_untrusted_link_foreign_key()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await migrationFixture.ExecuteSqlAsync(UntrustRightLinkForeignKeySql(migrationFixture), _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error =>
            error.Kind == SchemaVerificationErrorKind.MissingForeignKey &&
            error.Table == "entity_links" &&
            error.ErrorMessage.Contains("right", StringComparison.Ordinal));
    }

    // Drops and recreates the right-side link FK WITH NOCHECK: SQL Server marks a WITH NOCHECK
    // foreign key as not trusted (sys.foreign_keys.is_not_trusted = 1) since it never validated
    // existing rows against it, even though the constraint itself is enabled and otherwise
    // identical. Verification must reject this the same way it rejects a missing FK.
    private static string UntrustRightLinkForeignKeySql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE [{fixture.SchemaName}].entity_links DROP CONSTRAINT [FK_{fixture.SchemaName}_entity_links_right];
        ALTER TABLE [{fixture.SchemaName}].entity_links WITH NOCHECK ADD CONSTRAINT [FK_{fixture.SchemaName}_entity_links_right]
            FOREIGN KEY (pool_id, right_entity_type_id, right_entity_id)
            REFERENCES [{fixture.SchemaName}].entities (pool_id, entity_type_id, entity_id) ON DELETE NO ACTION;
        """;

    private static Task RenameIdentifiersAsync(IMigrationFixture fixture, Ct ct) =>
        fixture.ExecuteSqlAsync(
            $"""
            EXEC sys.sp_rename N'{fixture.SchemaName}.entities', N'ENTITIES';
            EXEC sys.sp_rename N'{fixture.SchemaName}.entity_links.right_entity_type_id', N'RIGHT_ENTITY_TYPE_ID', N'COLUMN';
            """, ct);

    private static Task ApplyVersionOneAsync(IMigrationFixture fixture, Ct ct)
    {
        var script = MigrationScriptLoader.GetScripts(typeof(MsSqlStorageEngine).Assembly, DatabaseSchemaVersion.Zero, fixture.SchemaName)
            .Single(s => s.TargetVersion == 1).Sql;
        return fixture.ExecuteMigrationScriptAsync(script, ct);
    }

    private static string InsertLegacyOutboxSql(IMigrationFixture fixture) =>
        $$"""
        INSERT INTO {{fixture.SchemaName}}.outbox_subscriber_queue
            (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name)
        VALUES ('{{LegacyMessageId}}', '{{LegacyEventId}}', '2024-01-02T03:04:05+00:00', 'LegacyEvent',
            '{{SqlServerGuidConverter.ToSqlServer(Guid.Parse(LegacySubjectId))}}', 12, 'LegacyEntity', 7, '{"value":"preserved"}', 'legacy-subscriber')
        """;

    private static string ChangeNullabilitySql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE [{fixture.SchemaName}].outbox_subscriber_queue ALTER COLUMN event_name NVARCHAR(500) NULL;
        ALTER TABLE [{fixture.SchemaName}].outbox_subscriber_queue ALTER COLUMN dso_type_schema_version INT NOT NULL;
        """;

    private static string DropPrimaryKeySql(IMigrationFixture fixture) =>
        $"ALTER TABLE [{fixture.SchemaName}].entity_links DROP CONSTRAINT [PK_{fixture.SchemaName}_entity_links]";

    private static string AddWrongPrimaryKeySql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE [{fixture.SchemaName}].entity_links ADD CONSTRAINT [PK_{fixture.SchemaName}_entity_links]
            PRIMARY KEY (link_type_id, pool_id, left_entity_id, right_entity_id)
        """;

    private static string AddPrimaryKeyWithWrongColumnSql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE [{fixture.SchemaName}].entity_links ADD CONSTRAINT [PK_{fixture.SchemaName}_entity_links]
            PRIMARY KEY (pool_id, link_type_id, left_entity_id, right_entity_type_id)
        """;

    private static string DropUniqueConstraintSql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE [{fixture.SchemaName}].outbox_subscriber_queue
            DROP CONSTRAINT [UQ_{fixture.SchemaName}_outbox_subscriber_queue_message_id]
        """;

    private static string AddWrongUniqueConstraintSql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE [{fixture.SchemaName}].outbox_subscriber_queue
            ADD CONSTRAINT [UQ_{fixture.SchemaName}_outbox_subscriber_queue_message_id] UNIQUE (event_id)
        """;

    private static string ChangeForeignKeyDeleteRuleSql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE [{fixture.SchemaName}].entity_keys DROP CONSTRAINT [FK_{fixture.SchemaName}_entity_keys_entities];
        ALTER TABLE [{fixture.SchemaName}].entity_keys ADD CONSTRAINT [FK_{fixture.SchemaName}_entity_keys_entities]
            FOREIGN KEY (pool_id, entity_type_id, entity_id)
            REFERENCES [{fixture.SchemaName}].entities (pool_id, entity_type_id, entity_id) ON DELETE NO ACTION;
        """;

    private static string CreatedAtIndexName(IMigrationFixture fixture) => $"IX_{fixture.SchemaName}_entities_created_at";

    private static string DropCreatedAtIndexSql(IMigrationFixture fixture) =>
        $"DROP INDEX [{CreatedAtIndexName(fixture)}] ON [{fixture.SchemaName}].entities";

    private static string CreateWrongCreatedAtIndexSql(IMigrationFixture fixture) =>
        $"""
        CREATE INDEX [{CreatedAtIndexName(fixture)}]
            ON [{fixture.SchemaName}].entities (pool_id, created_at, entity_type_id)
        """;

    private static string CreateIndexWithWrongColumnSql(IMigrationFixture fixture) =>
        $"""
        CREATE INDEX [{CreatedAtIndexName(fixture)}]
            ON [{fixture.SchemaName}].entities (pool_id, entity_type_id, expires_at)
        """;

    private static string CreateIndexWithExtraKeySql(IMigrationFixture fixture) =>
        $"""
        CREATE INDEX [{CreatedAtIndexName(fixture)}]
            ON [{fixture.SchemaName}].entities (pool_id, entity_type_id, created_at, value_version)
        """;
}

[Collection("MsSqlIntegration")]
public partial class StoreLinkConcurrencyTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new MsSqlMigrationFixtureFactory(fixture);
}

[Collection("MsSqlIntegration")]
public partial class StoreLinkDeadlockStressTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new MsSqlMigrationFixtureFactory(fixture);
}

[Collection("MsSqlIntegration")]
public partial class LinkMigrationTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new MsSqlMigrationFixtureFactory(fixture);
}

[Collection("MsSqlIntegration")]
public partial class StoreLinkDeterministicConcurrencyTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new MsSqlMigrationFixtureFactory(fixture);
    private IConcurrencyTestFixture ConcurrencyTestFixture { get; } = new MsSqlConcurrencyTestFixture();
}
