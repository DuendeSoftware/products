// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Duende.Storage.PostgreSql;

internal sealed class PostgreSqlMigrationFixtureFactory(AspireFixture aspire) : IMigrationFixtureFactory
{
    public async Task<IMigrationFixture> CreateAsync(CancellationToken ct)
    {
        var connectionString = await aspire.Pool.GetConnectionStringAsync(ct);
        var schemaName = "s_" + DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture);
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddNpgsqlDataSource(connectionString, serviceKey: "migration-test");
        _ = services.AddStorageInternal(storage => storage.AddPostgreSqlStore("migration-test", o => o.SchemaName = schemaName));
        var provider = services.BuildServiceProvider();

        var schema = provider.GetRequiredKeyedService<IDatabaseSchema>("migration-test");
        return new PostgreSqlMigrationFixture(provider, schemaName, schema, connectionString);
    }
}

internal sealed class PostgreSqlMigrationFixture(
    ServiceProvider provider,
    string schemaName,
    IDatabaseSchema schema,
    string connectionString) : IMigrationFixture
{
    private NpgsqlDataSource _dataSource = NpgsqlDataSource.Create(connectionString);
    public IDatabaseSchema Schema => schema;

    public async Task ExecuteSqlAsync(string sql, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand(sql);
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await provider.DisposeAsync();

        var dropCommand = _dataSource.CreateCommand("DROP SCHEMA IF EXISTS \"" + schemaName + "\" CASCADE");
        _ = await dropCommand.ExecuteNonQueryAsync();

        await _dataSource.DisposeAsync();
    }
}
