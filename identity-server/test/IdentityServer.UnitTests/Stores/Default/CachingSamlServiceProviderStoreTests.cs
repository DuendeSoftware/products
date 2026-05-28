// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Runtime.CompilerServices;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Common;

namespace UnitTests.Stores.Default;

public class CachingSamlServiceProviderStoreTests : IDisposable
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private readonly SpySamlServiceProviderStore _spy = new();
    private readonly FakeTimeProvider _fakeTimeProvider = new();
    private readonly IdentityServerOptions _options = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly CachingSamlServiceProviderStore<SpySamlServiceProviderStore> _subject;

    public CachingSamlServiceProviderStoreTests()
    {
        _serviceProvider = TestHybridCacheHelper.BuildServiceProvider(_fakeTimeProvider);
        var cache = TestHybridCacheHelper.GetCache(_serviceProvider);
        _subject = new CachingSamlServiceProviderStore<SpySamlServiceProviderStore>(_options, _spy, cache);
    }

    public void Dispose() => _serviceProvider.Dispose();

    private static SamlServiceProvider MakeSp(string entityId) =>
        new()
        {
            EntityId = entityId,
            AssertionConsumerServiceUrls = [new IndexedEndpoint { Location = "https://sp.example.com/acs", Index = 0 }],
            AllowedScopes = ["openid"]
        };

    [Fact]
    public async Task FindByEntityIdAsync_ReturnsServiceProviderFromInnerStore()
    {
        _spy.ServiceProviders["https://sp.example.com"] = MakeSp("https://sp.example.com");

        var result = await _subject.FindByEntityIdAsync("https://sp.example.com", _ct);

        result.ShouldNotBeNull();
        result.EntityId.ShouldBe("https://sp.example.com");
        _spy.FindByEntityIdCalls.ShouldBe(1);
    }

    [Fact]
    public async Task FindByEntityIdAsync_CachesNonNullResults()
    {
        _spy.ServiceProviders["https://sp.example.com"] = MakeSp("https://sp.example.com");

        await _subject.FindByEntityIdAsync("https://sp.example.com", _ct);
        await _subject.FindByEntityIdAsync("https://sp.example.com", _ct);

        _spy.FindByEntityIdCalls.ShouldBe(1);
    }

    [Fact]
    public async Task FindByEntityIdAsync_DoesNotCacheNullResults()
    {
        var result = await _subject.FindByEntityIdAsync("https://unknown.example.com", _ct);
        result.ShouldBeNull();
        _spy.FindByEntityIdCalls.ShouldBe(1);

        result = await _subject.FindByEntityIdAsync("https://unknown.example.com", _ct);
        result.ShouldBeNull();
        _spy.FindByEntityIdCalls.ShouldBe(2);
    }

    [Fact]
    public async Task FindByEntityIdAsync_ExpiresAfterConfiguredDuration()
    {
        _spy.ServiceProviders["https://sp.example.com"] = MakeSp("https://sp.example.com");

        await _subject.FindByEntityIdAsync("https://sp.example.com", _ct);
        _spy.FindByEntityIdCalls.ShouldBe(1);

        _fakeTimeProvider.Advance(_options.Caching.SamlServiceProviderStoreExpiration + TimeSpan.FromSeconds(1));

        await _subject.FindByEntityIdAsync("https://sp.example.com", _ct);
        _spy.FindByEntityIdCalls.ShouldBe(2);
    }

    [Fact]
    public async Task GetAllSamlServiceProvidersAsync_PassesThroughToInnerStore()
    {
        _spy.AllServiceProviders.Add(MakeSp("https://sp1.example.com"));
        _spy.AllServiceProviders.Add(MakeSp("https://sp2.example.com"));

        var result = new List<SamlServiceProvider>();
        await foreach (var sp in _subject.GetAllSamlServiceProvidersAsync(_ct))
        {
            result.Add(sp);
        }

        result.Count.ShouldBe(2);
        _spy.GetAllCalls.ShouldBe(1);

        // Second call should also hit inner store (not cached)
        result.Clear();
        await foreach (var sp in _subject.GetAllSamlServiceProvidersAsync(_ct))
        {
            result.Add(sp);
        }

        _spy.GetAllCalls.ShouldBe(2);
    }

    private sealed class SpySamlServiceProviderStore : ISamlServiceProviderStore
    {
        public Dictionary<string, SamlServiceProvider> ServiceProviders { get; } = [];
        public List<SamlServiceProvider> AllServiceProviders { get; } = [];
        public int FindByEntityIdCalls { get; private set; }
        public int GetAllCalls { get; private set; }

        public Task<SamlServiceProvider?> FindByEntityIdAsync(string entityId, Ct ct)
        {
            FindByEntityIdCalls++;
            ServiceProviders.TryGetValue(entityId, out var sp);
            return Task.FromResult(sp);
        }

        public async IAsyncEnumerable<SamlServiceProvider> GetAllSamlServiceProvidersAsync([EnumeratorCancellation] Ct ct)
        {
            GetAllCalls++;
            foreach (var sp in AllServiceProviders)
            {
                yield return sp;
            }
        }
    }
}
