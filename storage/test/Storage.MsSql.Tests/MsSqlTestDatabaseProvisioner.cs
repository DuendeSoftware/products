// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;

namespace Duende.Storage.MsSql;

/// <summary>
/// Provisions and pools reusable SQL Server databases across integration tests. Instead of
/// creating and dropping a database per test, each test checks out a database,
/// runs, then releases it. On release the database tables are cleared so the
/// next test starts clean.
/// </summary>
internal sealed class MsSqlTestDatabaseProvisioner(string serverConnectionString)
{
    private readonly ConcurrentQueue<string> _available = new();
    private readonly ConcurrentBag<string> _all = new();

    /// <summary>
    /// Reserves a connection string for a database, synchronously. Reuses a previously
    /// released database when one is available; otherwise computes a connection string
    /// for a new database that has not yet been created.
    /// </summary>
    public ProvisionedTestDatabase Provision()
    {
        if (_available.TryDequeue(out var connectionString))
        {
            return new ProvisionedTestDatabase(this, connectionString, exists: true);
        }

        var dbName = $"pool_{Guid.NewGuid():N}";
        var csb = new SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = dbName };
        return new ProvisionedTestDatabase(this, csb.ConnectionString, exists: false);
    }

    /// <summary>
    /// Creates the database backing <paramref name="connectionString"/>.
    /// </summary>
    internal async Task CreateAsync(string connectionString, CancellationToken ct)
    {
        var dbName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        await using var connection = new SqlConnection(serverConnectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE [{dbName}]";
        _ = await cmd.ExecuteNonQueryAsync(ct);
        _all.Add(connectionString);
    }

    /// <summary>
    /// Releases a database back to the pool after clearing all test data.
    /// Deletes in FK-dependency order since SQL Server has no TRUNCATE CASCADE.
    /// </summary>
    internal async Task ReleaseAsync(string connectionString)
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
    /// Drops all databases that were created by this provisioner. Called during
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
