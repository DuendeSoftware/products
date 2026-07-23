// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.MultiSpace.Internal;
using Duende.MultiSpace.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Duende.MultiSpace;

/// <summary>
/// Extension methods for registering multi-space services with an <see cref="IServiceCollection"/>.
/// </summary>
public static class MultiSpaceServiceCollectionExtensions
{
    /// <summary>
    /// Adds multi-space services to the service collection.
    /// </summary>
    /// <remarks>
    /// Registers a Transient <see cref="IStoreFactory"/> that routes storage operations to the
    /// pool corresponding to the current space context. When the default store factory is also
    /// registered (via <c>TryAddSingleton</c>), it will not overwrite this registration because
    /// an <see cref="IStoreFactory"/> is already present in the container.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMultiSpace(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register the multi-space store factory as Transient.
        // StorageModule uses TryAddSingleton for DefaultStoreFactory, so it will not overwrite this.
        _ = services.AddTransient<IStoreFactory>(sp =>
        {
            var pooledStore = sp.GetRequiredService<IPooledStore>();
            var contextAccessor = sp.GetRequiredService<ISpaceContextAccessor>();
            var spaceStore = sp.GetRequiredService<ISpaceStore>();
            return new MultiSpaceStoreFactory(pooledStore, contextAccessor, spaceStore);
        });

        // Space context — Scoped so each request/scope gets its own instance.
        services.TryAddScoped<ISpaceContextAccessor, SpaceContextAccessor>();

        // Management store accessor — Singleton because IPooledStore is Singleton.
        services.TryAddSingleton<ManagementStoreAccessor>(sp =>
        {
            var pooledStore = sp.GetRequiredService<IPooledStore>();
            return new ManagementStoreAccessor(pooledStore);
        });

        // Space repository — Singleton (depends only on Singleton ManagementStoreAccessor and HybridCache).
        services.TryAddSingleton<SpaceRepository>(sp =>
        {
            var storeAccessor = sp.GetRequiredService<ManagementStoreAccessor>();
            var cache = sp.GetService<HybridCache>();
            return new SpaceRepository(storeAccessor, cache);
        });

        // Space resolution infrastructure
        services.TryAddSingleton<ISpaceStore>(sp =>
        {
            var repo = sp.GetRequiredService<SpaceRepository>();
            var cache = sp.GetRequiredService<HybridCache>();
            var options = sp.GetRequiredService<IOptions<MultiSpaceOptions>>();
            return new SpaceStore(repo, cache, options);
        });
        services.TryAddSingleton<ISpacePathRewriter, DefaultSpacePathRewriter>();

        // Space admin — Singleton for CRUD management of spaces.
        services.TryAddSingleton<ISpaceAdmin>(sp =>
        {
            var repo = sp.GetRequiredService<SpaceRepository>();
            return new SpaceAdmin(repo);
        });

        // HybridCache for space resolution caching.
        _ = services.AddHybridCache();

        // Register the SpaceDso type for deserialization
        services.AddDsoRegistration<SpaceDso.V1>();

        // Options
        _ = services.AddOptions<MultiSpaceOptions>();

        return services;
    }
}
