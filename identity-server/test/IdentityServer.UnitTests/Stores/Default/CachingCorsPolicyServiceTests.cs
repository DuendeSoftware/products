// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Common;

namespace UnitTests.Stores.Default;

public class CachingCorsPolicyServiceTests : IDisposable
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private readonly SpyCorsPolicyService _spy = new();
    private readonly FakeTimeProvider _fakeTimeProvider = new();
    private readonly IdentityServerOptions _options = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly CachingCorsPolicyService<SpyCorsPolicyService> _subject;

    public CachingCorsPolicyServiceTests()
    {
        _serviceProvider = TestHybridCacheHelper.BuildServiceProvider(_fakeTimeProvider);
        var cache = TestHybridCacheHelper.GetCache(_serviceProvider);
        _subject = new CachingCorsPolicyService<SpyCorsPolicyService>(_options, _spy, cache);
    }

    public void Dispose() => _serviceProvider.Dispose();

    [Fact]
    public async Task IsOriginAllowedAsync_returns_true_for_allowed_origin()
    {
        _spy.AllowedOrigins.Add("https://allowed.example.com");

        var result = await _subject.IsOriginAllowedAsync("https://allowed.example.com", _ct);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsOriginAllowedAsync_returns_false_for_denied_origin()
    {
        var result = await _subject.IsOriginAllowedAsync("https://denied.example.com", _ct);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsOriginAllowedAsync_caches_allowed_results()
    {
        _spy.AllowedOrigins.Add("https://allowed.example.com");

        await _subject.IsOriginAllowedAsync("https://allowed.example.com", _ct);
        await _subject.IsOriginAllowedAsync("https://allowed.example.com", _ct);

        _spy.IsOriginAllowedCalls.ShouldBe(1);
    }

    [Fact]
    public async Task IsOriginAllowedAsync_caches_false_results()
    {
        await _subject.IsOriginAllowedAsync("https://denied.example.com", _ct);
        await _subject.IsOriginAllowedAsync("https://denied.example.com", _ct);

        // Unlike client store, CORS caches both true and false results
        _spy.IsOriginAllowedCalls.ShouldBe(1);
    }

    [Fact]
    public async Task IsOriginAllowedAsync_expires_after_configured_duration()
    {
        _spy.AllowedOrigins.Add("https://allowed.example.com");

        await _subject.IsOriginAllowedAsync("https://allowed.example.com", _ct);
        _spy.IsOriginAllowedCalls.ShouldBe(1);

        _fakeTimeProvider.Advance(_options.Caching.CorsExpiration + TimeSpan.FromSeconds(1));

        await _subject.IsOriginAllowedAsync("https://allowed.example.com", _ct);
        _spy.IsOriginAllowedCalls.ShouldBe(2);
    }

    [Fact]
    public async Task IsOriginAllowedAsync_caches_different_origins_independently()
    {
        _spy.AllowedOrigins.Add("https://allowed.example.com");

        await _subject.IsOriginAllowedAsync("https://allowed.example.com", _ct);
        await _subject.IsOriginAllowedAsync("https://other.example.com", _ct);

        _spy.IsOriginAllowedCalls.ShouldBe(2);

        // Both should now be cached
        await _subject.IsOriginAllowedAsync("https://allowed.example.com", _ct);
        await _subject.IsOriginAllowedAsync("https://other.example.com", _ct);

        _spy.IsOriginAllowedCalls.ShouldBe(2);
    }

    private sealed class SpyCorsPolicyService : ICorsPolicyService
    {
        public HashSet<string> AllowedOrigins { get; } = [];
        public int IsOriginAllowedCalls { get; private set; }

        public Task<bool> IsOriginAllowedAsync(string origin, Ct ct)
        {
            IsOriginAllowedCalls++;
            return Task.FromResult(AllowedOrigins.Contains(origin));
        }
    }
}
