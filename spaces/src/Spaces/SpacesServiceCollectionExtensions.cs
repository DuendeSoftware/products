// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Private.Licencing.V2;
using Duende.Spaces.Internal;
using Duende.Spaces.Internal.Licensing;
using Duende.Spaces.Internal.Storage;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.Spaces;

/// <summary>
/// Extension methods for registering Spaces services with an <see cref="IServiceCollection"/>.
/// </summary>
public static class SpacesServiceCollectionExtensions
{
    /// <summary>
    /// Adds Spaces services to the service collection, storing Spaces data in the default storage
    /// instance.
    /// </summary>
    /// <remarks>
    /// Registers a transient <see cref="IPartitionedStorageFactory"/> that routes storage operations to the
    /// pool of the current space. Duende.Storage registers its default factory with
    /// <c>TryAddTransient</c>, so this registration wins regardless of the order in which storage
    /// and Spaces are added.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSpaces(this IServiceCollection services) =>
        services.AddSpaces(StorageInstanceId.Default);

    /// <summary>
    /// Adds Spaces services to the service collection, storing Spaces data in the given
    /// <paramref name="storageInstanceId"/>.
    /// </summary>
    /// <remarks>
    /// Registers a transient <see cref="IPartitionedStorageFactory"/> that routes storage operations to the
    /// pool of the current space. Duende.Storage registers its default factory with
    /// <c>TryAddTransient</c>, so this registration wins regardless of the order in which storage
    /// and Spaces are added.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="storageInstanceId">The storage instance to route Spaces category storage to.</param>
    /// <returns>The service collection for chaining.</returns>
    internal static IServiceCollection AddSpaces(this IServiceCollection services, StorageInstanceId storageInstanceId)
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

        // HybridCache for space resolution caching.
        _ = services.AddHybridCache();

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

        return services;
    }
}
