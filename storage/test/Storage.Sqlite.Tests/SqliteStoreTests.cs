// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Sqlite;

public class SqliteStoreTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _connectionString;
    private const string ServiceKey = "my-sqlite-store";

    public SqliteStoreTests()
    {
        var serviceCollection = new ServiceCollection();
        _ = serviceCollection.AddLogging();
        var dbName = $"test_{Guid.NewGuid():N}";
        _connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
        _ = serviceCollection.AddStorageInternal(storage => storage.AddSqliteStore(ServiceKey, opt => opt.ConnectionString = _connectionString));
        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    [Fact]
    public void Can_resolve_store()
    {
        var pooledStore = _serviceProvider.GetRequiredKeyedService<IPooledStore>(ServiceKey);

        var store = pooledStore.OpenPool(1);

        _ = store.ShouldNotBeNull();
    }

    [Fact]
    public async Task Can_create_schema()
    {
        var databaseSchema = _serviceProvider.GetRequiredKeyedService<IDatabaseSchema>(ServiceKey);

        var schemaVersionResult = await databaseSchema.CheckVersionAsync(_ct);
        schemaVersionResult.CurrentVersion.ShouldBe(0u);
        schemaVersionResult.IsCompatible.ShouldBeFalse();
        schemaVersionResult.RequiredVersion.ShouldBe(1u);

        await databaseSchema.MigrateAsync(_ct);
        schemaVersionResult = await databaseSchema.CheckVersionAsync(_ct);
        schemaVersionResult.CurrentVersion.ShouldBe(1u);
    }

    [Fact]
    public async Task Create_schema_twice_succeeds()
    {
        var databaseSchema = _serviceProvider.GetRequiredKeyedService<IDatabaseSchema>(ServiceKey);

        await databaseSchema.MigrateAsync(_ct);
        await databaseSchema.MigrateAsync(_ct);
    }

    [Fact]
    public async Task migrate_async_on_fresh_db_succeeds_and_version_is_current()
    {
        var databaseSchema = _serviceProvider.GetRequiredKeyedService<IDatabaseSchema>(ServiceKey);

        await databaseSchema.MigrateAsync(_ct);

        var result = await databaseSchema.CheckVersionAsync(_ct);
        result.CurrentVersion.ShouldBe(result.RequiredVersion);
        result.IsCompatible.ShouldBeTrue();
    }

    [Fact]
    public async Task migrate_async_twice_is_idempotent()
    {
        var databaseSchema = _serviceProvider.GetRequiredKeyedService<IDatabaseSchema>(ServiceKey);

        await databaseSchema.MigrateAsync(_ct);
        var versionAfterFirst = (await databaseSchema.CheckVersionAsync(_ct)).CurrentVersion;

        await databaseSchema.MigrateAsync(_ct);
        var versionAfterSecond = (await databaseSchema.CheckVersionAsync(_ct)).CurrentVersion;

        versionAfterSecond.ShouldBe(versionAfterFirst);
    }

    [Fact]
    public async Task build_migration_script_from_zero_returns_non_empty_script_with_expected_tables()
    {
        var databaseSchema = _serviceProvider.GetRequiredKeyedService<IDatabaseSchema>(ServiceKey);

        var script = databaseSchema.BuildMigrationScript(DatabaseSchemaVersion.Zero);

        script.ShouldNotBeNullOrWhiteSpace();
        script.ShouldContain("entities");
        script.ShouldContain("entity_keys");
        script.ShouldContain("search_values");
        script.ShouldContain("entity_links");
        script.ShouldContain("outbox_subscriber_queue");
    }

    [Fact]
    public async Task build_migration_script_from_current_version_returns_empty_string()
    {
        var databaseSchema = _serviceProvider.GetRequiredKeyedService<IDatabaseSchema>(ServiceKey);

        await databaseSchema.MigrateAsync(_ct);
        var currentVersion = (await databaseSchema.CheckVersionAsync(_ct)).CurrentVersion;

        var script = databaseSchema.BuildMigrationScript(new DatabaseSchemaVersion((int)currentVersion));

        script.Trim().ShouldBeEmpty();
    }

    [Fact]
    public async Task verify_schema_async_on_valid_schema_returns_no_errors()
    {
        var databaseSchema = _serviceProvider.GetRequiredKeyedService<IDatabaseSchema>(ServiceKey);

        await databaseSchema.MigrateAsync(_ct);

        var result = await databaseSchema.VerifySchemaAsync(_ct);

        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task verify_schema_async_detects_dropped_column()
    {
        var databaseSchema = _serviceProvider.GetRequiredKeyedService<IDatabaseSchema>(ServiceKey);

        await databaseSchema.MigrateAsync(_ct);

        await using var cnn = new SqliteConnection(_connectionString);
        await cnn.OpenAsync(_ct);

        // Drop the partial index that references expires_at before dropping the column.
        await using var dropIdxCmd = cnn.CreateCommand();
        dropIdxCmd.CommandText = "DROP INDEX entities_expires_at_index";
        _ = await dropIdxCmd.ExecuteNonQueryAsync(_ct);

        await using var cmd = cnn.CreateCommand();
        cmd.CommandText = "ALTER TABLE entities DROP COLUMN expires_at";
        _ = await cmd.ExecuteNonQueryAsync(_ct);

        var result = await databaseSchema.VerifySchemaAsync(_ct);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.Kind == SchemaVerificationErrorKind.MissingColumn &&
            e.Table == "entities" &&
            e.Column == "expires_at");
    }

    [Fact]
    public async Task migrate_async_throws_when_schema_verification_fails()
    {
        var databaseSchema = _serviceProvider.GetRequiredKeyedService<IDatabaseSchema>(ServiceKey);

        // Migrate first so the schema exists and all steps are already applied.
        await databaseSchema.MigrateAsync(_ct);

        // Corrupt the schema by dropping a column.
        await using var cnn = new SqliteConnection(_connectionString);
        await cnn.OpenAsync(_ct);

        // Drop the partial index that references expires_at before dropping the column.
        await using var dropIdxCmd = cnn.CreateCommand();
        dropIdxCmd.CommandText = "DROP INDEX entities_expires_at_index";
        _ = await dropIdxCmd.ExecuteNonQueryAsync(_ct);

        await using var cmd = cnn.CreateCommand();
        cmd.CommandText = "ALTER TABLE entities DROP COLUMN expires_at";
        _ = await cmd.ExecuteNonQueryAsync(_ct);

        // Second MigrateAsync: no migration steps to run, but VerifySchemaAsync detects the missing column.
        _ = await Should.ThrowAsync<InvalidOperationException>(() => databaseSchema.MigrateAsync(_ct));
    }
}
