// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer;
using Duende.IdentityServer.Stores;
using Duende.Spaces;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Configuration.DependencyInjection;

public class ConfigurationStoreHybridCacheSpaceAwarenessTests
{
    private sealed class FakeSpaceContextAccessor : ISpaceContextAccessor
    {
        public SpaceId GetSpaceId() => throw new NotSupportedException();

        public bool IsSpaceIdConfigured() => false;

        public IDisposable SetSpace(SpaceId spaceId) => throw new NotSupportedException();
    }

    private static IIdentityServerBuilder AddIdentityServerWithClientStoreCache(IServiceCollection services)
    {
        services.AddLogging();
        var builder = services.AddIdentityServer();
        builder.AddClientStoreCache<NullClientStore>();
        return builder;
    }

    private static IServiceCollection AddSpacesWithFakeAccessor(IServiceCollection services)
    {
        services.AddSpaces();

        // Avoid the license-gated default accessor; the factory only needs an accessor instance.
        services.AddSingleton<ISpaceContextAccessor, FakeSpaceContextAccessor>();
        return services;
    }

    private static HybridCache ResolveConfigurationStoreCache(ServiceProvider sp) =>
        sp.GetRequiredService<IHybridCacheFactory>().GetCache(ServiceProviderKeys.ConfigurationStoreCache);

    [Fact]
    public void factory_returns_keyed_cache_unchanged_without_spaces()
    {
        var services = new ServiceCollection();
        AddIdentityServerWithClientStoreCache(services);

        using var sp = services.BuildServiceProvider();

        var cache = ResolveConfigurationStoreCache(sp);

        cache.ShouldBeSameAs(sp.GetRequiredKeyedService<HybridCache>(ServiceProviderKeys.ConfigurationStoreCache));
    }

    [Fact]
    public void factory_returns_space_aware_cache_when_spaces_added_before_identity_server()
    {
        var services = new ServiceCollection();
        AddSpacesWithFakeAccessor(services);
        AddIdentityServerWithClientStoreCache(services);

        using var sp = services.BuildServiceProvider();

        ResolveConfigurationStoreCache(sp).ShouldBeOfType<SpaceAwareHybridCache>();
    }

    [Fact]
    public void factory_returns_space_aware_cache_when_spaces_added_after_identity_server()
    {
        var services = new ServiceCollection();
        AddIdentityServerWithClientStoreCache(services);
        AddSpacesWithFakeAccessor(services);

        using var sp = services.BuildServiceProvider();

        ResolveConfigurationStoreCache(sp).ShouldBeOfType<SpaceAwareHybridCache>();
    }

    [Fact]
    public async Task app_registered_cache_before_identity_server_is_used_as_the_inner_cache()
    {
        var services = new ServiceCollection();
        var appCache = new RecordingHybridCache();
        services.AddKeyedSingleton<HybridCache>(ServiceProviderKeys.ConfigurationStoreCache, appCache);
        AddIdentityServerWithClientStoreCache(services);
        AddSpacesWithFakeAccessor(services);

        using var sp = services.BuildServiceProvider();
        var cache = ResolveConfigurationStoreCache(sp);

        _ = await cache.GetOrCreateAsync("key", 0, static (_, _) => ValueTask.FromResult(1), cancellationToken: TestContext.Current.CancellationToken);

        cache.ShouldBeOfType<SpaceAwareHybridCache>();
        appCache.GetOrCreateCalls.ShouldBe(1);
    }

    [Fact]
    public async Task app_registered_cache_after_identity_server_is_used_as_the_inner_cache()
    {
        var services = new ServiceCollection();
        AddIdentityServerWithClientStoreCache(services);
        AddSpacesWithFakeAccessor(services);
        var appCache = new RecordingHybridCache();
        services.AddKeyedSingleton<HybridCache>(ServiceProviderKeys.ConfigurationStoreCache, appCache);

        using var sp = services.BuildServiceProvider();
        var cache = ResolveConfigurationStoreCache(sp);

        _ = await cache.GetOrCreateAsync("key", 0, static (_, _) => ValueTask.FromResult(1), cancellationToken: TestContext.Current.CancellationToken);

        cache.ShouldBeOfType<SpaceAwareHybridCache>();
        appCache.GetOrCreateCalls.ShouldBe(1);
    }

    [Fact]
    public void app_registered_space_aware_cache_is_not_wrapped_again()
    {
        var services = new ServiceCollection();
        AddIdentityServerWithClientStoreCache(services);
        AddSpacesWithFakeAccessor(services);
        var appCache = new SpaceAwareHybridCache(new RecordingHybridCache(), new FakeSpaceContextAccessor());
        services.AddKeyedSingleton<HybridCache>(ServiceProviderKeys.ConfigurationStoreCache, appCache);

        using var sp = services.BuildServiceProvider();

        ResolveConfigurationStoreCache(sp).ShouldBeSameAs(appCache);
    }

    [Fact]
    public void app_registered_factory_is_not_replaced_by_identity_server()
    {
        var services = new ServiceCollection();
        services.AddTransient<IHybridCacheFactory, RecordingHybridCacheFactory>();
        AddIdentityServerWithClientStoreCache(services);

        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<IHybridCacheFactory>().ShouldBeOfType<RecordingHybridCacheFactory>();
    }

    [Fact]
    public void caching_store_resolves_its_cache_through_the_factory()
    {
        var services = new ServiceCollection();
        AddIdentityServerWithClientStoreCache(services);
        services.AddSingleton<RecordingHybridCacheFactory>();
        services.AddSingleton<IHybridCacheFactory>(sp => sp.GetRequiredService<RecordingHybridCacheFactory>());

        using var sp = services.BuildServiceProvider();
        _ = sp.GetRequiredService<IClientStore>();

        sp.GetRequiredService<RecordingHybridCacheFactory>().RequestedKeys.ShouldBe([ServiceProviderKeys.ConfigurationStoreCache]);
    }

    private sealed class RecordingHybridCacheFactory : IHybridCacheFactory
    {
        public List<string> RequestedKeys { get; } = [];

        public HybridCache GetCache(string serviceKey)
        {
            RequestedKeys.Add(serviceKey);
            return new RecordingHybridCache();
        }
    }

    private sealed class NullClientStore : IClientStore
    {
        public Task<Duende.IdentityServer.Models.Client?> FindClientByIdAsync(string clientId, Ct ct) =>
            Task.FromResult<Duende.IdentityServer.Models.Client?>(null);

#if NET10_0_OR_GREATER
        public IAsyncEnumerable<Duende.IdentityServer.Models.Client> GetAllClientsAsync(Ct ct) => AsyncEnumerable.Empty<Duende.IdentityServer.Models.Client>();
#endif
    }

    private sealed class RecordingHybridCache : HybridCache
    {
        public int GetOrCreateCalls { get; private set; }

        public override ValueTask<T> GetOrCreateAsync<TState, T>(
            string key,
            TState state,
            Func<TState, CancellationToken, ValueTask<T>> factory,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            GetOrCreateCalls++;
            return factory(state, cancellationToken);
        }

        public override ValueTask SetAsync<T>(
            string key,
            T value,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
