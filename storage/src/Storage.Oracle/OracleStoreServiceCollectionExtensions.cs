// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Oracle.Internal;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.Storage.Oracle;

public static class OracleStoreServiceCollectionExtensions
{
    extension(IStorageBuilder builder)
    {
        /// <summary>
        /// Adds an Oracle store with the specified service key for multi-store scenarios.
        /// The caller must register a keyed <see cref="CreateOracleConnection"/> with the same service key.
        /// </summary>
        internal IStorageBuilder AddOracleStore(object serviceKey, Action<OracleStoreOptions> configure)
        {
            var services = builder.Services;
            _ = services.AddStore<OracleStore>(serviceKey);
            _ = services.AddKeyedTransient<OracleStore>(serviceKey, (sp, _) =>
            {
                var createConnection = sp.GetRequiredKeyedService<CreateOracleConnection>(serviceKey);
                var outboxSubscribers = sp.GetRequiredKeyedService<OutboxSubscribers>(serviceKey);
                return BuildStore(sp, createConnection, outboxSubscribers, configure);
            });
            return builder;
        }

        /// <summary>
        /// Adds an Oracle store without a service key for single-store scenarios.
        /// The caller must register an unkeyed <see cref="CreateOracleConnection"/>.
        /// </summary>
        public IStorageBuilder AddOracleStore(Action<OracleStoreOptions> configure)
        {
            var services = builder.Services;
            _ = services.AddStore<OracleStore>();
            _ = services.AddTransient<OracleStore>(sp =>
            {
                var createConnection = sp.GetRequiredService<CreateOracleConnection>();
                var outboxSubscribers = sp.GetRequiredService<OutboxSubscribers>();
                return BuildStore(sp, createConnection, outboxSubscribers, configure);
            });
            return builder;
        }
    }

    private static OracleStore BuildStore(
        IServiceProvider sp,
        CreateOracleConnection createConnection,
        OutboxSubscribers outboxSubscribers,
        Action<OracleStoreOptions> configure)
    {
        var options = new OracleStoreOptions();
        configure(options);
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        return new OracleStore(
            createConnection,
            options,
            sp.GetRequiredService<DataStorageTypeRegistry>(),
            sp.GetRequiredService<TimeProvider>(),
            outboxSubscribers,
            sp.GetRequiredService<ILogger<OracleStore>>());
    }
}
