// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Npgsql;

namespace Duende.Storage.PostgreSql;

/// <summary>
/// Pools reusable PostgreSQL databases across integration tests. Instead of
/// creating and dropping a database per test, each test checks out a database,
/// runs, then returns it. On return the database tables are truncated so the
/// next test starts clean.
/// </summary>
internal sealed class PostgreSqlDatabasePool(string serverConnectionString)
{
    private readonly ConcurrentQueue<string> _available = new();
    private readonly ConcurrentBag<string> _all = [];

    /// <summary>
    /// Returns a connection string for a ready-to-use database. The caller is
    /// responsible for applying the schema. Creates a new database if none are
    /// available in the pool.
    /// </summary>
    public async Task<string> GetConnectionStringAsync(CancellationToken ct)
    {
        if (_available.TryDequeue(out var connectionString))
        {
            return connectionString;
        }

        var dbName = $"pool_{Guid.NewGuid():N}";
        await using var ds = NpgsqlDataSource.Create(serverConnectionString);
        await using var cmd = ds.CreateCommand($"CREATE DATABASE \"{dbName}\"");
        _ = await cmd.ExecuteNonQueryAsync(ct);

        var csb = new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = dbName };
        var cs = csb.ConnectionString;
        _all.Add(cs);
        return cs;
    }

    /// <summary>
    /// Returns a database to the pool after truncating all test data.
    /// </summary>
    public async Task ReturnAsync(string connectionString)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var ds = NpgsqlDataSource.Create(connectionString);
            await using var cmd = ds.CreateCommand("""
                TRUNCATE TABLE public.outbox_subscriber_queue;
                TRUNCATE TABLE public.entity_links;
                TRUNCATE TABLE public.entities CASCADE;
                """);
            _ = await cmd.ExecuteNonQueryAsync(cts.Token);
            _available.Enqueue(connectionString);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // If truncation fails, don't return the database to the pool —
            // it stays poisoned until DropAllAsync cleans it up.
            Console.WriteLine($"Failed to truncate pooled database; it will not be reused: {ex.Message}");
        }
    }

    /// <summary>
    /// Drops all databases that were created by this pool. Called during
    /// test suite teardown.
    /// </summary>
    public async Task DropAllAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;
        await using var ds = NpgsqlDataSource.Create(serverConnectionString);
        foreach (var connectionString in _all)
        {
            var dbName = new NpgsqlConnectionStringBuilder(connectionString).Database!;
            try
            {
                await using var terminateCmd = ds.CreateCommand("""
                    SELECT pg_terminate_backend(pid) FROM pg_stat_activity
                    WHERE datname = $1 AND pid <> pg_backend_pid();
                    """);
                _ = terminateCmd.Parameters.Add(new NpgsqlParameter { Value = dbName });
                _ = await terminateCmd.ExecuteNonQueryAsync(ct);
                await using var dropCmd = ds.CreateCommand($"DROP DATABASE \"{dbName}\"");
                _ = await dropCmd.ExecuteNonQueryAsync(ct);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Console.WriteLine($"Failed to drop pooled database {dbName}: {ex.Message}");
            }
        }
    }
}
