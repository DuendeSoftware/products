// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;

namespace Duende.UserManagement;

/// <summary>
/// SQLite-specific test database provider.
/// SQLite does not need an Aspire resource — each "schema" maps to a separate temp database file.
/// Cleanup involves deleting the temp files on dispose.
/// </summary>
public sealed class SqliteTestDatabaseProvider : ITestDatabaseProvider
{
    private readonly ConcurrentDictionary<string, string> _createdDatabases = new();
    private readonly string _basePath = Path.Combine(Path.GetTempPath(), "duende-sqlite-tests");

    /// <inheritdoc />
    public string ResourceName => "sqlite";

    /// <inheritdoc />
    public string GetNCrunchConnectionString() =>
        CreateConnectionString(Guid.NewGuid().ToString("N")[..8]);

    /// <inheritdoc />
    public Task<string> GetConnectionStringAsync(Func<string, Ct, Task<string>> getConnectionString, Ct ct)
    {
        // SQLite doesn't need Aspire — just create a local database file.
        // Return a base connection string; each schema will get its own file.
        var connectionString = CreateConnectionString(Guid.NewGuid().ToString("N")[..8]);
        return Task.FromResult(connectionString);
    }

    /// <inheritdoc />
    public void RegisterSchemaName(string schemaName)
    {
        var dbPath = GetDatabasePath(schemaName);
        _ = _createdDatabases.TryAdd(schemaName, dbPath);
    }

    /// <inheritdoc />
    public Task DropAllSchemasAsync(Ct ct)
    {
        foreach (var (schemaName, dbPath) in _createdDatabases)
        {
            try
            {
                // SQLite may have WAL/SHM journal files alongside the main file
                DeleteFileIfExists(dbPath);
                DeleteFileIfExists(dbPath + "-wal");
                DeleteFileIfExists(dbPath + "-shm");
                DeleteFileIfExists(dbPath + "-journal");
            }
#pragma warning disable CA1031 // Catch more specific exceptions
            catch (Exception ex)
#pragma warning restore CA1031
            {
                // Best effort cleanup - don't fail dispose if file deletion fails
                Console.WriteLine($"Failed to delete SQLite database {schemaName}: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a connection string for a SQLite database file identified by the given name.
    /// </summary>
    public string CreateConnectionString(string name)
    {
        _ = Directory.CreateDirectory(_basePath);
        var dbPath = GetDatabasePath(name);
        return $"Data Source={dbPath}";
    }

    private string GetDatabasePath(string name) =>
        Path.Combine(_basePath, $"{name}.db");

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
