// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.PostgreSql.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Duende.Storage.PostgreSql;

public static class PostgreSqlStoreServiceCollectionExtensions
{
    extension(IStorageBuilder builder)
    {
        /// <summary>
        /// Adds a PostgreSQL store with the specified service key for multi-store scenarios.
        /// The caller must register a keyed <see cref="NpgsqlDataSource"/> with the same service key.
        /// </summary>
        internal IStorageBuilder AddPostgreSqlStore(string serviceKey, Action<PostgreSqlStoreOptions> configure)
        {
            var services = builder.Services;
            _ = services.AddStore<PostgreSqlStore>(serviceKey);
            _ = services.AddKeyedTransient<PostgreSqlStore>(serviceKey, (sp, _) =>
            {
                var dataSource = sp.GetRequiredKeyedService<NpgsqlDataSource>(serviceKey);
                var outboxSubscribers = sp.GetRequiredKeyedService<OutboxSubscribers>(serviceKey);
                return BuildStore(sp, dataSource, outboxSubscribers, configure);
            });
            return builder;
        }

        /// <summary>
        /// Adds a PostgreSQL store without a service key for single-store scenarios.
        /// The caller must register an unkeyed <see cref="NpgsqlDataSource"/>.
        /// </summary>
        public IStorageBuilder AddPostgreSqlStore() => builder.AddPostgreSqlStore(_ => { });

        /// <summary>
        /// Adds a PostgreSQL store without a service key for single-store scenarios.
        /// The caller must register an unkeyed <see cref="NpgsqlDataSource"/>.
        /// </summary>
        public IStorageBuilder AddPostgreSqlStore(Action<PostgreSqlStoreOptions> configure)
        {
            var services = builder.Services;
            _ = services.AddStore<PostgreSqlStore>();
            _ = services.AddTransient<PostgreSqlStore>(sp =>
            {
                var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
                var outboxSubscribers = sp.GetRequiredService<OutboxSubscribers>();
                return BuildStore(sp, dataSource, outboxSubscribers, configure);
            });
            return builder;
        }
    }

    private static PostgreSqlStore BuildStore(
        IServiceProvider sp,
        NpgsqlDataSource dataSource,
        OutboxSubscribers outboxSubscribers,
        Action<PostgreSqlStoreOptions> configure)
    {
        var options = new PostgreSqlStoreOptions();
        configure(options);
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        return new PostgreSqlStore(
            dataSource,
            options,
            sp.GetRequiredService<DataStorageTypeRegistry>(),
            sp.GetRequiredService<TimeProvider>(),
            outboxSubscribers,
            sp.GetRequiredService<ILogger<PostgreSqlStore>>());
    }
}
