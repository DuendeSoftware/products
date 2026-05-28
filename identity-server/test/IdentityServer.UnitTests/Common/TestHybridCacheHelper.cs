// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Common;

/// <summary>
/// Helper that creates a real keyed HybridCache backed by TimeProviderMemoryCache,
/// giving tests full time-controllable cache expiration via FakeTimeProvider.
/// </summary>
internal static class TestHybridCacheHelper
{
    /// <summary>
    /// Creates a ServiceProvider with a keyed HybridCache registered under
    /// <see cref="ServiceProviderKeys.ConfigurationStoreCache"/>, backed by
    /// a <see cref="FakeTimeProvider"/> so that tests can advance time to expire entries.
    /// </summary>
    /// <param name="fakeTimeProvider">The fake time provider to use for L1 cache expiration.</param>
    /// <returns>A ServiceProvider with the keyed HybridCache configured.</returns>
    public static ServiceProvider BuildServiceProvider(FakeTimeProvider fakeTimeProvider)
    {
        var services = new ServiceCollection();

        // Register FakeTimeProvider as TimeProvider so AddTimeProviderMemoryCache picks it up
        services.AddSingleton<TimeProvider>(fakeTimeProvider);

        // Register TimeProviderMemoryCache as IMemoryCache — must be BEFORE AddKeyedHybridCache
        // so HybridCache's TryAdd<IMemoryCache> finds our time-aware implementation
        services.AddTimeProviderMemoryCache();

        // Register keyed HybridCache using the IMemoryCache registered above as L1.
        // Disable distributed cache writes so no serialization is attempted for IdentityServer types.
        services.AddKeyedHybridCache(ServiceProviderKeys.ConfigurationStoreCache, options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Flags = HybridCacheEntryFlags.DisableDistributedCacheWrite
            };
        });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Resolves the keyed HybridCache from a ServiceProvider built by <see cref="BuildServiceProvider"/>.
    /// </summary>
    public static HybridCache GetCache(ServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredKeyedService<HybridCache>(ServiceProviderKeys.ConfigurationStoreCache);
}
