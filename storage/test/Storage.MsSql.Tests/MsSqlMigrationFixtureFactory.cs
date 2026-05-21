// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.MsSql;

internal sealed class MsSqlMigrationFixtureFactory(AspireFixture aspire) : IMigrationFixtureFactory
{
    public async Task<IMigrationFixture> CreateAsync(CancellationToken ct)
    {
        var connectionString = await aspire.Pool.GetConnectionStringAsync(ct);
        var schemaName = "s_" + DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture);

        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddKeyedSingleton<CreateSqlConnection>("migration-test", () => new SqlConnection(connectionString));
        _ = services.AddStorageInternal(storage => storage.AddMsSqlStore("migration-test", o => o.SchemaName = schemaName));
        var provider = services.BuildServiceProvider();

        var schema = provider.GetRequiredKeyedService<IDatabaseSchema>("migration-test");
        return new MsSqlMigrationFixture(provider, schemaName, schema, connectionString);
    }
}

internal sealed class MsSqlMigrationFixture(
    ServiceProvider provider,
    string schemaName,
    IDatabaseSchema schema,
    string connectionString) : IMigrationFixture
{
    public IDatabaseSchema Schema => schema;

    public async Task ExecuteSqlAsync(string sql, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await provider.DisposeAsync();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            DECLARE @sql NVARCHAR(MAX) = N'';

            -- Drop all foreign keys
            SELECT @sql += 'ALTER TABLE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ' DROP CONSTRAINT ' + QUOTENAME(fk.name) + ';' + CHAR(10)
            FROM sys.foreign_keys fk
            INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = N'{schemaName}';
            EXEC sp_executesql @sql;

            -- Drop all types
            SET @sql = N'';
            SELECT @sql += 'DROP TYPE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ';' + CHAR(10)
            FROM sys.types t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = N'{schemaName}' AND t.is_user_defined = 1;
            EXEC sp_executesql @sql;

            -- Drop all tables
            SET @sql = N'';
            SELECT @sql += 'DROP TABLE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ';' + CHAR(10)
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = N'{schemaName}';
            EXEC sp_executesql @sql;

            -- Drop extended properties on the schema
            IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 3 AND major_id = SCHEMA_ID(N'{schemaName}'))
                EXEC sys.sp_dropextendedproperty @name = N'SchemaVersion', @level0type = N'SCHEMA', @level0name = N'{schemaName}';

            -- Drop the schema
            DROP SCHEMA IF EXISTS [{schemaName}];
            """;
        _ = await cmd.ExecuteNonQueryAsync();
    }
}
