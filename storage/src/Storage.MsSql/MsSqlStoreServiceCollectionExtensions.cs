// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.MsSql.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.Storage.MsSql;

public static class MsSqlStoreServiceCollectionExtensions
{
    extension(IStorageBuilder builder)
    {
        /// <summary>
        /// Adds a SQL Server store with the specified service key for multi-store scenarios.
        /// The caller must register a keyed <see cref="CreateSqlConnection"/> with the same service key.
        /// </summary>
        internal IStorageBuilder AddMsSqlStore(object serviceKey, Action<MsSqlStoreOptions> configure)
        {
            var services = builder.Services;
            _ = services.AddStore<MsSqlStore>(serviceKey);
            _ = services.AddKeyedTransient<MsSqlStore>(serviceKey, (sp, _) =>
            {
                var createConnection = sp.GetRequiredKeyedService<CreateSqlConnection>(serviceKey);
                var outboxSubscribers = sp.GetRequiredKeyedService<OutboxSubscribers>(serviceKey);
                return BuildStore(sp, createConnection, outboxSubscribers, configure);
            });
            return builder;
        }

        /// <summary>
        /// Adds a SQL Server store without a service key for single-store scenarios.
        /// The caller must register an unkeyed <see cref="CreateSqlConnection"/>.
        /// </summary>
        public IStorageBuilder AddMsSqlStore(Action<MsSqlStoreOptions> configure)
        {
            var services = builder.Services;
            _ = services.AddStore<MsSqlStore>();
            _ = services.AddTransient<MsSqlStore>(sp =>
            {
                var createConnection = sp.GetRequiredService<CreateSqlConnection>();
                var outboxSubscribers = sp.GetRequiredService<OutboxSubscribers>();
                return BuildStore(sp, createConnection, outboxSubscribers, configure);
            });
            return builder;
        }
    }

    private static MsSqlStore BuildStore(
        IServiceProvider sp,
        CreateSqlConnection createConnection,
        OutboxSubscribers outboxSubscribers,
        Action<MsSqlStoreOptions> configure)
    {
        var options = new MsSqlStoreOptions();
        configure(options);
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        return new MsSqlStore(
            createConnection,
            options,
            sp.GetRequiredService<DataStorageTypeRegistry>(),
            sp.GetRequiredService<TimeProvider>(),
            outboxSubscribers,
            sp.GetRequiredService<ILogger<MsSqlStore>>());
    }
}
