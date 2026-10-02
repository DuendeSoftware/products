// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.PostgreSql;

internal sealed class PostgreSqlMigrationFixtureFactory(AspireFixture aspire) : IMigrationFixtureFactory
{
    public async Task<IMigrationFixture> CreateAsync(CancellationToken ct)
    {
        var db = aspire.DatabaseProvisioner.Provision();
        await db.CreateAsync(ct);
        var schemaName = "s_" + DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture);
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddStorageInternal(storage => storage.AddPostgreSql(
            _ => db.DataSource,
            o => o.SchemaName = schemaName));
        var provider = services.BuildServiceProvider();

        var storageInstanceSchema = await provider.GetRequiredService<IStorageInstanceSchemaFactory>().GetStorageInstanceSchema(ct);
        return new PostgreSqlMigrationFixture(provider, schemaName, storageInstanceSchema, db);
    }
}

internal sealed class PostgreSqlMigrationFixture(
    ServiceProvider provider,
    string schemaName,
    IStorageInstanceSchema storageInstanceSchema,
    ProvisionedTestDatabase db) : IMigrationFixture
{
    public uint RequiredVersion => 2u;
    public IStorageInstanceSchema StorageInstanceSchema => storageInstanceSchema;

    public async Task ExecuteSqlAsync(string sql, CancellationToken ct)
    {
        await using var cmd = db.DataSource.CreateCommand(sql);
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await provider.DisposeAsync();

        var dropCommand = db.DataSource.CreateCommand("DROP SCHEMA IF EXISTS \"" + schemaName + "\" CASCADE");
        _ = await dropCommand.ExecuteNonQueryAsync();

        // Intentionally not released back to the provisioner's pool: the database
        // stays checked out until DropAllAsync cleans it up at suite teardown,
        // matching the original migration fixture behaviour. Only the owned
        // NpgsqlDataSource is disposed here.
        await db.DataSource.DisposeAsync();
    }
}
