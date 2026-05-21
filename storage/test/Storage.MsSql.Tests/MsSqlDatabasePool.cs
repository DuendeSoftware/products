// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;

namespace Duende.Storage.MsSql;

/// <summary>
/// Pools reusable SQL Server databases across integration tests. Instead of
/// creating and dropping a database per test, each test checks out a database,
/// runs, then returns it. On return the database tables are cleared so the
/// next test starts clean.
/// </summary>
internal sealed class MsSqlDatabasePool(string serverConnectionString)
{
    private readonly ConcurrentQueue<string> _available = new();
    private readonly ConcurrentBag<string> _all = new();

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
        await using var connection = new SqlConnection(serverConnectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE [{dbName}]";
        _ = await cmd.ExecuteNonQueryAsync(ct);

        var csb = new SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = dbName };
        var cs = csb.ConnectionString;
        _all.Add(cs);
        return cs;
    }

    /// <summary>
    /// Returns a database to the pool after clearing all test data.
    /// Deletes in FK-dependency order since SQL Server has no TRUNCATE CASCADE.
    /// </summary>
    public async Task ReturnAsync(string connectionString)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var ct = cts.Token;
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                DELETE FROM [dbo].[outbox_subscriber_queue];
                DELETE FROM [dbo].[entity_links];
                DELETE FROM [dbo].[search_values];
                DELETE FROM [dbo].[entity_keys];
                DELETE FROM [dbo].[entities];
                """;
            _ = await cmd.ExecuteNonQueryAsync(ct);
            _available.Enqueue(connectionString);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // If cleanup fails, don't return to pool — leave it out until DropAllAsync.
            Console.WriteLine($"Failed to clean pooled database; it will not be reused: {ex.Message}");
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
        foreach (var connectionString in _all)
        {
            var dbName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
            try
            {
                await using var conn = new SqlConnection(serverConnectionString);
                await conn.OpenAsync(ct);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"""
                    ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{dbName}];
                    """;
                _ = await cmd.ExecuteNonQueryAsync(ct);
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
