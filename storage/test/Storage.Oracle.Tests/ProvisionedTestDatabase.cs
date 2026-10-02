// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Oracle.ManagedDataAccess.Client;

namespace Duende.Storage.Oracle;

/// <summary>
/// An Oracle user (schema) reserved for a single storage fixture. The user is
/// created lazily the first time it is used and released back to the shared
/// <see cref="OracleTestDatabaseProvisioner"/> when disposed.
/// </summary>
internal sealed class ProvisionedTestDatabase(OracleTestDatabaseProvisioner provisioner, string connectionString, bool exists) : IAsyncDisposable
{
    private bool _created = exists;

    public string ConnectionString { get; } = connectionString;

    public CreateOracleConnection Connect => () => new OracleConnection(ConnectionString);

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
