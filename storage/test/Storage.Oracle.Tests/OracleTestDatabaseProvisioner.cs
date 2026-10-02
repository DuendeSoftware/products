// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Oracle.ManagedDataAccess.Client;

namespace Duende.Storage.Oracle;

/// <summary>
/// Provisions and pools reusable Oracle schemas (users) across integration tests. Oracle has no
/// lightweight "CREATE DATABASE" per test, so each pooled "database" is a dedicated
/// Oracle user/schema. A test checks out a schema, runs, then releases it; on release
/// the tables are cleared so the next test starts clean. The returned connection
/// string connects as the pooled user, so the storage writes into that user's own
/// schema (no <c>SchemaName</c> option required).
/// </summary>
internal sealed class OracleTestDatabaseProvisioner(string serverConnectionString)
{
    internal const string UserPassword = "DuendeTests1";

    private readonly ConcurrentQueue<string> _available = new();
    private readonly ConcurrentBag<string> _all = [];

    /// <summary>
    /// Reserves a connection string for a schema, synchronously. Reuses a previously
    /// released schema when one is available; otherwise computes a connection string
    /// for a new Oracle user that has not yet been created.
    /// </summary>
    public ProvisionedTestDatabase Provision()
    {
        if (_available.TryDequeue(out var connectionString))
        {
            return new ProvisionedTestDatabase(this, connectionString, exists: true);
        }

        var user = ("POOL_" + Guid.NewGuid().ToString("N")[..24]).ToUpperInvariant();
        var csb = new OracleConnectionStringBuilder(serverConnectionString)
        {
            UserID = user,
            Password = UserPassword
        };
        return new ProvisionedTestDatabase(this, csb.ConnectionString, exists: false);
    }

    /// <summary>
    /// Creates the Oracle user (schema) backing <paramref name="connectionString"/>.
    /// </summary>
    internal async Task CreateAsync(string connectionString, CancellationToken ct)
    {
        var user = new OracleConnectionStringBuilder(connectionString).UserID;

        await using var connection = new OracleConnection(serverConnectionString);
        await connection.OpenAsync(ct);

        await using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = $"CREATE USER \"{user}\" IDENTIFIED BY \"{UserPassword}\"";
            _ = await createCmd.ExecuteNonQueryAsync(ct);
        }

        await using (var grantCmd = connection.CreateCommand())
        {
            grantCmd.CommandText = $"GRANT CONNECT, RESOURCE, UNLIMITED TABLESPACE TO \"{user}\"";
            _ = await grantCmd.ExecuteNonQueryAsync(ct);
        }

        _all.Add(connectionString);
    }

    /// <summary>
    /// Releases a schema back to the pool after clearing all test data.
    /// Deletes in FK-dependency order since the tables use ON DELETE CASCADE
    /// only for the entity children.
    /// </summary>
    internal async Task ReleaseAsync(string connectionString)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var ct = cts.Token;
            await using var connection = new OracleConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                BEGIN
                  DELETE FROM OUTBOX_SUBSCRIBER_QUEUE;
                  DELETE FROM ENTITY_LINKS;
                  DELETE FROM SEARCH_VALUES;
                  DELETE FROM ENTITY_KEYS;
                  DELETE FROM ENTITIES;
                  COMMIT;
                END;
                """;
            _ = await cmd.ExecuteNonQueryAsync(ct);
            _available.Enqueue(connectionString);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // If cleanup fails, don't return to pool — leave it out until DropAllAsync.
            Console.WriteLine($"Failed to clean pooled schema; it will not be reused: {ex.Message}");
        }
    }

    /// <summary>
    /// Drops all Oracle users that were created by this provisioner. Called during
    /// test suite teardown.
    /// </summary>
    public async Task DropAllAsync()
    {
        // Close any pooled physical connections so DROP USER isn't blocked by active sessions.
        OracleConnection.ClearAllPools();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;
        foreach (var connectionString in _all)
        {
            var user = new OracleConnectionStringBuilder(connectionString).UserID;
            try
            {
                await DropUserAsync(serverConnectionString, user, ct);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Console.WriteLine($"Failed to drop pooled schema {user}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Creates a fresh Oracle user (schema), independent of the pool, and returns a
    /// connection string that connects as that user. Used by migration tests, which
    /// need a guaranteed-unmigrated schema rather than a possibly-recycled one.
    /// </summary>
    internal static async Task<(string ConnectionString, string User)> CreateFreshUserAsync(string serverConnectionString, CancellationToken ct)
    {
        var user = ("MIG_" + Guid.NewGuid().ToString("N")[..24]).ToUpperInvariant();

        await using var connection = new OracleConnection(serverConnectionString);
        await connection.OpenAsync(ct);

        await using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = $"CREATE USER \"{user}\" IDENTIFIED BY \"{UserPassword}\"";
            _ = await createCmd.ExecuteNonQueryAsync(ct);
        }

        await using (var grantCmd = connection.CreateCommand())
        {
            grantCmd.CommandText = $"GRANT CONNECT, RESOURCE, UNLIMITED TABLESPACE TO \"{user}\"";
            _ = await grantCmd.ExecuteNonQueryAsync(ct);
        }

        var csb = new OracleConnectionStringBuilder(serverConnectionString)
        {
            UserID = user,
            Password = UserPassword
        };
        return (csb.ConnectionString, user);
    }

    /// <summary>
    /// Drops an Oracle user and all of its objects.
    /// </summary>
    internal static async Task DropUserAsync(string serverConnectionString, string user, CancellationToken ct)
    {
        await using var connection = new OracleConnection(serverConnectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"DROP USER \"{user}\" CASCADE";
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }
}
