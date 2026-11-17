// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.CliPlugin.Commands;

public sealed class VerifyHandlerTests
{
    [Fact]
    public void Read_only_connection_string_does_not_treat_case_variant_memory_name_as_in_memory()
    {
        var connectionString = VerifyHandler.GetReadOnlyConnectionString("sqlite", "Data Source=:MEMORY:");

        new SqliteConnectionStringBuilder(connectionString).Mode.ShouldBe(SqliteOpenMode.ReadOnly);
    }

    [Fact]
    public async Task Version_one_schema_returns_pending_without_applying_version_two()
    {
        var connectionString = $"Data Source=file:TestDb{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        var assembly = typeof(SqliteStorageEngineOptions).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name =>
            name.EndsWith("V001_InitialCreate.sql", StringComparison.Ordinal));
        using var script = new StreamReader(assembly.GetManifestResourceStream(resource)!);
        await ExecuteAsync(connection, await script.ReadToEndAsync());
        await using var stdout = new StringWriter();
        await using var stderr = new StringWriter();

        var result = await VerifyHandler.RunAsync("sqlite", connectionString, null, stdout, stderr, Ct.None);

        result.ShouldBe(2);
        stdout.ToString().ShouldContain("Current schema version: 1");
        stdout.ToString().ShouldContain("Required schema version: 3");
        stderr.ToString().ShouldContain("dso_type_schema_version");
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('outbox_subscriber_queue') WHERE name = 'dso_type_schema_version'";
        (await command.ExecuteScalarAsync()).ShouldBe(0L);
    }

    [Fact]
    public async Task Current_schema_succeeds_without_changing_the_database_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"duende-storage-verify-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        try
        {
            await CreateSchemaAsync(connectionString);
            var before = await File.ReadAllBytesAsync(path);
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            await using var stdout = new StringWriter();
            await using var stderr = new StringWriter();

            var result = await VerifyHandler.RunAsync("sqlite", connectionString, null, stdout, stderr, Ct.None);

            result.ShouldBe(0);
            stdout.ToString().ShouldContain("Current schema version: 3");
            stdout.ToString().ShouldContain("Required schema version: 3");
            stdout.ToString().ShouldContain("verification succeeded", Case.Insensitive);
            stderr.ToString().ShouldBeEmpty();
            (await File.ReadAllBytesAsync(path)).ShouldBe(before);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            }

            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("mode=rw")]
    [InlineData("mode=rwc")]
    [InlineData("mode=r%77")]
    [InlineData("%6dode=rw")]
    public async Task Current_schema_file_uri_succeeds_read_only(string query)
    {
        var path = Path.Combine(Path.GetTempPath(), $"duende-storage-verify-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"{new Uri(path).AbsoluteUri}?{query}",
            Pooling = false,
        }.ToString();
        var setupConnectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        try
        {
            await CreateSchemaAsync(setupConnectionString);
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
            }

            var before = await File.ReadAllBytesAsync(path);
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            await using var stdout = new StringWriter();
            await using var stderr = new StringWriter();

            var result = await VerifyHandler.RunAsync("sqlite", connectionString, null, stdout, stderr, Ct.None);

            stderr.ToString().ShouldBeEmpty();
            stdout.ToString().ShouldContain("verification succeeded", Case.Insensitive);
            result.ShouldBe(0);
            stdout.ToString().ShouldContain("Current schema version: 3");
            stdout.ToString().ShouldContain("Required schema version: 3");
            (await File.ReadAllBytesAsync(path)).ShouldBe(before);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            }

            File.Delete(path);
        }
    }

    [Fact]
    public async Task Failed_postgresql_connection_does_not_disclose_host_or_port()
    {
        await using var stdout = new StringWriter();
        await using var stderr = new StringWriter();

        var result = await VerifyHandler.RunAsync(
            "postgresql", "Host=127.0.0.1;Port=1;Timeout=1;Pooling=false", null, stdout, stderr, Ct.None);

        result.ShouldBe(1);
        stderr.ToString().ShouldContain("Failed to access the database");
        stderr.ToString().ShouldNotContain("127.0.0.1:1");
        stderr.ToString().ShouldNotContain("127.0.0.1");
    }

    [Fact]
    public async Task Missing_sqlite_file_is_not_created_and_its_path_is_not_disclosed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"duende-storage-secret-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        try
        {
            File.Exists(path).ShouldBeFalse();
            await using var stdout = new StringWriter();
            await using var stderr = new StringWriter();

            var result = await VerifyHandler.RunAsync("sqlite", connectionString, null, stdout, stderr, Ct.None);

            result.ShouldBe(1);
            File.Exists(path).ShouldBeFalse();
            stderr.ToString().ShouldContain("database", Case.Insensitive);
            stderr.ToString().ShouldNotContain(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Sqlite_file_uri_cannot_create_a_database_when_connection_mode_is_memory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"duende-storage-verify-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"{new Uri(path).AbsoluteUri}?mode=rwc",
            Mode = SqliteOpenMode.Memory,
            Pooling = false,
        }.ToString();
        try
        {
            var result = await VerifyHandler.RunAsync(
                "sqlite", connectionString, null, TextWriter.Null, TextWriter.Null, Ct.None);

            File.Exists(path).ShouldBeFalse();
            result.ShouldBe(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Current_schema_reports_each_structural_error_on_its_own_line()
    {
        var connectionString = $"Data Source=TestDb{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await CreateSchemaAsync(connectionString);
        var columnName = "first\r\nsecond";
        await ExecuteAsync(connection,
            $"""
            ALTER TABLE entities ADD COLUMN "{columnName}" TEXT;
            DROP INDEX entities_created_at_index;
            CREATE INDEX entities_created_at_index ON entities (pool_id, entity_type_id, "{columnName}");
            DROP INDEX entities_last_updated_at_index;
            """);
        await using var stdout = new StringWriter();
        await using var stderr = new StringWriter();

        var result = await VerifyHandler.RunAsync("sqlite", connectionString, null, stdout, stderr, Ct.None);

        result.ShouldBe(1);
        var lines = stderr.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Length.ShouldBe(3);
        lines.ShouldContain(line => line.Contains("entities_created_at_index", StringComparison.Ordinal) &&
                                    line.Contains(@"first\r\nsecond", StringComparison.Ordinal));
        lines.ShouldContain(line => line.Contains("entities_last_updated_at_index", StringComparison.Ordinal));
        lines.ShouldNotContain(line => line.Contains("entities_created_at_index", StringComparison.Ordinal) &&
                                       line.Contains("entities_last_updated_at_index", StringComparison.Ordinal));
        stdout.ToString().ShouldNotContain("verification succeeded", Case.Insensitive);
    }

    [Fact]
    public async Task Sqlite_primary_key_that_can_store_null_is_reported()
    {
        var connectionString = $"Data Source=TestDb{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await CreateSchemaAsync(connectionString);
        await ExecuteAsync(connection,
            """
            DROP TABLE outbox_subscriber_queue;
            CREATE TABLE outbox_subscriber_queue (
                sequence_number INTEGER PRIMARY KEY DESC,
                message_id TEXT NOT NULL UNIQUE,
                event_id TEXT NOT NULL,
                timestamp TEXT NOT NULL,
                event_name TEXT NOT NULL,
                subject_id TEXT NOT NULL,
                entity_type_id INTEGER NOT NULL,
                entity_type_name TEXT NOT NULL,
                pool_id INTEGER NOT NULL,
                payload TEXT NOT NULL,
                subscriber_name TEXT NOT NULL,
                dso_type_schema_version INTEGER NULL
            );
            CREATE INDEX outbox_subscriber_queue_subscriber_index
                ON outbox_subscriber_queue (subscriber_name, sequence_number);
            CREATE INDEX outbox_subscriber_queue_pool_id_index
                ON outbox_subscriber_queue (pool_id);
            """);
        await using var stdout = new StringWriter();
        await using var stderr = new StringWriter();

        var result = await VerifyHandler.RunAsync("sqlite", connectionString, null, stdout, stderr, Ct.None);

        result.ShouldBe(1);
        stderr.ToString().ShouldContain("outbox_subscriber_queue.sequence_number");
        stderr.ToString().ShouldContain("nullable, expected not nullable");
    }

    [Fact]
    public async Task Newer_version_returns_error_and_still_reports_structural_errors()
    {
        var connectionString = $"Data Source=TestDb{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await CreateSchemaAsync(connectionString);
        await ExecuteAsync(connection,
            """UPDATE __schema_info SET value = '{"Version":999}' WHERE key = 'SchemaVersion'; DROP INDEX entities_created_at_index;""");
        await using var stdout = new StringWriter();
        await using var stderr = new StringWriter();

        var result = await VerifyHandler.RunAsync("sqlite", connectionString, null, stdout, stderr, Ct.None);

        result.ShouldBe(1);
        stdout.ToString().ShouldContain("Current schema version: 999");
        stdout.ToString().ShouldContain("Required schema version: 3");
        stderr.ToString().ShouldContain("newer", Case.Insensitive);
        stderr.ToString().ShouldContain("entities_created_at_index");
    }

    [Fact]
    public async Task Invalid_connection_string_returns_an_error()
    {
        await using var stdout = new StringWriter();
        await using var stderr = new StringWriter();

        var result = await VerifyHandler.RunAsync("sqlite", "not-a-valid-connection-string", null, stdout, stderr,
            Ct.None);

        result.ShouldBe(1);
        stderr.ToString().ShouldContain("Invalid connection string", Case.Insensitive);
    }

    [Theory]
    [InlineData("postgresql", "Host=localhost")]
    [InlineData("mssql", "Server=localhost")]
    [InlineData("oracle", "Data Source=localhost/ORCLCDB")]
    public async Task Invalid_schema_is_reported_as_a_configuration_error(string provider, string connectionString)
    {
        await using var stdout = new StringWriter();
        await using var stderr = new StringWriter();

        var result = await VerifyHandler.RunAsync(provider, connectionString, "invalid schema", stdout, stderr, Ct.None);

        result.ShouldBe(1);
        stderr.ToString().ShouldContain("Invalid provider configuration");
        stderr.ToString().ShouldContain("schema", Case.Insensitive);
        stderr.ToString().ShouldNotContain("unexpected error", Case.Insensitive);
    }

    internal static async Task CreateSchemaAsync(string connectionString)
    {
        await using var services = DatabaseProviderFactory.CreateServiceProvider("sqlite", connectionString, null);
        await services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct.None);
    }

    internal static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        _ = await command.ExecuteNonQueryAsync();
    }
}
