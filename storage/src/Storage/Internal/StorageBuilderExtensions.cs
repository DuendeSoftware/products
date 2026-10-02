// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.Storage.Internal;

/// <summary>
/// Extension methods for configuring storage services.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public static class StorageBuilderExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Configures the default storage instance using the specified callback.
        /// </summary>
        /// <remarks>
        /// This method is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
        /// </remarks>
        public IServiceCollection AddStorageInternal(Action<IStorageBuilder> configure) =>
            services.AddStorageInternal(StorageInstanceId.Default, configure);

        /// <summary>
        /// Configures a specific storage instance (note, this can also be the default) using the given storage builder.
        /// </summary>
        /// <remarks>
        /// This method is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
        /// </remarks>
        public IServiceCollection AddStorageInternal(StorageInstanceId storageInstanceId, Action<IStorageBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            var builder = new StorageInstanceBuilder(services, storageInstanceId);
            services.TryAddTransient<IPartitionedStorageFactory, DefaultPartitionedStorageFactory>();
            services.TryAddTransient<ICrossPartitionStorageFactory, CrossPartitionStorageFactory>();
            services.TryAddTransient<IStorageInstanceSchemaFactory, DefaultStorageInstanceSchemaFactory>();

            services.TryAddKeyedSingleton<IStorageInstanceSchema>(StorageInstanceId.Default,
                (_, __) => throw new InvalidOperationException(
                    "No database provider is registered for the default storage instance. Call a provider registration " +
                    "method such as AddSqlite(), AddPostgreSql(), AddMsSql(), or AddOracle() for it."));

            services.TryAddTransient<IStorageInstanceSchema>(sp => sp.GetRequiredKeyedService<IStorageInstanceSchema>(StorageInstanceId.Default));

            // The default in-memory schema store, built from every registered SchemaConfiguration; for the same id, the
            // last registered wins. AddDynamicSchemas() replaces this store by appending its own ISchemaStore registration,
            // which wins over this TryAdd default.
            services.TryAddSingleton<ISchemaStore, InMemorySchemaStore>();

            // Calling the get or add to ensure it's registered.
            _ = services.GetOrAddStorageInstanceRouter();

            configure(builder);
            return services;
        }

    }

    private sealed class StorageInstanceBuilder(IServiceCollection services, StorageInstanceId storageInstanceId) : IStorageBuilder
    {
        public IServiceCollection Services { get; } = services;
        public StorageInstanceId StorageInstanceId { get; } = storageInstanceId;
    }
}
