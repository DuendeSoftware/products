// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Hosting.Coordination;

/// <summary>
/// Builds several <see cref="ServiceProvider"/> instances ("nodes") that share one temp-file
/// SQLite database, a shared data-protection key ring directory, and a single
/// <see cref="FakeTimeProvider"/>, so multi-node tests can exercise real hosted services across
/// more than one process-equivalent without any of them sharing in-process state.
/// </summary>
/// <remarks>
/// Uses a temp-file database rather than <c>Mode=Memory;Cache=Shared</c>: shared-cache in-memory
/// SQLite raises <c>SQLITE_LOCKED</c> under concurrent writers from separate connections, which a
/// multi-node test relies on. Every node's <see cref="ServiceProvider"/> is kept alive until
/// <see cref="DisposeAsync"/>, which disposes them all and then deletes the database file and key
/// ring directory.
/// </remarks>
internal sealed class MultiNodeTestCluster : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly List<ServiceProvider> _providers = [];

    private MultiNodeTestCluster(string dbPath, string keyRingDirectory)
    {
        _dbPath = dbPath;
        ConnectionString = $"Data Source={dbPath}";
        KeyRingDirectory = keyRingDirectory;
        Directory.CreateDirectory(keyRingDirectory);
    }

    /// <summary>
    /// A single <see cref="FakeTimeProvider"/> shared by every node, so a test can advance time
    /// for the whole cluster at once.
    /// </summary>
    public FakeTimeProvider TimeProvider { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>The shared SQLite connection string every node's storage provider must use.</summary>
    public string ConnectionString { get; }

    /// <summary>A directory every node's data protection key ring should be persisted to, so keys protected by one node can be read by another.</summary>
    public string KeyRingDirectory { get; }

    /// <summary>
    /// Creates a cluster backed by a new temp-file SQLite database and a new key ring directory,
    /// both unique to this cluster instance.
    /// </summary>
    public static MultiNodeTestCluster Create()
    {
        var token = Guid.NewGuid().ToString("N");
        return new MultiNodeTestCluster(
            Path.Combine(Path.GetTempPath(), $"multi-node-{token}.db"),
            Path.Combine(Path.GetTempPath(), $"multi-node-{token}-keys"));
    }

    /// <summary>
    /// Builds one node: a fresh <see cref="ServiceCollection"/> with logging and the shared
    /// <see cref="TimeProvider"/> already registered, configured further by <paramref name="configure"/>,
    /// then built into a <see cref="ServiceProvider"/> that this cluster keeps alive until disposed.
    /// </summary>
    public ServiceProvider AddNode(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(TimeProvider);

        configure(services);

        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return provider;
    }

    /// <summary>
    /// Migrates the shared database schema once, using the first node added. Safe to call even
    /// though every node's own <see cref="IStorageInstanceSchema"/> targets the same database file.
    /// </summary>
    public async Task MigrateAsync(Ct ct)
    {
        await using var scope = _providers[0].CreateAsyncScope();
        var schema = scope.ServiceProvider.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var provider in _providers)
        {
            await provider.DisposeAsync();
        }

        // Microsoft.Data.Sqlite pools its native connections, so a connection used by a disposed
        // provider can still hold the file open for a moment after DisposeAsync returns. Clear the
        // pool first so the delete below doesn't race it.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        DeleteIfExists(_dbPath);
        DeleteIfExists($"{_dbPath}-wal");
        DeleteIfExists($"{_dbPath}-shm");
        DeleteIfExists($"{_dbPath}-journal");

        if (Directory.Exists(KeyRingDirectory))
        {
            Directory.Delete(KeyRingDirectory, recursive: true);
        }
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup: a lingering handle must not fail the test that already ran.
        }
    }
}
