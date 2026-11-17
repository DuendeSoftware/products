// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.PostgreSql;
using Duende.Storage.PostgreSql.Internal;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Duende.Storage.IntegrationTests;

[Collection("PostgreSqlIntegration")]
public partial class MigrationTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new PostgreSqlMigrationFixtureFactory(fixture);

    [Fact]
    public async Task verify_allows_included_index_columns()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await migrationFixture.ExecuteSqlAsync(DropCreatedAtIndexSql(migrationFixture), _ct);
        await migrationFixture.ExecuteSqlAsync(
            $"""
            CREATE INDEX entities_created_at_index
                ON {migrationFixture.SchemaName}.entities (pool_id, entity_type_id, created_at)
                INCLUDE (entity_type_name, value_version)
            """, _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task verify_accepts_extra_quoted_case_variant_index_and_unique_constraint()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await migrationFixture.ExecuteSqlAsync(
            $"""
            CREATE INDEX "ENTITIES_CREATED_AT_INDEX"
                ON {migrationFixture.SchemaName}.entities (pool_id, entity_type_id, created_at);
            ALTER TABLE {migrationFixture.SchemaName}.outbox_subscriber_queue
                ADD CONSTRAINT "OUTBOX_SUBSCRIBER_QUEUE_MESSAGE_ID_KEY" UNIQUE (event_id);
            """, _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task verify_rejects_invalid_required_index()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await migrationFixture.ExecuteSqlAsync($"DROP INDEX {migrationFixture.SchemaName}.entities_created_at_index", _ct);
        await migrationFixture.ExecuteSqlAsync(
            $"""
            INSERT INTO {migrationFixture.SchemaName}.entities
                (pool_id, entity_type_id, entity_id, entity_type_name, value,
                 dso_type_schema_version, value_version, created_at)
            VALUES
                (1, 1, '00000000-0000-0000-0000-000000000001', 'entity', 'null'::jsonb, 1, 1, '2024-01-01T00:00:00Z'),
                (1, 1, '00000000-0000-0000-0000-000000000002', 'entity', 'null'::jsonb, 1, 1, '2024-01-01T00:00:00Z')
            """, _ct);

        _ = await Should.ThrowAsync<PostgresException>(() =>
            migrationFixture.ExecuteSqlAsync(
                $"""
                CREATE UNIQUE INDEX CONCURRENTLY entities_created_at_index
                    ON {migrationFixture.SchemaName}.entities (pool_id, entity_type_id, created_at)
                """, _ct));

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        result.Errors.ShouldContain(error =>
            error.Kind == SchemaVerificationErrorKind.MissingIndex &&
            error.ErrorMessage.Contains("entities_created_at_index", StringComparison.Ordinal));
    }

    [Fact]
    public async Task verify_distinguishes_case_sensitive_tables()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await migrationFixture.ExecuteSqlAsync(
            $"""
            CREATE TABLE {migrationFixture.SchemaName}."ENTITIES" (value_version integer NOT NULL);
            CREATE TABLE {migrationFixture.SchemaName}."ENTITY_KEYS" (
                pool_id integer,
                entity_type_id integer,
                entity_id uuid,
                CONSTRAINT entity_keys_pool_id_entity_type_id_entity_id_fkey
                    FOREIGN KEY (pool_id, entity_type_id, entity_id)
                    REFERENCES {migrationFixture.SchemaName}.entities (pool_id, entity_type_id, entity_id) ON DELETE CASCADE
            );
            """, _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();

        await migrationFixture.ExecuteSqlAsync(
            $"""
            ALTER TABLE {migrationFixture.SchemaName}.entity_keys
                DROP CONSTRAINT entity_keys_pool_id_entity_type_id_entity_id_fkey;
            ALTER TABLE {migrationFixture.SchemaName}.entities DROP COLUMN value_version;
            """, _ct);
        result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        result.Errors.Count.ShouldBe(2);
        result.Errors.ShouldContain(error =>
            error.Kind == SchemaVerificationErrorKind.MissingColumn &&
            error.Table == "entities" && error.Column == "value_version");
        result.Errors.ShouldContain(error =>
            error.Kind == SchemaVerificationErrorKind.MissingForeignKey && error.Table == "entity_keys");
    }

    [Fact]
    public async Task verify_rejects_renamed_case_sensitive_tables_and_columns()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await migrationFixture.ExecuteSqlAsync(
            $"""
            ALTER TABLE {migrationFixture.SchemaName}.entities RENAME TO "ENTITIES";
            ALTER TABLE {migrationFixture.SchemaName}.entity_links
                RENAME COLUMN right_entity_type_id TO "RIGHT_ENTITY_TYPE_ID";
            """, _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        result.Errors.ShouldContain(error =>
            error.Kind == SchemaVerificationErrorKind.MissingTable && error.Table == "entities");
        result.Errors.ShouldContain(error =>
            error.Kind == SchemaVerificationErrorKind.MissingColumn &&
            error.Table == "entity_links" && error.Column == "right_entity_type_id");
    }

    [Fact]
    public async Task verify_rejects_case_distinct_index_and_foreign_key_columns()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await migrationFixture.ExecuteSqlAsync(
            $"""
            ALTER TABLE {migrationFixture.SchemaName}.entities ADD COLUMN "CREATED_AT" timestamp without time zone;
            DROP INDEX {migrationFixture.SchemaName}.entities_created_at_index;
            CREATE INDEX entities_created_at_index
                ON {migrationFixture.SchemaName}.entities (pool_id, entity_type_id, "CREATED_AT");
            """, _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        var error = result.Errors.ShouldHaveSingleItem();
        error.ErrorMessage.ShouldContain("CREATED_AT");

        await migrationFixture.ExecuteSqlAsync(
            $"""
            DROP INDEX {migrationFixture.SchemaName}.entities_created_at_index;
            CREATE INDEX entities_created_at_index
                ON {migrationFixture.SchemaName}.entities (pool_id, entity_type_id, created_at);
            ALTER TABLE {migrationFixture.SchemaName}.entity_keys ADD COLUMN "POOL_ID" integer;
            ALTER TABLE {migrationFixture.SchemaName}.entity_keys
                DROP CONSTRAINT entity_keys_pool_id_entity_type_id_entity_id_fkey;
            ALTER TABLE {migrationFixture.SchemaName}.entity_keys
                ADD CONSTRAINT entity_keys_pool_id_entity_type_id_entity_id_fkey
                FOREIGN KEY ("POOL_ID", entity_type_id, entity_id)
                REFERENCES {migrationFixture.SchemaName}.entities (pool_id, entity_type_id, entity_id) ON DELETE CASCADE;
            """, _ct);

        result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);
        error = result.Errors.ShouldHaveSingleItem();
        error.Kind.ShouldBe(SchemaVerificationErrorKind.MissingForeignKey);
        error.Table.ShouldBe("entity_keys");
    }

    [Fact]
    public async Task verify_rejects_entity_links_foreign_key_with_wrong_column_mapping()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await migrationFixture.ExecuteSqlAsync(
            $"""
            ALTER TABLE {migrationFixture.SchemaName}.entity_links DROP CONSTRAINT fk_entity_links_right;
            ALTER TABLE {migrationFixture.SchemaName}.entity_links
                ADD CONSTRAINT fk_entity_links_right
                    FOREIGN KEY (pool_id, left_entity_type_id, right_entity_id)
                    REFERENCES {migrationFixture.SchemaName}.entities (pool_id, entity_type_id, entity_id) ON DELETE NO ACTION;
            """, _ct);

        var result = await migrationFixture.StorageInstanceSchema.VerifySchemaAsync(_ct);

        result.IsValid.ShouldBeFalse();
        var error = result.Errors.ShouldHaveSingleItem();
        error.Kind.ShouldBe(SchemaVerificationErrorKind.MissingForeignKey);
        error.Table.ShouldBe("entity_links");
    }

    [Fact]
    public async Task verify_reads_version_from_schema_with_a_hyphen()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        var originalSchema = migrationFixture.SchemaName;
        var schemaName = $"{originalSchema}-storage";
        var databaseName = (string)(await migrationFixture.ExecuteScalarAsync("SELECT current_database()", _ct))!;
        await migrationFixture.ExecuteSqlAsync($"ALTER SCHEMA \"{originalSchema}\" RENAME TO \"{schemaName}\"", _ct);

        try
        {
            var connectionString = new NpgsqlConnectionStringBuilder(fixture.ServerConnectionString)
            {
                Database = databaseName,
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddStorageInternal(storage => storage.AddPostgreSql(
                _ => dataSource,
                options => options.SchemaName = schemaName));
            await using var provider = services.BuildServiceProvider();
            var schema = await provider.GetRequiredService<IStorageInstanceSchemaFactory>().GetStorageInstanceSchema(_ct);

            var version = await schema.CheckVersionAsync(_ct);
            var result = await schema.VerifySchemaAsync(_ct);

            version.IsCompatible.ShouldBeTrue();
            result.Errors.ShouldBeEmpty();
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await migrationFixture.ExecuteSqlAsync($"ALTER SCHEMA \"{schemaName}\" RENAME TO \"{originalSchema}\"", cleanup.Token);
        }
    }

    [Fact]
    public async Task verify_valid_schema_as_select_only_role()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        var roleName = $"migration_readonly_{Guid.NewGuid():N}";
        var password = Guid.NewGuid().ToString("N");
        await migrationFixture.ExecuteSqlAsync($"CREATE ROLE \"{roleName}\" LOGIN PASSWORD '{password}'", _ct);

        try
        {
            await migrationFixture.ExecuteSqlAsync(
                $"""
                GRANT USAGE ON SCHEMA "{migrationFixture.SchemaName}" TO "{roleName}";
                GRANT SELECT ON ALL TABLES IN SCHEMA "{migrationFixture.SchemaName}" TO "{roleName}";
                """, _ct);

            var databaseName = (string)(await migrationFixture.ExecuteScalarAsync("SELECT current_database()", _ct))!;
            var connectionString = new NpgsqlConnectionStringBuilder(fixture.ServerConnectionString)
            {
                Database = databaseName,
                Username = roleName,
                Password = password,
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(connectionString);

            await using (var permissions = dataSource.CreateCommand(
                """
                SELECT current_user,
                       has_schema_privilege($1, 'USAGE'),
                       has_schema_privilege($1, 'CREATE'),
                       (SELECT count(*) FROM pg_catalog.pg_class t
                        JOIN pg_catalog.pg_namespace n ON n.oid = t.relnamespace
                        WHERE n.nspname = $1 AND t.relkind = 'r'
                          AND t.relowner <> (SELECT oid FROM pg_catalog.pg_roles WHERE rolname = current_user)
                          AND has_table_privilege(t.oid, 'SELECT')
                          AND NOT has_table_privilege(t.oid, 'INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER')),
                       (SELECT count(*) FROM information_schema.table_constraints
                        WHERE table_schema = $1 AND constraint_type IN ('PRIMARY KEY', 'UNIQUE'))
                """))
            {
                _ = permissions.Parameters.AddWithValue(migrationFixture.SchemaName);
                await using var reader = await permissions.ExecuteReaderAsync(_ct);
                (await reader.ReadAsync(_ct)).ShouldBeTrue();
                reader.GetString(0).ShouldBe(roleName);
                reader.GetBoolean(1).ShouldBeTrue();
                reader.GetBoolean(2).ShouldBeFalse();
                reader.GetInt64(3).ShouldBe(5L);
                reader.GetInt64(4).ShouldBe(0L);
            }

            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddStorageInternal(storage => storage.AddPostgreSql(
                _ => dataSource,
                options => options.SchemaName = migrationFixture.SchemaName));
            await using var readOnlyProvider = services.BuildServiceProvider();
            var schema = await readOnlyProvider.GetRequiredService<IStorageInstanceSchemaFactory>().GetStorageInstanceSchema(_ct);

            var version = await schema.CheckVersionAsync(_ct);
            var result = await schema.VerifySchemaAsync(_ct);

            version.IsCompatible.ShouldBeTrue();
            result.IsValid.ShouldBeTrue();
            result.Errors.ShouldBeEmpty();
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await migrationFixture.ExecuteSqlAsync(
                $"""
                REVOKE SELECT ON ALL TABLES IN SCHEMA "{migrationFixture.SchemaName}" FROM "{roleName}";
                REVOKE USAGE ON SCHEMA "{migrationFixture.SchemaName}" FROM "{roleName}";
                DROP ROLE "{roleName}";
                """, cleanup.Token);
        }
    }

    [Fact]
    public async Task verify_rejects_foreign_key_to_case_distinct_table_hidden_from_select_only_role()
    {
        await using var migrationFixture = await MigrationFixtureFactory.CreateAsync(_ct);
        await migrationFixture.StorageInstanceSchema.MigrateAsync(_ct);
        await migrationFixture.ExecuteSqlAsync(
            $"""
            CREATE TABLE "{migrationFixture.SchemaName}"."ENTITIES" (
                pool_id integer NOT NULL,
                entity_type_id integer NOT NULL,
                entity_id uuid NOT NULL,
                PRIMARY KEY (pool_id, entity_type_id, entity_id)
            );
            ALTER TABLE "{migrationFixture.SchemaName}".entity_keys
                DROP CONSTRAINT entity_keys_pool_id_entity_type_id_entity_id_fkey;
            ALTER TABLE "{migrationFixture.SchemaName}".entity_keys
                ADD CONSTRAINT entity_keys_pool_id_entity_type_id_entity_id_fkey
                FOREIGN KEY (pool_id, entity_type_id, entity_id)
                REFERENCES "{migrationFixture.SchemaName}"."ENTITIES"
                    (pool_id, entity_type_id, entity_id) ON DELETE CASCADE;
            """, _ct);

        var roleName = $"migration_readonly_fk_{Guid.NewGuid():N}";
        var password = Guid.NewGuid().ToString("N");
        await migrationFixture.ExecuteSqlAsync(
            $"CREATE ROLE \"{roleName}\" LOGIN PASSWORD '{password}'", _ct);

        try
        {
            await migrationFixture.ExecuteSqlAsync(
                $"""
                GRANT USAGE ON SCHEMA "{migrationFixture.SchemaName}" TO "{roleName}";
                GRANT SELECT ON ALL TABLES IN SCHEMA "{migrationFixture.SchemaName}" TO "{roleName}";
                REVOKE SELECT ON "{migrationFixture.SchemaName}"."ENTITIES" FROM "{roleName}";
                """, _ct);

            var databaseName = (string)(await migrationFixture.ExecuteScalarAsync("SELECT current_database()", _ct))!;
            var connectionString = new NpgsqlConnectionStringBuilder(fixture.ServerConnectionString)
            {
                Database = databaseName,
                Username = roleName,
                Password = password,
                Pooling = false,
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            await using (var hiddenColumns = dataSource.CreateCommand(
                "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = $1 AND table_name = 'ENTITIES'"))
            {
                _ = hiddenColumns.Parameters.AddWithValue(migrationFixture.SchemaName);
                (await hiddenColumns.ExecuteScalarAsync(_ct)).ShouldBe(0L);
            }

            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddStorageInternal(storage => storage.AddPostgreSql(
                _ => dataSource,
                options => options.SchemaName = migrationFixture.SchemaName));
            await using var readOnlyProvider = services.BuildServiceProvider();
            var schema = await readOnlyProvider.GetRequiredService<IStorageInstanceSchemaFactory>()
                .GetStorageInstanceSchema(_ct);

            var result = await schema.VerifySchemaAsync(_ct);

            result.Errors.ShouldContain(error =>
                error.Kind == SchemaVerificationErrorKind.MissingForeignKey && error.Table == "entity_keys");
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await migrationFixture.ExecuteSqlAsync(
                $"""
                REVOKE SELECT ON ALL TABLES IN SCHEMA "{migrationFixture.SchemaName}" FROM "{roleName}";
                REVOKE USAGE ON SCHEMA "{migrationFixture.SchemaName}" FROM "{roleName}";
                DROP ROLE "{roleName}";
                """, cleanup.Token);
        }
    }

    private static Task ApplyVersionOneAsync(IMigrationFixture fixture, Ct ct)
    {
        var script = MigrationScriptLoader.GetScripts(typeof(PostgreSqlStorageEngine).Assembly, DatabaseSchemaVersion.Zero, fixture.SchemaName)
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

    private static string ChangeNullabilitySql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE {fixture.SchemaName}.outbox_subscriber_queue
            ALTER COLUMN event_name DROP NOT NULL,
            ALTER COLUMN dso_type_schema_version SET NOT NULL;
        """;

    private static string DropPrimaryKeySql(IMigrationFixture fixture) =>
        $"ALTER TABLE {fixture.SchemaName}.entity_links DROP CONSTRAINT entity_links_pkey";

    private static string AddWrongPrimaryKeySql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE {fixture.SchemaName}.entity_links ADD CONSTRAINT entity_links_pkey
            PRIMARY KEY (link_type_id, pool_id, left_entity_id, right_entity_id)
        """;

    private static string AddPrimaryKeyWithWrongColumnSql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE {fixture.SchemaName}.entity_links ADD CONSTRAINT entity_links_pkey
            PRIMARY KEY (pool_id, link_type_id, left_entity_id, right_entity_type_id)
        """;

    private static string DropUniqueConstraintSql(IMigrationFixture fixture) =>
        $"ALTER TABLE {fixture.SchemaName}.outbox_subscriber_queue DROP CONSTRAINT outbox_subscriber_queue_message_id_key";

    private static string AddWrongUniqueConstraintSql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE {fixture.SchemaName}.outbox_subscriber_queue ADD CONSTRAINT outbox_subscriber_queue_message_id_key
            UNIQUE (event_id)
        """;

    private static string ChangeForeignKeyDeleteRuleSql(IMigrationFixture fixture) =>
        $"""
        ALTER TABLE {fixture.SchemaName}.entity_keys
            DROP CONSTRAINT entity_keys_pool_id_entity_type_id_entity_id_fkey;
        ALTER TABLE {fixture.SchemaName}.entity_keys ADD CONSTRAINT entity_keys_pool_id_entity_type_id_entity_id_fkey
            FOREIGN KEY (pool_id, entity_type_id, entity_id)
            REFERENCES {fixture.SchemaName}.entities (pool_id, entity_type_id, entity_id) ON DELETE NO ACTION;
        """;

    private static string CreatedAtIndexName(IMigrationFixture _) => "entities_created_at_index";

    private static string DropCreatedAtIndexSql(IMigrationFixture fixture) =>
        $"DROP INDEX {fixture.SchemaName}.entities_created_at_index";

    private static string CreateWrongCreatedAtIndexSql(IMigrationFixture fixture) =>
        $"""
        CREATE INDEX entities_created_at_index
            ON {fixture.SchemaName}.entities (pool_id, created_at, entity_type_id)
        """;

    private static string CreateIndexWithWrongColumnSql(IMigrationFixture fixture) =>
        $"""
        CREATE INDEX entities_created_at_index
            ON {fixture.SchemaName}.entities (pool_id, entity_type_id, expires_at);
        -- The valid uppercase variant must not mask the incorrect exact-name index.
        CREATE INDEX "ENTITIES_CREATED_AT_INDEX"
            ON {fixture.SchemaName}.entities (pool_id, entity_type_id, created_at)
        """;

    private static string CreateIndexWithExtraKeySql(IMigrationFixture fixture) =>
        $"""
        CREATE INDEX entities_created_at_index
            ON {fixture.SchemaName}.entities (pool_id, entity_type_id, created_at, value_version)
        """;
}

[Collection("PostgreSqlIntegration")]
public partial class StoreLinkConcurrencyTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new PostgreSqlMigrationFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class StoreLinkDeadlockStressTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new PostgreSqlMigrationFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class LinkMigrationTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new PostgreSqlMigrationFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class StoreLinkDeterministicConcurrencyTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new PostgreSqlMigrationFixtureFactory(fixture);
    private IConcurrencyTestFixture ConcurrencyTestFixture { get; } = new PostgreSqlConcurrencyTestFixture();
}
