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

public static class SqliteStoreServiceCollectionExtensions
{
    extension(IStorageBuilder builder)
    {
        /// <summary>
        /// Adds a SQLite store with the specified service key for multi-store scenarios.
        /// </summary>
        internal IStorageBuilder AddSqliteStore(object serviceKey, Action<SqliteStoreOptions> configure)
        {
            var services = builder.Services;
            var options = BuildOptions(configure);
            _ = services.AddStore<SqliteStore>(serviceKey);
            _ = services.AddKeyedSingleton<SqliteConnection>(serviceKey, (_, _) =>
            {
                var connection = new SqliteConnection(options.ConnectionString);
                connection.Open();
                return connection;
            });
            _ = services.AddKeyedTransient<SqliteStore>(serviceKey, (sp, _) =>
            {
                // Ensure the keep-alive connection exists (required for in-memory databases)
                _ = sp.GetRequiredKeyedService<SqliteConnection>(serviceKey);
                var outboxSubscribers = sp.GetRequiredKeyedService<OutboxSubscribers>(serviceKey);
                return BuildStore(sp, outboxSubscribers, options);
            });
            return builder;
        }

        /// <summary>
        /// Adds a SQLite store without a service key for single-store scenarios.
        /// </summary>
        public IStorageBuilder AddSqliteStore(Action<SqliteStoreOptions> configure)
        {
            var services = builder.Services;
            var options = BuildOptions(configure);
            _ = services.AddStore<SqliteStore>();
            _ = services.AddSingleton<SqliteConnection>(_ =>
            {
                var connection = new SqliteConnection(options.ConnectionString);
                connection.Open();
                return connection;
            });
            _ = services.AddTransient<SqliteStore>(sp =>
            {
                // Ensure the keep-alive connection exists (required for in-memory databases)
                _ = sp.GetRequiredService<SqliteConnection>();
                var outboxSubscribers = sp.GetRequiredService<OutboxSubscribers>();
                return BuildStore(sp, outboxSubscribers, options);
            });
            return builder;
        }

        /// <summary>
        /// Adds a SQLite in-memory store intended for testing only.
        /// Uses a shared in-memory database with a generated unique name.
        /// This method is NOT intended for production use.
        /// </summary>
        public IStorageBuilder AddSqliteInMemoryStore() =>
            builder.AddSqliteInMemoryStore($"InMemoryDb_{Guid.NewGuid():N}");

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
        public IStorageBuilder AddSqliteInMemoryStore(string dataSourceName) =>
            builder.AddSqliteStore(opt => opt.ConnectionString = $"Data Source={dataSourceName};Mode=Memory;Cache=Shared");
    }

    private static SqliteStoreOptions BuildOptions(Action<SqliteStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new SqliteStoreOptions();
        configure(options);
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        return options;
    }

    private static SqliteStore BuildStore(
        IServiceProvider sp,
        OutboxSubscribers outboxSubscribers,
        SqliteStoreOptions options) =>
        new(
            options,
            sp.GetRequiredService<DataStorageTypeRegistry>(),
            sp.GetRequiredService<TimeProvider>(),
            outboxSubscribers,
            sp.GetRequiredService<ILogger<SqliteStore>>());
}
