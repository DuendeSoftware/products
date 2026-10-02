// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.Storage.Sqlite;

public static class SqliteStorageEngineServiceCollectionExtensions
{
    extension(IStorageBuilder builder)
    {
        /// <summary>
        /// Adds a SQLite store for the builder's storage instance, using the connection string set in
        /// <paramref name="configure"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a database provider has already been selected for the builder's storage instance.
        /// </exception>
        public IStorageBuilder AddSqlite(Action<SqliteStorageEngineOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            ArgumentNullException.ThrowIfNull(builder);

            builder.MarkProviderSelected("SQLite");

            var services = builder.Services;
            var options = BuildOptions(configure);
            _ = services.AddStore<SqliteStorageEngine>(builder.StorageInstanceId);
            _ = services.AddKeyedSingleton<SqliteConnection>(builder.StorageInstanceId, (_, _) =>
            {
                var connection = new SqliteConnection(options.ConnectionString);
                connection.Open();
                return connection;
            });
            _ = services.AddKeyedTransient<SqliteStorageEngine>(builder.StorageInstanceId, (sp, _) =>
            {
                // Ensure the keep-alive connection exists (required for in-memory databases)
                _ = sp.GetRequiredKeyedService<SqliteConnection>(builder.StorageInstanceId);
                var outboxSubscriptions = sp.GetRequiredKeyedService<OutboxSubscriptions>(builder.StorageInstanceId);
                return BuildStore(sp, outboxSubscriptions, options);
            });
            return builder;
        }


        /// <summary>
        /// Adds a SQLite in-memory store intended for testing only.
        /// Uses a shared in-memory database with a generated unique name.
        /// This method is NOT intended for production use.
        /// </summary>
        public IStorageBuilder AddSqliteInMemory() =>
            builder.AddSqliteInMemory($"InMemoryDb_{Guid.NewGuid():N}");

        /// <summary>
        /// Adds a SQLite in-memory store intended for testing only.
        /// Uses a shared in-memory database with the specified data source name,
        /// allowing multiple connections (and tests) to share the same database.
        /// This method is NOT intended for production use.
        /// </summary>
        /// <param name="dataSourceName">
        /// The data source name for the shared in-memory database.
        /// Use the same name across tests to share state.
        /// </param>
        public IStorageBuilder AddSqliteInMemory(string dataSourceName) =>
            builder.AddSqlite(opt => opt.ConnectionString = $"Data Source={dataSourceName};Mode=Memory;Cache=Shared");
    }

    private static SqliteStorageEngineOptions BuildOptions(Action<SqliteStorageEngineOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new SqliteStorageEngineOptions();
        configure(options);
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        return options;
    }

    private static SqliteStorageEngine BuildStore(
        IServiceProvider sp,
        OutboxSubscriptions outboxSubscriptions,
        SqliteStorageEngineOptions options) =>
        new(
            options,
            sp.GetRequiredService<DataStorageTypeRegistry>(),
            sp.GetRequiredService<TimeProvider>(),
            outboxSubscriptions,
            sp.GetRequiredService<ILogger<SqliteStorageEngine>>());
}
