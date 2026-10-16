// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Sqlite;

public sealed class RunMigrationsHowToTests
{
    private static IServiceCollection BuildServicesWithDefaultSqlite(string connectionString)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddStorageInternal(storage => storage.AddSqlite(options => options.ConnectionString = connectionString));
        return services;
    }

    [Fact]
    public async Task Migrating_the_default_instance_brings_a_fresh_database_up_to_date()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = $"Data Source=HowToRunMigrations_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var services = BuildServicesWithDefaultSqlite(connectionString);
        await using var provider = services.BuildServiceProvider();

        // begin-snippet: run-migrations-resolve-default-schema
        var schema = provider.GetRequiredService<IStorageInstanceSchema>();
        // end-snippet

        var beforeMigration = await schema.CheckVersionAsync(ct);
        beforeMigration.CurrentVersion.ShouldBe(0u);
        beforeMigration.IsCompatible.ShouldBeFalse();

        await schema.MigrateAsync(ct);

        var afterMigration = await schema.CheckVersionAsync(ct);
        afterMigration.IsCompatible.ShouldBeTrue();
        afterMigration.CurrentVersion.ShouldBe(afterMigration.RequiredVersion);
    }

    [Fact]
    public async Task Migrating_a_named_instance_goes_through_the_schema_factory()
    {
        var ct = TestContext.Current.CancellationToken;
        var catalogInstanceId = StorageInstanceId.Create("catalog");
        var connectionString = $"Data Source=HowToRunMigrations_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddStorageInternal(catalogInstanceId, storage => storage.AddSqlite(options => options.ConnectionString = connectionString));
        await using var provider = services.BuildServiceProvider();

        // begin-snippet: run-migrations-resolve-named-schema
        var factory = provider.GetRequiredService<IStorageInstanceSchemaFactory>();
        var schema = await factory.GetStorageInstanceSchema(catalogInstanceId, ct);
        // end-snippet
        await schema.MigrateAsync(ct);

        var result = await schema.CheckVersionAsync(ct);
        result.IsCompatible.ShouldBeTrue();
    }

    [Fact]
    public async Task Migrating_twice_in_a_row_is_a_no_op_the_second_time()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = $"Data Source=HowToRunMigrations_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var services = BuildServicesWithDefaultSqlite(connectionString);
        await using var provider = services.BuildServiceProvider();
        var schema = provider.GetRequiredService<IStorageInstanceSchema>();

        // begin-snippet: run-migrations-idempotent
        // Safe to call every time your product starts up: a database that's already at the
        // required version is left untouched, and an empty database is created from scratch.
        await schema.MigrateAsync(ct);
        await schema.MigrateAsync(ct);
        // end-snippet

        var result = await schema.VerifySchemaAsync(ct);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Concurrent_migrations_against_a_fresh_database_are_not_safe()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = $"Data Source=HowToRunMigrations_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var services = BuildServicesWithDefaultSqlite(connectionString);
        await using var provider = services.BuildServiceProvider();
        var schema = provider.GetRequiredService<IStorageInstanceSchema>();

        // MigrateAsync calls CheckVersionAsync, THEN opens a connection and applies scripts. Two
        // app instances starting at the same time can both read an old version and both apply the
        // same scripts. Reproduce that deterministically: migrate fully, then roll the recorded
        // version back to 1 so the next MigrateAsync acts like an instance that read the version
        // before the other instance finished. The SQLite V002 script's ALTER TABLE ... ADD COLUMN
        // isn't guarded (SQLite has no "ADD COLUMN IF NOT EXISTS"), so re-running it fails.
        await schema.MigrateAsync(ct);

        await using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync(ct);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE __schema_info SET value = '{\"Version\":1}' WHERE key = 'SchemaVersion'";
            _ = await cmd.ExecuteNonQueryAsync(ct);
        }

        var exception = await Should.ThrowAsync<Exception>(() => schema.MigrateAsync(ct));
        exception.Message.ShouldContain("duplicate column name", Case.Insensitive);
    }

    [Fact]
    public async Task Checking_version_before_migrating_reports_version_zero_and_incompatible()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = $"Data Source=HowToRunMigrations_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var services = BuildServicesWithDefaultSqlite(connectionString);
        await using var provider = services.BuildServiceProvider();
        var schema = provider.GetRequiredService<IStorageInstanceSchema>();

        // begin-snippet: run-migrations-check-version
        var result = await schema.CheckVersionAsync(ct);
        // end-snippet

        result.CurrentVersion.ShouldBe(0u);
        result.IsCompatible.ShouldBeFalse();
    }

    [Fact]
    public async Task Checking_version_after_migrating_reports_it_up_to_date()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = $"Data Source=HowToRunMigrations_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var services = BuildServicesWithDefaultSqlite(connectionString);
        await using var provider = services.BuildServiceProvider();
        var schema = provider.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(ct);

        var result = await schema.CheckVersionAsync(ct);

        result.IsCompatible.ShouldBeTrue();
        result.CurrentVersion.ShouldBe(result.RequiredVersion);
    }

    [Fact]
    public async Task A_schema_newer_than_the_running_code_expects_is_reported_as_incompatible()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = $"Data Source=HowToRunMigrations_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var services = BuildServicesWithDefaultSqlite(connectionString);
        await using var provider = services.BuildServiceProvider();
        var schema = provider.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(ct);

        // Simulate a newer application version having already migrated this database further
        // than the version currently running against it understands.
        await using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync(ct);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE __schema_info SET value = '{\"Version\":999}' WHERE key = 'SchemaVersion'";
            _ = await cmd.ExecuteNonQueryAsync(ct);
        }

        var result = await schema.CheckVersionAsync(ct);

        result.CurrentVersion.ShouldBe(999u);
        result.IsCompatible.ShouldBeFalse();
        (result.CurrentVersion > result.RequiredVersion).ShouldBeTrue();
    }

    [Fact]
    public async Task Verifying_a_freshly_migrated_schema_finds_no_errors()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = $"Data Source=HowToRunMigrations_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var services = BuildServicesWithDefaultSqlite(connectionString);
        await using var provider = services.BuildServiceProvider();
        var schema = provider.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(ct);

        // begin-snippet: run-migrations-verify-schema
        var result = await schema.VerifySchemaAsync(ct);
        // end-snippet

        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task Verifying_a_schema_with_a_table_dropped_out_from_under_it_reports_the_gap()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = $"Data Source=HowToRunMigrations_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var services = BuildServicesWithDefaultSqlite(connectionString);
        await using var provider = services.BuildServiceProvider();
        var schema = provider.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(ct);

        // Something outside Storage's control (a manual script, a bad backup restore) dropped
        // a table the current schema version expects to find.
        await using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync(ct);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "DROP TABLE entity_links";
            _ = await cmd.ExecuteNonQueryAsync(ct);
        }

        var result = await schema.VerifySchemaAsync(ct);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Table == "entity_links" && e.Kind == SchemaVerificationErrorKind.MissingTable);
    }

    [Fact]
    public void Building_the_migration_script_returns_nonempty_sql_without_executing_it()
    {
        var connectionString = $"Data Source=HowToRunMigrations_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var services = BuildServicesWithDefaultSqlite(connectionString);
        using var provider = services.BuildServiceProvider();
        var schema = provider.GetRequiredService<IStorageInstanceSchema>();

        // begin-snippet: run-migrations-build-script
        // Pass DatabaseSchemaVersion.Zero to get the full script for a fresh database, or a
        // specific version to get only the steps needed from there.
        var script = schema.BuildMigrationScript(DatabaseSchemaVersion.Zero);
        // end-snippet

        script.ShouldNotBeNullOrWhiteSpace();
        script.ShouldContain("CREATE TABLE");
    }
}
