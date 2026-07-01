// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.MsSql;
using Duende.Storage.PostgreSql;
using Duende.Storage.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Duende.Storage.CliPlugin.Commands;

internal static class DatabaseProviderFactory
{
    internal static ServiceProvider CreateServiceProvider(string provider, string connectionString, string? schemaName)
    {
        var services = new ServiceCollection();

        _ = services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        _ = services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        switch (provider)
        {
            case "postgresql":
                _ = services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
                _ = services.AddStorageInternal(storage => storage.AddPostgreSqlStore(options =>
                {
                    if (schemaName is not null)
                    {
                        options.SchemaName = schemaName;
                    }
                }));
                break;

            case "mssql":
                _ = services.AddSingleton<CreateSqlConnection>(() => new SqlConnection(connectionString));
                _ = services.AddStorageInternal(storage => storage.AddMsSqlStore(options =>
                {
                    if (schemaName is not null)
                    {
                        options.SchemaName = schemaName;
                    }
                }));
                break;

            case "sqlite":
                _ = services.AddStorageInternal(storage => storage.AddSqliteStore(options => options.ConnectionString = connectionString));
                break;

            default:
                throw new ArgumentException($"Unsupported database provider '{provider}'.", nameof(provider));
        }

        return services.BuildServiceProvider();
    }
}
