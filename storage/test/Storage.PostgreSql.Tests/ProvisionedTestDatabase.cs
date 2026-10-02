// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Npgsql;

namespace Duende.Storage.PostgreSql;

/// <summary>
/// A PostgreSQL database reserved for a single storage fixture. The database is
/// created lazily the first time it is used and released back to the shared
/// <see cref="PostgreSqlTestDatabaseProvisioner"/> when disposed.
/// </summary>
internal sealed class ProvisionedTestDatabase(
    PostgreSqlTestDatabaseProvisioner provisioner,
    string connectionString,
    bool exists)
    : IAsyncDisposable
{
    private bool _created = exists;

    public string ConnectionString { get; } = connectionString;
    public NpgsqlDataSource DataSource { get; } = NpgsqlDataSource.Create(connectionString);

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
        await DataSource.DisposeAsync();
        if (_created)
        {
            await provisioner.ReleaseAsync(ConnectionString);
        }
    }
}
