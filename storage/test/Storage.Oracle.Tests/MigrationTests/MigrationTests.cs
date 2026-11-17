// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using Duende.Storage.Internal;
using Duende.Storage.Oracle;
using Duende.Storage.Oracle.Internal;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;

namespace Duende.Storage.IntegrationTests;

[Collection("OracleIntegration")]
public partial class MigrationTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new OracleMigrationFixtureFactory(fixture);

    [Fact]
    public async Task verify_accepts_descending_keys_and_rejects_function_based_keys()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await migrationFixture.ExecuteSqlAsync(
            $"""
            DROP INDEX {migrationFixture.SchemaName}.IX_ENTITIES_CREATED_AT
            /
            CREATE INDEX {migrationFixture.SchemaName}.IX_ENTITIES_CREATED_AT
                ON {migrationFixture.SchemaName}.entities (pool_id DESC, entity_type_id, created_at DESC)
            /
            DROP INDEX {migrationFixture.SchemaName}.IX_ENTITIES_ENTITY_TYPE_NAME
            /
            CREATE INDEX {migrationFixture.SchemaName}.IX_ENTITIES_ENTITY_TYPE_NAME
                ON {migrationFixture.SchemaName}.entities (entity_type_name DESC)
            """, _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();

        await migrationFixture.ExecuteSqlAsync(
            $"""
            DROP INDEX {migrationFixture.SchemaName}.IX_ENTITIES_ENTITY_TYPE_NAME
            /
            CREATE INDEX {migrationFixture.SchemaName}.IX_ENTITIES_ENTITY_TYPE_NAME
                ON {migrationFixture.SchemaName}.entities (UPPER(entity_type_name))
            """, _ct);

        result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        var error = result.Errors.ShouldHaveSingleItem();
        error.ErrorMessage.ShouldContain("IX_ENTITIES_ENTITY_TYPE_NAME");
        error.ErrorMessage.ShouldContain("<expression>");
    }

    [Fact]
    public async Task verify_rejects_unusable_required_index()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await migrationFixture.ExecuteSqlAsync(
            $"ALTER INDEX {migrationFixture.SchemaName}.IX_ENTITIES_CREATED_AT UNUSABLE", _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        result.Errors.ShouldContain(error =>
            error.Kind == SchemaVerificationErrorKind.MissingIndex &&
            error.ErrorMessage.Contains("IX_ENTITIES_CREATED_AT", StringComparison.Ordinal));
    }

    [Fact]
    public async Task verify_distinguishes_quoted_case_variant_tables_and_columns()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await migrationFixture.ExecuteSqlAsync(
            $"""
            CREATE TABLE {migrationFixture.SchemaName}."entities" (
                POOL_ID NUMBER(10) NOT NULL,
                ENTITY_TYPE_ID NUMBER(10) NOT NULL,
                ENTITY_ID RAW(16) NOT NULL,
                PRIMARY KEY (POOL_ID, ENTITY_TYPE_ID, ENTITY_ID)
            )
            /
            ALTER TABLE {migrationFixture.SchemaName}.ENTITIES ADD ("created_at" TIMESTAMP(6) WITH TIME ZONE)
            /
            ALTER TABLE {migrationFixture.SchemaName}.OUTBOX_SUBSCRIBER_QUEUE ADD ("message_id" RAW(16))
            """, _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.Errors.ShouldBeEmpty();

        await migrationFixture.ExecuteSqlAsync(
            $"""
            DROP INDEX {migrationFixture.SchemaName}.IX_ENTITIES_CREATED_AT
            /
            CREATE INDEX {migrationFixture.SchemaName}.IX_ENTITIES_CREATED_AT
                ON {migrationFixture.SchemaName}.ENTITIES (POOL_ID, ENTITY_TYPE_ID, "created_at")
            """, _ct);

        result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        var error = result.Errors.ShouldHaveSingleItem();
        error.ErrorMessage.ShouldContain("IX_ENTITIES_CREATED_AT");
        error.ErrorMessage.ShouldContain("has key columns (POOL_ID, ENTITY_TYPE_ID, created_at)");
        error.ErrorMessage.ShouldContain("expected (POOL_ID, ENTITY_TYPE_ID, CREATED_AT)");
    }

    [Fact]
    public async Task verify_rejects_renamed_case_sensitive_tables_and_columns()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await migrationFixture.ExecuteSqlAsync(
            $"""
            ALTER TABLE {migrationFixture.SchemaName}.ENTITIES RENAME TO "entities"
            /
            ALTER TABLE {migrationFixture.SchemaName}.ENTITY_LINKS
                RENAME COLUMN RIGHT_ENTITY_TYPE_ID TO "right_entity_type_id"
            """, _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        result.Errors.ShouldContain(error =>
            error.Kind == SchemaVerificationErrorKind.MissingTable && error.Table == "ENTITIES");
        result.Errors.ShouldContain(error =>
            error.Kind == SchemaVerificationErrorKind.MissingColumn &&
            error.Table == "ENTITY_LINKS" && error.Column == "RIGHT_ENTITY_TYPE_ID");
    }

    [Fact]
    public async Task verify_rejects_a_foreign_key_to_a_differently_cased_schema()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        var otherSchema = migrationFixture.SchemaName.ToLowerInvariant();
        var password = Guid.NewGuid().ToString("N");

        await using var admin = new OracleConnection(fixture.ServerConnectionString);
        await admin.OpenAsync(_ct);
        await using var command = admin.CreateCommand();
        command.CommandText = $"CREATE USER \"{otherSchema}\" IDENTIFIED BY \"{password}\"";
        _ = await command.ExecuteNonQueryAsync(_ct);

        try
        {
            command.CommandText = $"GRANT UNLIMITED TABLESPACE TO \"{otherSchema}\"";
            _ = await command.ExecuteNonQueryAsync(_ct);
            command.CommandText = $"""
                CREATE TABLE "{otherSchema}".ENTITIES (
                    POOL_ID NUMBER(10) NOT NULL,
                    ENTITY_TYPE_ID NUMBER(10) NOT NULL,
                    ENTITY_ID RAW(16) NOT NULL,
                    PRIMARY KEY (POOL_ID, ENTITY_TYPE_ID, ENTITY_ID)
                )
                """;
            _ = await command.ExecuteNonQueryAsync(_ct);
            command.CommandText = $"GRANT REFERENCES ON \"{otherSchema}\".ENTITIES TO \"{migrationFixture.SchemaName}\"";
            _ = await command.ExecuteNonQueryAsync(_ct);

            await migrationFixture.ExecuteSqlAsync(
                $"""
                ALTER TABLE {migrationFixture.SchemaName}.ENTITY_KEYS DROP CONSTRAINT FK_ENTITY_KEYS_ENTITIES
                /
                ALTER TABLE {migrationFixture.SchemaName}.ENTITY_KEYS ADD CONSTRAINT FK_ENTITY_KEYS_ENTITIES
                    FOREIGN KEY (POOL_ID, ENTITY_TYPE_ID, ENTITY_ID)
                    REFERENCES "{otherSchema}".ENTITIES (POOL_ID, ENTITY_TYPE_ID, ENTITY_ID) ON DELETE CASCADE
                """, _ct);

            var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

            result.Errors.ShouldContain(error =>
                error.Kind == SchemaVerificationErrorKind.MissingForeignKey && error.Table == "entity_keys");
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await OracleTestDatabaseProvisioner.DropUserAsync(fixture.ServerConnectionString, otherSchema, cleanup.Token);
        }
    }

    [Fact]
    public async Task verify_valid_schema_as_select_only_user()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        var user = ("VERIFY_" + Guid.NewGuid().ToString("N")[..22]).ToUpperInvariant();
        var password = Guid.NewGuid().ToString("N");
        var connectionString = new OracleConnectionStringBuilder(fixture.ServerConnectionString)
        {
            UserID = user,
            Password = password,
        }.ConnectionString;

        await using var admin = new OracleConnection(fixture.ServerConnectionString);
        await admin.OpenAsync(_ct);
        await using var command = admin.CreateCommand();
        command.CommandText = $"CREATE USER \"{user}\" IDENTIFIED BY \"{password}\"";
        _ = await command.ExecuteNonQueryAsync(_ct);

        try
        {
            command.CommandText = $"GRANT CREATE SESSION TO \"{user}\"";
            _ = await command.ExecuteNonQueryAsync(_ct);
            foreach (var table in new[] { "ENTITIES", "ENTITY_KEYS", "SEARCH_VALUES", "ENTITY_LINKS", "OUTBOX_SUBSCRIBER_QUEUE", "__SCHEMA_INFO" })
            {
                command.CommandText = $"GRANT SELECT ON \"{migrationFixture.SchemaName}\".\"{table}\" TO \"{user}\"";
                _ = await command.ExecuteNonQueryAsync(_ct);
            }

            await using (var readOnlyConnection = new OracleConnection(connectionString))
            {
                await readOnlyConnection.OpenAsync(_ct);
                await using var privileges = readOnlyConnection.CreateCommand();
                privileges.CommandText = "SELECT COUNT(*) FROM SESSION_PRIVS WHERE PRIVILEGE IN ('ALTER SESSION', 'CREATE TABLE', 'CREATE ANY TABLE')";
                Convert.ToInt32(await privileges.ExecuteScalarAsync(_ct), CultureInfo.InvariantCulture).ShouldBe(0);
            }

            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddKeyedSingleton<CreateOracleConnection>("read-only", () => new OracleConnection(connectionString));
            _ = services.AddStorageInternal(storage => storage.AddOracle(
                sp => sp.GetRequiredKeyedService<CreateOracleConnection>("read-only"),
                options => options.SchemaName = migrationFixture.SchemaName));
            await using var provider = services.BuildServiceProvider();
            var schema = await provider.GetRequiredService<IStorageInstanceSchemaFactory>().GetStorageInstanceSchema(_ct);

            var version = await schema.CheckVersionAsync(_ct);
            version.IsCompatible.ShouldBeTrue();
            var result = await schema.VerifySchemaAsync(_ct);
            result.IsValid.ShouldBeTrue();
            result.Errors.ShouldBeEmpty();
        }
        finally
        {
            using var pooledConnection = new OracleConnection(connectionString);
            OracleConnection.ClearPool(pooledConnection);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await OracleTestDatabaseProvisioner.DropUserAsync(fixture.ServerConnectionString, user, cleanup.Token);
        }
    }

    private static Task ApplyVersionOneAsync(IMigrationFixture fixture, Ct ct)
    {
        var statements = MigrationScriptLoader.GetScripts(typeof(OracleStorageEngine).Assembly, DatabaseSchemaVersion.Zero, "")
            .Single(s => s.TargetVersion == 1).Statements;
        return fixture.ExecuteMigrationScriptAsync(string.Join("\n/\n", statements), ct);
    }

    private static string InsertLegacyOutboxSql(IMigrationFixture fixture) =>
        $$"""
        INSERT INTO {{fixture.SchemaName}}.outbox_subscriber_queue
            (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name)
        VALUES (HEXTORAW('{{LegacyMessageId.Replace("-", "", StringComparison.Ordinal)}}'),
            HEXTORAW('{{LegacyEventId.Replace("-", "", StringComparison.Ordinal)}}'),
            TO_TIMESTAMP_TZ('2024-01-02 03:04:05 +00:00', 'YYYY-MM-DD HH24:MI:SS TZH:TZM'),
            'LegacyEvent', HEXTORAW('{{LegacySubjectId.Replace("-", "", StringComparison.Ordinal)}}'),
            12, 'LegacyEntity', 7, '{"value":"preserved"}', 'legacy-subscriber')
        """;

    private static string ChangeNullabilitySql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE {fixture.SchemaName}.outbox_subscriber_queue MODIFY (event_name NULL)
        /
        ALTER TABLE {fixture.SchemaName}.outbox_subscriber_queue MODIFY (dso_type_schema_version NOT NULL)
        """;

    private static string DropPrimaryKeySql(IMigrationFixture fixture) =>
        $"ALTER TABLE {fixture.SchemaName}.entity_links DROP CONSTRAINT PK_ENTITY_LINKS";

    private static string AddWrongPrimaryKeySql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE {fixture.SchemaName}.entity_links ADD CONSTRAINT PK_ENTITY_LINKS
            PRIMARY KEY (link_type_id, pool_id, left_entity_id, right_entity_id)
        """;

    private static string AddPrimaryKeyWithWrongColumnSql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE {fixture.SchemaName}.entity_links ADD CONSTRAINT PK_ENTITY_LINKS
            PRIMARY KEY (pool_id, link_type_id, left_entity_id, right_entity_type_id)
        """;

    private static string DropUniqueConstraintSql(IMigrationFixture fixture) =>
        $"ALTER TABLE {fixture.SchemaName}.outbox_subscriber_queue DROP CONSTRAINT UQ_OUTBOX_MESSAGE_ID";

    private static string AddWrongUniqueConstraintSql(IMigrationFixture fixture) =>
        $"ALTER TABLE {fixture.SchemaName}.outbox_subscriber_queue ADD CONSTRAINT UQ_OUTBOX_MESSAGE_ID UNIQUE (event_id)";

    [Fact]
    public async Task verify_reports_entity_links_foreign_key_with_wrong_delete_ruleAsync()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await migrationFixture.ExecuteSqlAsync(ChangeEntityLinksRightForeignKeyDeleteRuleSql(migrationFixture), _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error =>
            string.Equals(error.Table, "ENTITY_LINKS", StringComparison.OrdinalIgnoreCase) &&
            error.ErrorMessage.Contains("FK_ENTITY_LINKS_RIGHT", StringComparison.Ordinal) &&
            error.ErrorMessage.Replace('_', ' ').Contains("delete rule 'CASCADE', expected 'NO ACTION'", StringComparison.Ordinal));
    }

    private static string ChangeEntityLinksRightForeignKeyDeleteRuleSql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE {fixture.SchemaName}.entity_links DROP CONSTRAINT FK_ENTITY_LINKS_RIGHT
        /
        ALTER TABLE {fixture.SchemaName}.entity_links ADD CONSTRAINT FK_ENTITY_LINKS_RIGHT
            FOREIGN KEY (pool_id, right_entity_type_id, right_entity_id)
            REFERENCES {fixture.SchemaName}.entities (pool_id, entity_type_id, entity_id)
            ON DELETE CASCADE
        """;

    private static string ChangeForeignKeyDeleteRuleSql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE {fixture.SchemaName}.entity_keys DROP CONSTRAINT FK_ENTITY_KEYS_ENTITIES
        /
        ALTER TABLE {fixture.SchemaName}.entity_keys ADD CONSTRAINT FK_ENTITY_KEYS_ENTITIES
            FOREIGN KEY (pool_id, entity_type_id, entity_id)
            REFERENCES {fixture.SchemaName}.entities (pool_id, entity_type_id, entity_id)
        """;

    private static string CreatedAtIndexName(IMigrationFixture _) => "IX_ENTITIES_CREATED_AT";

    private static string DropCreatedAtIndexSql(IMigrationFixture fixture) =>
        $"DROP INDEX {fixture.SchemaName}.IX_ENTITIES_CREATED_AT";

    private static string CreateWrongCreatedAtIndexSql(IMigrationFixture fixture) =>
        $"""
        CREATE INDEX {fixture.SchemaName}.IX_ENTITIES_CREATED_AT
            ON {fixture.SchemaName}.entities (pool_id, created_at, entity_type_id)
        """;

    private static string CreateIndexWithWrongColumnSql(IMigrationFixture fixture) =>
        $"""
        CREATE INDEX {fixture.SchemaName}.IX_ENTITIES_CREATED_AT
            ON {fixture.SchemaName}.entities (pool_id, entity_type_id, expires_at)
        """;

    private static string CreateIndexWithExtraKeySql(IMigrationFixture fixture) =>
        $"""
        CREATE INDEX {fixture.SchemaName}.IX_ENTITIES_CREATED_AT
            ON {fixture.SchemaName}.entities (pool_id, entity_type_id, created_at, value_version)
        """;
}

[Collection("OracleIntegration")]
public partial class StoreLinkConcurrencyTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new OracleMigrationFixtureFactory(fixture);
}

[Collection("OracleIntegration")]
public partial class StoreLinkDeadlockStressTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new OracleMigrationFixtureFactory(fixture);
}

[Collection("OracleIntegration")]
public partial class LinkMigrationTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new OracleMigrationFixtureFactory(fixture);
}

[Collection("OracleIntegration")]
public partial class StoreLinkDeterministicConcurrencyTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new OracleMigrationFixtureFactory(fixture);
    private IConcurrencyTestFixture ConcurrencyTestFixture { get; } = new OracleConcurrencyTestFixture();
}
