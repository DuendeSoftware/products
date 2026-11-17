// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.DataExtensions;
using Duende.IdentityServer.Infrastructure.DistributedLock;
using Duende.IdentityServer.Stores;
using Duende.Private.Licencing.V2;
using Duende.Spaces;
using Duende.Spaces.Internal;
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
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer;

/// <summary>
/// Extension methods for adding Duende Spaces to IdentityServer.
/// </summary>
public static class IdentityServerSpacesBuilderExtensions
{
    extension(IIdentityServerBuilder builder)
    {
        /// <summary>
        /// Adds Spaces to IdentityServer, storing Spaces data in the default database
        /// (<see cref="StorageInstanceId.Default"/>).
        /// </summary>
        /// <remarks>
        /// Equivalent to <c>AddSpaces(StorageInstanceId.Default, configure)</c>. See that overload for details.
        /// </remarks>
        /// <param name="configure">A delegate to configure <see cref="SpacesOptions"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when Spaces data is already mapped to a storage instance.
        /// </exception>
        public IIdentityServerBuilder AddSpaces(Action<SpacesOptions> configure) =>
            builder.AddSpaces(StorageInstanceId.Default, configure);

        /// <summary>
        /// Adds Spaces to IdentityServer, storing Spaces data in the database registered for
        /// <paramref name="storageInstanceId"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Register a database for <paramref name="storageInstanceId"/> with <c>AddStorage</c>. Otherwise the
        /// first Spaces read or write throws an <see cref="InvalidOperationException"/> because the instance has
        /// no database provider.
        /// </para>
        /// <para>
        /// Call <c>UseSpaceResolution()</c> early in the request pipeline so each request runs in its space.
        /// </para>
        /// </remarks>
        /// <param name="storageInstanceId">The storage instance that holds Spaces data.</param>
        /// <param name="configure">A delegate to configure <see cref="SpacesOptions"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when Spaces data is already mapped to a storage instance.
        /// </exception>
        public IIdentityServerBuilder AddSpaces(StorageInstanceId storageInstanceId, Action<SpacesOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            _ = builder.Services.Configure(configure);

            var services = builder.Services;

            services.GetOrAddStorageInstanceRouter().AddMapping(DataCategoryName.Spaces, storageInstanceId);

            _ = services.AddTransient<IPartitionedStorageFactory, SpacesPartitionedStorageFactory>();
            services.TryAddTransient<DefaultPartitionedStorageFactory>();

            services.TryAddSingleton<ISpaceContextAccessor, SpaceContextAccessor>();
            services.TryAddSingleton<ISchemaStore, InMemorySchemaStore>();

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
            services.AddDsoRegistration<SpacePoolIdHighWaterMarkDso.V1>();

            // Options
            _ = services.AddOptions<SpacesOptions>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SpacesOptions>, SpacesOptionsValidator>());

            // Licensing
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<LicenseValidator>();
            services.TryAddSingleton<SpacesLicenseValidator>();

            // Data protection partitioning
            SpacesDataProtectionRegistration.Register(services);

            // The Space schema is stored in the management space. In-memory mode has no ISchemaAdmin to wrap.
            if (services.Any(d => d.ServiceType == typeof(ISchemaAdmin)))
            {
                Duende.Spaces.Internal.DecoratorServiceCollectionExtensions
                    .AddTransientDecorator<ISchemaAdmin, SpaceSchemaAdmin>(services);
            }

            _ = services.AddSingleton<GetSpaceIdOrDefault>(sp => sp.GetRequiredService<ISpaceContextAccessor>().GetSpaceIdOrDefault);

            // Validate SpacesOptions when the host starts, rather than relying on a hosted service happening
            // to read it during startup. Its validators include checks that keep data separate per space.
            _ = builder.Services.AddOptions<SpacesOptions>().ValidateOnStart();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SpacesOptions>, SharedIssuerUriSpacesValidator>());

            // Spaces' management data always needs one shared location every node can see, so Spaces
            // always switches the distributed lock to Storage, regardless of which storage instance
            // Spaces data is mapped to.
            builder.Services.AddDistributedStorageLock();

            return builder;
        }
    }
}
