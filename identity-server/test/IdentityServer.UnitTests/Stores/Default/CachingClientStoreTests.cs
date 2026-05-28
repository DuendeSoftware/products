// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Common;

namespace UnitTests.Stores.Default;

public class CachingClientStoreTests : IDisposable
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private readonly SpyClientStore _spy = new();
    private readonly FakeTimeProvider _fakeTimeProvider = new();
    private readonly IdentityServerOptions _options = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly CachingClientStore<SpyClientStore> _subject;

    public CachingClientStoreTests()
    {
        _serviceProvider = TestHybridCacheHelper.BuildServiceProvider(_fakeTimeProvider);
        var cache = TestHybridCacheHelper.GetCache(_serviceProvider);
        _subject = new CachingClientStore<SpyClientStore>(_options, _spy, cache);
    }

    public void Dispose() => _serviceProvider.Dispose();

    [Fact]
    public async Task FindClientByIdAsync_returns_client_from_inner_store()
    {
        _spy.Clients["client1"] = new Client { ClientId = "client1" };

        var result = await _subject.FindClientByIdAsync("client1", _ct);

        result.ShouldNotBeNull();
        result.ClientId.ShouldBe("client1");
        _spy.FindClientByIdCalls.ShouldBe(1);
    }

    [Fact]
    public async Task FindClientByIdAsync_caches_non_null_results()
    {
        _spy.Clients["client1"] = new Client { ClientId = "client1" };

        await _subject.FindClientByIdAsync("client1", _ct);
        await _subject.FindClientByIdAsync("client1", _ct);

        _spy.FindClientByIdCalls.ShouldBe(1);
    }

    [Fact]
    public async Task FindClientByIdAsync_does_not_cache_null_results()
    {
        var result = await _subject.FindClientByIdAsync("unknown", _ct);
        result.ShouldBeNull();
        _spy.FindClientByIdCalls.ShouldBe(1);

        // Second call should still hit inner store
        result = await _subject.FindClientByIdAsync("unknown", _ct);
        result.ShouldBeNull();
        _spy.FindClientByIdCalls.ShouldBe(2);
    }

    [Fact]
    public async Task FindClientByIdAsync_returns_newly_added_client_after_prior_null_lookup()
    {
        var result = await _subject.FindClientByIdAsync("client1", _ct);
        result.ShouldBeNull();

        _spy.Clients["client1"] = new Client { ClientId = "client1" };

        result = await _subject.FindClientByIdAsync("client1", _ct);
        result.ShouldNotBeNull();
        result.ClientId.ShouldBe("client1");
    }

    [Fact]
    public async Task FindClientByIdAsync_expires_after_configured_duration()
    {
        _spy.Clients["client1"] = new Client { ClientId = "client1" };

        await _subject.FindClientByIdAsync("client1", _ct);
        _spy.FindClientByIdCalls.ShouldBe(1);

        // Advance time past the cache expiration
        _fakeTimeProvider.Advance(_options.Caching.ClientStoreExpiration + TimeSpan.FromSeconds(1));

        await _subject.FindClientByIdAsync("client1", _ct);
        _spy.FindClientByIdCalls.ShouldBe(2);
    }

    private sealed class SpyClientStore : IClientStore
    {
        public Dictionary<string, Client> Clients { get; } = [];
        public int FindClientByIdCalls { get; private set; }

        public Task<Client> FindClientByIdAsync(string clientId, Ct ct)
        {
            FindClientByIdCalls++;
            Clients.TryGetValue(clientId, out var client);
            return Task.FromResult(client);
        }

        public IAsyncEnumerable<Client> GetAllClientsAsync(Ct ct) => AsyncEnumerable.Empty<Client>();
    }
}
