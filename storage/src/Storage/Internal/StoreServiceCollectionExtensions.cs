// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Schema;
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
        internal IServiceCollection AddStore<TStoreBase>(StorageInstanceId storageInstanceId)
            where TStoreBase : class, IPartitionedStorage, IStorageInstanceSchema, ICrossPartitionStorage
        {
            services.GetOrAddStorageInstanceRouter().RegisterInstance(storageInstanceId);

            services.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);
            services.TryAddSingleton<DataStorageTypeRegistry>();
            // Options pattern: ConfigureOutboxProcessor() callers and the host supplying the
            // storage key each contribute an IConfigureOptions, so they compose regardless of
            // registration order. OutboxProcessor consumes IOptions<T> directly.
            _ = services.AddOptions<OutboxProcessorOptions>();
            services.TryAddTransient<IAmbientOutboxProcessingContextProvider, DefaultAmbientOutboxProcessingContextProvider>();

            _ = services.AddKeyedSingleton<OutboxSubscriptions>(storageInstanceId);
            services.TryAddSingleton<OutboxProcessor>();

            _ = services.AddKeyedTransient<IPartitionedStorage>(storageInstanceId,
                (sp, _) => sp.GetRequiredKeyedService<TStoreBase>(storageInstanceId));
            _ = services.AddKeyedTransient<IStorageInstanceSchema>(storageInstanceId,
                (sp, _) => sp.GetRequiredKeyedService<TStoreBase>(storageInstanceId));
            _ = services.AddKeyedTransient<ICrossPartitionStorage>
                (storageInstanceId, (sp, _) => sp.GetRequiredKeyedService<TStoreBase>(storageInstanceId));
            return services;
        }
    }
}
