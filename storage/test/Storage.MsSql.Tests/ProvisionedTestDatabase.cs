// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Data.SqlClient;

namespace Duende.Storage.MsSql;

/// <summary>
/// A SQL Server database reserved for a single storage fixture. The database is
/// created lazily the first time it is used and released back to the shared
/// <see cref="MsSqlTestDatabaseProvisioner"/> when disposed.
/// </summary>
internal sealed class ProvisionedTestDatabase(
    MsSqlTestDatabaseProvisioner provisioner,
    string connectionString,
    bool exists) : IAsyncDisposable
{
    private bool _created = exists;

    public string ConnectionString { get; } = connectionString;

    public CreateSqlConnection Connect => () => new SqlConnection(ConnectionString);

    public async Task CreateAsync(CancellationToken ct)
    {
        if (!_created)
        {
            await provisioner.CreateAsync(ConnectionString, ct);
            _created = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_created)
        {
            await provisioner.ReleaseAsync(ConnectionString);
        }
    }
}
