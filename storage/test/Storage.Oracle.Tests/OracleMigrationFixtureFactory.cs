// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;

namespace Duende.Storage.Oracle;

internal sealed class OracleMigrationFixtureFactory(AspireFixture aspire) : IMigrationFixtureFactory
{
    public async Task<IMigrationFixture> CreateAsync(CancellationToken ct)
    {
        // Each migration test gets its own fresh Oracle user (schema), so the store
        // writes into that user's own schema (no SchemaName option required).
        var (connectionString, user) = await OracleDatabasePool.CreateUserAsync(aspire.ServerConnectionString, ct);

        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddKeyedSingleton<CreateOracleConnection>("migration-test", () => new OracleConnection(connectionString));
        _ = services.AddStorageInternal(storage => storage.AddOracleStore("migration-test", _ => { }));
        var provider = services.BuildServiceProvider();

        var schema = provider.GetRequiredKeyedService<IDatabaseSchema>("migration-test");
        return new OracleMigrationFixture(provider, aspire.ServerConnectionString, user, schema, connectionString);
    }
}

internal sealed class OracleMigrationFixture(
    ServiceProvider provider,
    string serverConnectionString,
    string user,
    IDatabaseSchema schema,
    string connectionString) : IMigrationFixture
{
    public IDatabaseSchema Schema => schema;

    /// <summary>
    /// Executes raw SQL against the schema. Oracle runs a single statement per command
    /// and cannot batch DDL, so the script is split on lines containing only a slash
    /// (the SQL*Plus statement terminator) and each statement is executed individually.
    /// </summary>
    public async Task ExecuteSqlAsync(string sql, CancellationToken ct)
    {
        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync(ct);

        foreach (var statement in SplitStatements(sql))
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = statement;
            _ = await cmd.ExecuteNonQueryAsync(ct);
        }

        await using var commitCmd = connection.CreateCommand();
        commitCmd.CommandText = "COMMIT";
        _ = await commitCmd.ExecuteNonQueryAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await provider.DisposeAsync();

        try
        {
            OracleConnection.ClearAllPools();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await OracleDatabasePool.DropUserAsync(serverConnectionString, user, cts.Token);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Console.WriteLine($"Failed to drop migration schema {user}: {ex.Message}");
        }
    }

    private static IEnumerable<string> SplitStatements(string sql)
    {
        var current = new System.Text.StringBuilder();
        foreach (var rawLine in sql.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Trim() == "/")
            {
                var statement = current.ToString().Trim();
                if (statement.Length > 0)
                {
                    yield return statement;
                }
                _ = current.Clear();
                continue;
            }

            _ = current.Append(line).Append('\n');
        }

        var last = current.ToString().Trim();
        if (last.Length > 0)
        {
            yield return last;
        }
    }
}
