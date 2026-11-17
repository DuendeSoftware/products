// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Data.Sqlite;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal.Storage;

/// <summary>
/// One SQLite database in a topology's storage layout (design doc <c>ITestDatabase</c>, D19): a file shared by every
/// node of the topology. There are no database servers.
/// </summary>
internal sealed class TestDatabase
{
    public TestDatabase(string name, string directory)
    {
        Name = name;
        FilePath = Path.Combine(directory, $"{name}.db");
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            // Both nodes write to the same file; wait for a lock rather than fail.
            DefaultTimeout = 30,
        }.ToString();
    }

    /// <summary>For example <c>configuration</c> or <c>operational</c>.</summary>
    public string Name { get; }

    public string FilePath { get; }

    public string ConnectionString { get; }
}

/// <summary>The directory a topology's database files live in, deleted when the topology is disposed.</summary>
internal sealed class TestDatabaseDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "outside-in", Guid.NewGuid().ToString("N"));

    public TestDatabaseDirectory() => Directory.CreateDirectory(Path);

    public TestDatabase Database(string name) => new(name, Path);

    public void Dispose()
    {
        // Pooled connections keep the files open.
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A connection that's still closing can hold a file for a moment. The files are in the temp directory.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
