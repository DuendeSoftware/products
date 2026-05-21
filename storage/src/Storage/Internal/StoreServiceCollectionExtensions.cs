// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.Storage.Internal;

internal static class StoreServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers store services with a specific service key for multi-store scenarios.
        /// </summary>
        internal IServiceCollection AddStore<TStoreBase>(object serviceKey)
            where TStoreBase : IStore, IDatabaseSchema
        {
            services.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);
            services.TryAddSingleton<DataStorageTypeRegistry>();
            _ = services.AddKeyedTransient<IPooledStore>(serviceKey,
                (provider, _) => new PooledStore(provider, serviceKey));
            _ = services.AddKeyedSingleton<OutboxSubscribers>(serviceKey);

            _ = services.AddKeyedTransient<IStore>(serviceKey,
                (sp, _) => sp.GetRequiredKeyedService<TStoreBase>(serviceKey));
            _ = services.AddKeyedTransient<IDatabaseSchema>(serviceKey,
                (sp, _) => sp.GetRequiredKeyedService<TStoreBase>(serviceKey));
            return services;
        }

        /// <summary>
        /// Registers store services without a service key for single-store scenarios.
        /// </summary>
        internal IServiceCollection AddStore<TStoreBase>()
            where TStoreBase : IStore, IDatabaseSchema
        {
            services.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);
            services.TryAddSingleton<DataStorageTypeRegistry>();
            _ = services.AddTransient<IPooledStore>(provider =>
                new PooledStore(provider, null));
            _ = services.AddSingleton<OutboxSubscribers>();

            _ = services.AddTransient<IStore>(sp =>
                sp.GetRequiredService<TStoreBase>());
            _ = services.AddTransient<IDatabaseSchema>(sp =>
                sp.GetRequiredService<TStoreBase>());
            return services;
        }
    }
}
