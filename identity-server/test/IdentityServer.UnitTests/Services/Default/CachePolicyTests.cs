// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services.Default;
using Duende.Spaces;

namespace UnitTests.Services.Default;

public class CachePolicyTests
{
    [Fact]
    public void BuildKey_WithoutSpaceContextAccessor_ReturnsUnpartitionedKey()
    {
        var policy = new CachePolicy<Client>(null);

        var key = policy.BuildKey("foo");

        key.ShouldBe($"IS:{typeof(Client).FullName}-foo");
    }

    [Fact]
    public void BuildKey_WithConfiguredSpace_ReturnsPartitionedKey()
    {
        var spaceId = SpaceId.New();
        var accessor = new TestSpaceContextAccessor(spaceId, isConfigured: true);
        var policy = new CachePolicy<Client>(accessor);

        var key = policy.BuildKey("foo");

        key.ShouldBe($"{spaceId.Value}:IS:{typeof(Client).FullName}-foo");
    }

    [Fact]
    public void BuildKey_WithAccessorPresentButUnconfigured_ReturnsUnpartitionedKey()
    {
        var spaceId = SpaceId.New();
        var accessor = new TestSpaceContextAccessor(spaceId, isConfigured: false);
        var policy = new CachePolicy<Client>(accessor);

        var key = policy.BuildKey("foo");

        key.ShouldBe($"IS:{typeof(Client).FullName}-foo");
    }

    [Fact]
    public void WriteOptions_ReturnsDurationAsExpiration()
    {
        var policy = new CachePolicy<Client>(null);
        var duration = TimeSpan.FromMinutes(42);

        var entryOptions = policy.WriteOptions(duration);

        entryOptions.Expiration.ShouldBe(duration);
    }

    [Fact]
    public void Tags_WithoutSpace_ReturnsEmptyList()
    {
        var policy = new CachePolicy<Client>(null);

        var tags = policy.Tags;

        tags.ShouldBeEmpty();
    }

    [Fact]
    public void Tags_WithConfiguredSpace_ReturnsSpaceTag()
    {
        var spaceId = SpaceId.New();
        var accessor = new TestSpaceContextAccessor(spaceId, isConfigured: true);
        var policy = new CachePolicy<Client>(accessor);

        var tags = policy.Tags;

        tags.ShouldBe([$"space:{spaceId.Value}"]);
    }

    private class TestSpaceContextAccessor(SpaceId spaceId, bool isConfigured = true) : ISpaceContextAccessor
    {
        public SpaceId GetSpaceId() => spaceId;
        public bool IsSpaceIdConfigured() => isConfigured;
        public IDisposable SetSpace(SpaceId spaceId) => NoopDisposable.Instance;
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose() { }
    }
}
