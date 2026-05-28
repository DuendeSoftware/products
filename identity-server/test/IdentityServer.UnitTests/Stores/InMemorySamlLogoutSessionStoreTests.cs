// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Stores;

public sealed class InMemorySamlLogoutSessionStoreTests
{
    private const string Category = "SAML Logout Session Store";
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);

    private InMemorySamlLogoutSessionStore CreateStore() =>
        new(_timeProvider, NullLogger<InMemorySamlLogoutSessionStore>.Instance);

    private static SamlLogoutSession CreateSession(
        string logoutId = "logout-1",
        Dictionary<string, ExpectedSpLogout>? expectedResponses = null,
        DateTimeOffset? createdUtc = null) => new()
        {
            LogoutId = logoutId,
            ExpectedResponses = expectedResponses ?? new Dictionary<string, ExpectedSpLogout>
            {
                ["req-1"] = new("https://sp1.example.com"),
                ["req-2"] = new("https://sp2.example.com")
            },
            CreatedUtc = createdUtc ?? DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };

    [Fact]
    [Trait("Category", Category)]
    public async Task StoreAndRetrieveByLogoutId()
    {
        var store = CreateStore();
        var session = CreateSession();

        await store.StoreAsync(session, _ct);
        var retrieved = await store.GetByLogoutIdAsync("logout-1", _ct);

        retrieved.ShouldNotBeNull();
        retrieved.LogoutId.ShouldBe("logout-1");
        retrieved.ExpectedResponses.Count.ShouldBe(2);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetByLogoutIdReturnsNullWhenNotFound()
    {
        var store = CreateStore();

        var result = await store.GetByLogoutIdAsync("nonexistent", _ct);

        result.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task TryRecordResponseSucceedsWithMatchingIssuer()
    {
        var store = CreateStore();
        await store.StoreAsync(CreateSession(), _ct);

        var recorded = await store.TryRecordResponseAsync("req-1", "https://sp1.example.com", true, _ct);

        recorded.ShouldBeTrue();
        var session = await store.GetByLogoutIdAsync("logout-1", _ct);
        session!.ExpectedResponses["req-1"].Response.ShouldNotBeNull();
        session.ExpectedResponses["req-1"].Response!.Success.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task TryRecordResponseReturnsFalseForIssuerMismatch()
    {
        var store = CreateStore();
        await store.StoreAsync(CreateSession(), _ct);

        var recorded = await store.TryRecordResponseAsync("req-1", "https://wrong-sp.example.com", true, _ct);

        recorded.ShouldBeFalse();
        var session = await store.GetByLogoutIdAsync("logout-1", _ct);
        session!.ExpectedResponses["req-1"].Response.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task TryRecordResponseReturnsFalseForUnknownRequestId()
    {
        var store = CreateStore();
        await store.StoreAsync(CreateSession(), _ct);

        var recorded = await store.TryRecordResponseAsync("unknown-req", "https://sp1.example.com", true, _ct);

        recorded.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ExpiredSessionReturnsNull()
    {
        var store = CreateStore();
        var session = CreateSession(createdUtc: _timeProvider.GetUtcNow());
        await store.StoreAsync(session, _ct);

        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        var result = await store.GetByLogoutIdAsync("logout-1", _ct);
        result.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ExpiredSessionCannotRecordResponse()
    {
        var store = CreateStore();
        var session = CreateSession(createdUtc: _timeProvider.GetUtcNow());
        await store.StoreAsync(session, _ct);

        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        var recorded = await store.TryRecordResponseAsync("req-1", "https://sp1.example.com", true, _ct);
        recorded.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RemoveDeletesSessionAndSecondaryIndex()
    {
        var store = CreateStore();
        await store.StoreAsync(CreateSession(), _ct);

        await store.RemoveAsync("logout-1", _ct);

        var result = await store.GetByLogoutIdAsync("logout-1", _ct);
        result.ShouldBeNull();

        var recorded = await store.TryRecordResponseAsync("req-1", "https://sp1.example.com", true, _ct);
        recorded.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RemoveIsIdempotent()
    {
        var store = CreateStore();

        // Should not throw
        await store.RemoveAsync("nonexistent", _ct);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task EnforcesMaxEntries()
    {
        var store = CreateStore();

        // Fill to capacity
        for (var i = 0; i < 10_000; i++)
        {
            var session = CreateSession(
                logoutId: $"logout-{i}",
                expectedResponses: new Dictionary<string, ExpectedSpLogout>
                {
                    [$"req-{i}"] = new($"https://sp{i}.example.com")
                },
                createdUtc: _timeProvider.GetUtcNow());
            await store.StoreAsync(session, _ct);
        }

        // Next store should be rejected (silently)
        var overflow = CreateSession(
            logoutId: "overflow",
            expectedResponses: new Dictionary<string, ExpectedSpLogout>
            {
                ["req-overflow"] = new("https://overflow.example.com")
            },
            createdUtc: _timeProvider.GetUtcNow());
        await store.StoreAsync(overflow, _ct);

        var result = await store.GetByLogoutIdAsync("overflow", _ct);
        result.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RecordFailedResponse()
    {
        var store = CreateStore();
        await store.StoreAsync(CreateSession(), _ct);

        var recorded = await store.TryRecordResponseAsync("req-1", "https://sp1.example.com", false, _ct);

        recorded.ShouldBeTrue();
        var session = await store.GetByLogoutIdAsync("logout-1", _ct);
        session!.ExpectedResponses["req-1"].Response!.Success.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task DuplicateResponseOverwritesPreviousResponse()
    {
        var store = CreateStore();
        await store.StoreAsync(CreateSession(), _ct);

        // First response: failure
        await store.TryRecordResponseAsync("req-1", "https://sp1.example.com", false, _ct);
        // Second response: success (overwrites)
        var recorded = await store.TryRecordResponseAsync("req-1", "https://sp1.example.com", true, _ct);

        recorded.ShouldBeTrue();
        var session = await store.GetByLogoutIdAsync("logout-1", _ct);
        session!.ExpectedResponses["req-1"].Response!.Success.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ConcurrentRecordResponsesDoNotCorruptState()
    {
        var store = CreateStore();
        // Create a session with many expected responses
        var expectedResponses = new Dictionary<string, ExpectedSpLogout>();
        for (var i = 0; i < 100; i++)
        {
            expectedResponses[$"req-{i}"] = new($"https://sp{i}.example.com");
        }

        var session = CreateSession(expectedResponses: expectedResponses);
        await store.StoreAsync(session, _ct);

        // Record all responses concurrently
        var tasks = new List<Task<bool>>();
        for (var i = 0; i < 100; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(() =>
                store.TryRecordResponseAsync($"req-{idx}", $"https://sp{idx}.example.com", true, _ct)));
        }

        var results = await Task.WhenAll(tasks);
        results.ShouldAllBe(r => r);

        var retrieved = await store.GetByLogoutIdAsync("logout-1", _ct);
        retrieved.ShouldNotBeNull();
        retrieved.ExpectedResponses.Values
            .ShouldAllBe(e => e.Response != null && e.Response.Success);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetByLogoutIdReturnsSnapshotNotLiveReference()
    {
        var store = CreateStore();
        await store.StoreAsync(CreateSession(), _ct);

        var snapshot = await store.GetByLogoutIdAsync("logout-1", _ct);

        // Record a response after getting the snapshot
        await store.TryRecordResponseAsync("req-1", "https://sp1.example.com", true, _ct);

        // The snapshot should not reflect the change
        snapshot!.ExpectedResponses["req-1"].Response.ShouldBeNull();

        // But a fresh get should
        var fresh = await store.GetByLogoutIdAsync("logout-1", _ct);
        fresh!.ExpectedResponses["req-1"].Response.ShouldNotBeNull();
    }
}
