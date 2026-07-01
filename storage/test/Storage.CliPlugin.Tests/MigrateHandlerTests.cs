// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Schema;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.CliPlugin.Commands;

public sealed class MigrateHandlerTests
{
    [Fact]
    public async Task Handler_creates_schema_on_fresh_database_and_returns_success()
    {
        var connectionString = CreateSqliteInMemoryConnectionString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var stdoutWriter = new StringWriter();
        await using var stderrWriter = new StringWriter();

        var result = await MigrateHandler.RunAsync("sqlite", connectionString, null, false, stdoutWriter, stderrWriter,
            Ct.None);

        result.ShouldBe(0);
        stdoutWriter.ToString().ShouldContain("Migration complete");
        stderrWriter.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Handler_reports_up_to_date_on_already_migrated_database_and_returns_success()
    {
        var connectionString = CreateSqliteInMemoryConnectionString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var stdoutWriter = new StringWriter();
        await using var stderrWriter = new StringWriter();

        _ = await MigrateHandler.RunAsync("sqlite", connectionString, null, false, TextWriter.Null, TextWriter.Null,
            Ct.None);
        var result = await MigrateHandler.RunAsync("sqlite", connectionString, null, false, stdoutWriter,
            stderrWriter, Ct.None);

        result.ShouldBe(0);
        stdoutWriter.ToString().ShouldContain("up to date");
        stderrWriter.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Handler_outputs_sql_without_modifying_schema_during_dry_run()
    {
        var connectionString = CreateSqliteInMemoryConnectionString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var stdoutWriter = new StringWriter();
        await using var stderrWriter = new StringWriter();

        var result = await MigrateHandler.RunAsync("sqlite", connectionString, null, true, stdoutWriter, stderrWriter,
            Ct.None);

        result.ShouldBe(0);
        var output = stdoutWriter.ToString();
        output.ShouldContain("Dry run complete");
        output.ShouldContain("CREATE");
        stderrWriter.ToString().ShouldBeEmpty();

        await using var serviceProvider =
            DatabaseProviderFactory.CreateServiceProvider("sqlite", connectionString, null);
        var schema = serviceProvider.GetRequiredService<IDatabaseSchema>();
        var versionResult = await schema.CheckVersionAsync(Ct.None);

        versionResult.CurrentVersion.ShouldBe(0u);
    }

    [Fact]
    public async Task Handler_reports_error_for_invalid_connection_string()
    {
        await using var writer = new StringWriter();
        await using var errorWriter = new StringWriter();

        var result = await MigrateHandler.RunAsync("sqlite", "not-a-valid-connection-string", null, false, writer,
            errorWriter, Ct.None);

        result.ShouldBe(1);
        errorWriter.ToString().ShouldContain("Invalid connection string", Case.Insensitive);
    }

    private static string CreateSqliteInMemoryConnectionString() =>
        $"Data Source=TestDb{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
}
