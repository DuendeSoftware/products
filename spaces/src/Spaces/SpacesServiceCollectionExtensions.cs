// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal;
using Duende.Spaces.Internal.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Duende.Spaces;

/// <summary>
/// Extension methods for registering Spaces services with an <see cref="IServiceCollection"/>.
/// </summary>
public static class SpacesServiceCollectionExtensions
{
    /// <summary>
    /// Adds Spaces services to the service collection.
    /// </summary>
    /// <remarks>
    /// Registers a Transient <see cref="IStorageFactory"/> that routes storage operations to the
    /// pool corresponding to the current space context. When the default storage factory is also
    /// registered (via <c>TryAddSingleton</c>), it will not overwrite this registration because
    /// an <see cref="IStorageFactory"/> is already present in the container.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSpaces(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register the Spaces store factory as Transient.
        // StorageModule uses TryAddSingleton for DefaultStorageFactory, so it will not overwrite this.
        _ = services.AddTransient<IStorageFactory>(sp =>
        {
            var pooledStore = sp.GetRequiredService<IPooledStore>();
            var contextAccessor = sp.GetRequiredService<ISpaceContextAccessor>();
            var spaceStore = sp.GetRequiredService<ISpaceStore>();
            return new SpacesStorageFactory(pooledStore, contextAccessor, spaceStore);
        });

        services.TryAddSingleton<ISpaceContextAccessor, SpaceContextAccessor>();

        // Management storage accessor — Singleton because IPooledStore is Singleton.
        services.TryAddSingleton<ManagementStorageAccessor>(sp =>
        {
            var pooledStore = sp.GetRequiredService<IPooledStore>();
            return new ManagementStorageAccessor(pooledStore);
        });

        // Space repository — Singleton (depends only on Singleton ManagementStorageAccessor and HybridCache).
        services.TryAddSingleton<SpaceRepository>(sp =>
        {
            var storageAccessor = sp.GetRequiredService<ManagementStorageAccessor>();
            var pooledStore = sp.GetRequiredService<IPooledStore>();
            var cache = sp.GetService<HybridCache>();
            var schemaStore = sp.GetService<ISchemaStore>();
            return new SpaceRepository(storageAccessor, pooledStore, cache, schemaStore);
        });

        // Space resolution infrastructure
        services.TryAddSingleton<ISpaceStore>(sp =>
        {
            var repo = sp.GetRequiredService<SpaceRepository>();
            var cache = sp.GetRequiredService<HybridCache>();
            var options = sp.GetRequiredService<IOptions<SpacesOptions>>();
            return new SpaceStore(repo, cache, options);
        });
        services.TryAddSingleton<ISpacePathRewriter, DefaultSpacePathRewriter>();

        // Space admin — Singleton for CRUD management of spaces.
        services.TryAddSingleton<ISpaceAdmin>(sp =>
        {
            var repo = sp.GetRequiredService<SpaceRepository>();
            var schemaStore = sp.GetService<ISchemaStore>();
            return new SpaceAdmin(repo, schemaStore);
        });

        // HybridCache for space resolution caching.
        _ = services.AddHybridCache();

        // Register the SpaceDso type for deserialization
        services.AddDsoRegistration<SpaceDso.V1>();

        // Options
        _ = services.AddOptions<SpacesOptions>();

        return services;
    }
}
