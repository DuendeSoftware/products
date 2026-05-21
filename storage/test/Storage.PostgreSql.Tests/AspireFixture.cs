// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Npgsql;

namespace Duende.Storage.PostgreSql;

public sealed class AspireFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private string? _serverConnectionString;
    private string? _databaseName;

    public string ServerConnectionString { get; private set; } = null!;
    public string ConnectionString { get; private set; } = null!;

    /// <summary>
    /// Pool of reusable databases shared across all test classes in this collection.
    /// </summary>
    internal PostgreSqlDatabasePool Pool { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        Environment.SetEnvironmentVariable("TESTAPPHOST_RESOURCES", "postgresql");
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.TestAppHost>(ct);
        _app = await builder.BuildAsync(ct);
        await _app.StartAsync(ct);

        // WaitForResourceHealthyAsync can hang in CI when the persistent container
        // (started by warmup) is already healthy before the test app subscribes to
        // notifications. Use a timeout and fall through — the container is ready.
        using (var healthCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            healthCts.CancelAfter(TimeSpan.FromSeconds(60));
            try
            {
                _ = await _app.ResourceNotifications.WaitForResourceHealthyAsync("postgresql", healthCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout waiting for health notification — container is likely already healthy from warmup.
                Console.WriteLine("WaitForResourceHealthyAsync timed out; proceeding with connection string.");
            }
        }

        _serverConnectionString = (await _app.GetConnectionStringAsync("postgresql", ct))!;
        ServerConnectionString = _serverConnectionString;

        Pool = new PostgreSqlDatabasePool(_serverConnectionString);

        // Create a dedicated database for PostgreSqlStoreTests (smoke tests that
        // need a persistent connection string rather than a pooled one).
        _databaseName = $"test_{Guid.NewGuid():N}";
        await using var dataSource = NpgsqlDataSource.Create(_serverConnectionString);
        await using var cmd = dataSource.CreateCommand($"CREATE DATABASE \"{_databaseName}\"");
        _ = await cmd.ExecuteNonQueryAsync(ct);

        var csb = new NpgsqlConnectionStringBuilder(_serverConnectionString)
        {
            Database = _databaseName
        };
        ConnectionString = csb.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app != null && _serverConnectionString != null && _databaseName != null)
        {
            // Drop all pooled databases.
            if (Pool != null)
            {
                await Pool.DropAllAsync();
            }

            // Drop the dedicated smoke-test database.
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var ct = cts.Token;
                await using var dataSource = NpgsqlDataSource.Create(_serverConnectionString);
                await using var terminateCmd = dataSource.CreateCommand(
                    """
                    SELECT pg_terminate_backend(pid) FROM pg_stat_activity
                    WHERE datname = $1 AND pid <> pg_backend_pid()
                    """);
                _ = terminateCmd.Parameters.Add(new NpgsqlParameter { Value = _databaseName });
                _ = await terminateCmd.ExecuteNonQueryAsync(ct);
                await using var dropCmd = dataSource.CreateCommand($"DROP DATABASE \"{_databaseName}\"");
                _ = await dropCmd.ExecuteNonQueryAsync(ct);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Console.WriteLine($"Failed to drop test database {_databaseName}: {ex.Message}");
            }

            await _app.DisposeAsync();
        }
    }
}
