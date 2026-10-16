// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Private.Licencing.V2;
using Duende.Spaces.Internal.DataProtection;
using Duende.Spaces.Internal.Licensing;
using Duende.Spaces.Internal.Storage;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Outbox;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.Spaces.Internal;

/// <summary>
/// Extension methods for registering Spaces services with an <see cref="IServiceCollection"/>.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public static class SpacesServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds Spaces services to the service collection, storing Spaces data in the default storage
        /// instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
        /// </para>
        /// <para>
        /// Registers a transient <see cref="IPartitionedStorageFactory"/> that routes storage operations to the
        /// pool of the current space. Duende.Storage registers its default factory with
        /// <c>TryAddTransient</c>, so this registration wins regardless of the order in which storage
        /// and Spaces are added.
        /// </para>
        /// </remarks>
        public IServiceCollection AddSpacesInternal() =>
            services.AddSpacesInternal(StorageInstanceId.Default);

        /// <summary>
        /// Adds Spaces services to the service collection, storing Spaces data in the given
        /// <paramref name="storageInstanceId"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
        /// </para>
        /// <para>
        /// Registers a transient <see cref="IPartitionedStorageFactory"/> that routes storage operations to the
        /// pool of the current space. Duende.Storage registers its default factory with
        /// <c>TryAddTransient</c>, so this registration wins regardless of the order in which storage
        /// and Spaces are added.
        /// </para>
        /// </remarks>
        /// <param name="storageInstanceId">The storage instance to route Spaces category storage to.</param>
        public IServiceCollection AddSpacesInternal(StorageInstanceId storageInstanceId)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.GetOrAddStorageInstanceRouter().AddMapping(DataCategoryName.Spaces, storageInstanceId);

            _ = services.AddTransient<IPartitionedStorageFactory, SpacesPartitionedStorageFactory>();
            services.TryAddTransient<DefaultPartitionedStorageFactory>();

            services.TryAddSingleton<ISpaceContextAccessor, SpaceContextAccessor>();

            services.TryAddTransient<ManagementStorageAccessor>();

            services.TryAddTransient<SpaceRepository>();
            services.TryAddTransient<SpaceRouting>();

            // Space resolution infrastructure
            services.TryAddTransient<ISpaceStore, SpaceStore>();
            services.TryAddTransient<ISpacePathRewriter, DefaultSpacePathRewriter>();

            _ = services.AddTransient<IAmbientOutboxProcessingContextProvider,
                SpaceAwareAmbientOutboxProcessingContext>();

            services.TryAddTransient<ISpaceAdmin, SpaceAdmin>();

            // Keyed HybridCache for space resolution caching. Spaces uses its own keyed instance so apps
            // can supply a dedicated cache without Spaces registering or sharing the app's default one.
            if (!services.Any(d =>
                    d.ServiceType == typeof(HybridCache) &&
                    d.IsKeyedService &&
                    SpacesServiceProviderKeys.SpacesCache.Equals(d.ServiceKey)))
            {
                _ = services.AddKeyedHybridCache(SpacesServiceProviderKeys.SpacesCache);
            }

            // Replace (not TryAdd) so this wins over IdentityServer's default factory regardless of registration order.
            _ = services.Replace(ServiceDescriptor.Transient<IHybridCacheFactory, SpaceAwareHybridCacheFactory>());

            // Register the SpaceDso type for deserialization
            services.AddDsoRegistration<SpaceDso.V1>();

            // Options
            _ = services.AddOptions<SpacesOptions>();

            // Licensing
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<LicenseValidator>();
            services.TryAddSingleton<SpacesLicenseValidator>();

            // Data protection partitioning
            SpacesDataProtectionRegistration.Register(services);

            return services;
        }
    }
}
