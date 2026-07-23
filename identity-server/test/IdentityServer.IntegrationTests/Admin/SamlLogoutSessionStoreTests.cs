// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Stores.Storage.SamlLogoutSession;
using Duende.Storage;
using Duende.Storage.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// Integration tests for the IStore-backed ISamlLogoutSessionStore implementation.
/// Tests cover all 4 interface methods against a real SQLite database.
/// </summary>
public sealed class SamlLogoutSessionStoreTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private ISamlLogoutSessionStore Store => _fixture.SamlLogoutSessionStore;

    private static SamlLogoutSession BuildSession(
        string? logoutId = null,
        Dictionary<string, ExpectedSpLogout>? expectedResponses = null,
        DateTime? expiresAt = null) =>
        new()
        {
            LogoutId = logoutId ?? $"logout-{Guid.NewGuid():N}",
            ExpectedResponses = expectedResponses ?? new Dictionary<string, ExpectedSpLogout>
            {
                [$"req-{Guid.NewGuid():N}"] = new("https://sp1.example.com"),
                [$"req-{Guid.NewGuid():N}"] = new("https://sp2.example.com")
            },
            SkippedSpCount = 0,
            CreatedUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = expiresAt ?? DateTime.UtcNow.AddMinutes(5)
        };

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    // ─────────────────────────── Store + Retrieve ───────────────────────────

    [Fact]
    public async Task StoreAndGetByLogoutId_RoundTrips()
    {
        var session = BuildSession();

        await Store.StoreAsync(session, _ct);

        var retrieved = await Store.GetByLogoutIdAsync(session.LogoutId, _ct);

        retrieved.ShouldNotBeNull();
        retrieved.LogoutId.ShouldBe(session.LogoutId);
        retrieved.ExpectedResponses.Count.ShouldBe(session.ExpectedResponses.Count);
        retrieved.SkippedSpCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetByLogoutId_Nonexistent_ReturnsNull()
    {
        var result = await Store.GetByLogoutIdAsync("nonexistent-logout-id", _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByLogoutId_Expired_ReturnsNull()
    {
        var session = BuildSession(expiresAt: DateTime.UtcNow.AddSeconds(-10));

        await Store.StoreAsync(session, _ct);

        var result = await Store.GetByLogoutIdAsync(session.LogoutId, _ct);

        result.ShouldBeNull();
    }

    // ─────────────────────────── TryRecordResponse ───────────────────────────

    [Fact]
    public async Task TryRecordResponse_Success_UpdatesSession()
    {
        var requestId = $"req-{Guid.NewGuid():N}";
        var spEntityId = "https://sp1.example.com";
        var session = BuildSession(expectedResponses: new Dictionary<string, ExpectedSpLogout>
        {
            [requestId] = new(spEntityId)
        });

        await Store.StoreAsync(session, _ct);

        var recorded = await Store.TryRecordResponseAsync(requestId, spEntityId, true, _ct);

        recorded.ShouldBeTrue();

        var retrieved = await Store.GetByLogoutIdAsync(session.LogoutId, _ct);
        retrieved.ShouldNotBeNull();
        retrieved.ExpectedResponses[requestId].Response.ShouldNotBeNull();
        retrieved.ExpectedResponses[requestId].Response!.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task TryRecordResponse_UnknownRequestId_ReturnsFalse()
    {
        var result = await Store.TryRecordResponseAsync("unknown-request-id", "https://sp.example.com", true, _ct);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task TryRecordResponse_IssuerMismatch_ReturnsFalse()
    {
        var requestId = $"req-{Guid.NewGuid():N}";
        var session = BuildSession(expectedResponses: new Dictionary<string, ExpectedSpLogout>
        {
            [requestId] = new("https://sp1.example.com")
        });

        await Store.StoreAsync(session, _ct);

        var result = await Store.TryRecordResponseAsync(requestId, "https://wrong-sp.example.com", true, _ct);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task TryRecordResponse_ExpiredSession_ReturnsFalse()
    {
        var requestId = $"req-{Guid.NewGuid():N}";
        var session = BuildSession(
            expiresAt: DateTime.UtcNow.AddSeconds(-10),
            expectedResponses: new Dictionary<string, ExpectedSpLogout>
            {
                [requestId] = new("https://sp1.example.com")
            });

        await Store.StoreAsync(session, _ct);

        var result = await Store.TryRecordResponseAsync(requestId, "https://sp1.example.com", true, _ct);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task TryRecordResponse_MultipleRequestIds_AllResolvable()
    {
        var req1 = $"req-{Guid.NewGuid():N}";
        var req2 = $"req-{Guid.NewGuid():N}";
        var req3 = $"req-{Guid.NewGuid():N}";
        var session = BuildSession(expectedResponses: new Dictionary<string, ExpectedSpLogout>
        {
            [req1] = new("https://sp1.example.com"),
            [req2] = new("https://sp2.example.com"),
            [req3] = new("https://sp3.example.com")
        });

        await Store.StoreAsync(session, _ct);

        (await Store.TryRecordResponseAsync(req1, "https://sp1.example.com", true, _ct)).ShouldBeTrue();
        (await Store.TryRecordResponseAsync(req2, "https://sp2.example.com", false, _ct)).ShouldBeTrue();
        (await Store.TryRecordResponseAsync(req3, "https://sp3.example.com", true, _ct)).ShouldBeTrue();

        var retrieved = await Store.GetByLogoutIdAsync(session.LogoutId, _ct);
        retrieved.ShouldNotBeNull();
        retrieved.ExpectedResponses[req1].Response!.Success.ShouldBeTrue();
        retrieved.ExpectedResponses[req2].Response!.Success.ShouldBeFalse();
        retrieved.ExpectedResponses[req3].Response!.Success.ShouldBeTrue();
    }

    // ─────────────────────────── Remove ───────────────────────────

    [Fact]
    public async Task Remove_ThenGet_ReturnsNull()
    {
        var session = BuildSession();
        await Store.StoreAsync(session, _ct);

        await Store.RemoveAsync(session.LogoutId, _ct);

        var result = await Store.GetByLogoutIdAsync(session.LogoutId, _ct);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Remove_Nonexistent_DoesNotThrow() =>
        await Should.NotThrowAsync(() =>
            Store.RemoveAsync("nonexistent-logout-id", _ct));

    // ─────────────────────────── Validation ───────────────────────────

    [Fact]
    public async Task StoreWithoutExpiration_Throws()
    {
        var session = new SamlLogoutSession
        {
            LogoutId = "test-logout-id",
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>
            {
                ["req-1"] = new("https://sp.example.com")
            },
            CreatedUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = default
        };

        await Should.ThrowAsync<ArgumentException>(() =>
            Store.StoreAsync(session, _ct));
    }

    [Fact]
    public async Task StoreDuplicateLogoutId_Throws()
    {
        var logoutId = $"logout-{Guid.NewGuid():N}";
        var session1 = BuildSession(logoutId: logoutId);
        var session2 = BuildSession(logoutId: logoutId);

        await Store.StoreAsync(session1, _ct);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            Store.StoreAsync(session2, _ct));
    }

    // ─────────────────────────── Concurrency ───────────────────────────

    [Fact]
    public async Task TryRecordResponse_ConcurrentWrites_BothSucceed()
    {
        var req1 = $"req-{Guid.NewGuid():N}";
        var req2 = $"req-{Guid.NewGuid():N}";
        var session = BuildSession(expectedResponses: new Dictionary<string, ExpectedSpLogout>
        {
            [req1] = new("https://sp1.example.com"),
            [req2] = new("https://sp2.example.com")
        });

        await Store.StoreAsync(session, _ct);

        // Fire both record calls concurrently — one may hit UnexpectedVersion and retry
        var results = await Task.WhenAll(
            Store.TryRecordResponseAsync(req1, "https://sp1.example.com", true, _ct),
            Store.TryRecordResponseAsync(req2, "https://sp2.example.com", true, _ct));

        results[0].ShouldBeTrue();
        results[1].ShouldBeTrue();

        // Verify both responses recorded
        var retrieved = await Store.GetByLogoutIdAsync(session.LogoutId, _ct);
        retrieved.ShouldNotBeNull();
        retrieved.ExpectedResponses[req1].Response.ShouldNotBeNull();
        retrieved.ExpectedResponses[req2].Response.ShouldNotBeNull();
    }

    [Fact]
    public async Task TryRecordResponse_AfterExternalUpdate_SucceedsWithFreshState()
    {
        var requestId = $"req-{Guid.NewGuid():N}";
        var spEntityId = "https://sp1.example.com";
        var session = BuildSession(expectedResponses: new Dictionary<string, ExpectedSpLogout>
        {
            [requestId] = new(spEntityId)
        });

        await Store.StoreAsync(session, _ct);

        // Use the repository directly to bump the version (simulating a concurrent write)
        using var scope = _fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<SamlLogoutSessionRepository>();
        var entry = await repository.TryReadByLogoutIdAsync(session.LogoutId, _ct);
        entry.ShouldNotBeNull();

        // Re-write with same data to bump version
        var expiration = Expiration.AtAbsolute(
            new DateTimeOffset(new DateTime(entry.Value.Dso.ExpiresAtUtcTicks, DateTimeKind.Utc), TimeSpan.Zero));
        var updateResult = await repository.UpdateAsync(
            UuidV7.From(entry.Value.Id), entry.Value.Dso, entry.Value.Version, expiration, _ct);
        updateResult.ShouldBe(Duende.Storage.Internal.Operations.UpdateResult.Success);

        // Now TryRecordResponseAsync reads the new version and succeeds directly
        var recorded = await Store.TryRecordResponseAsync(requestId, spEntityId, true, _ct);
        recorded.ShouldBeTrue();

        var retrieved = await Store.GetByLogoutIdAsync(session.LogoutId, _ct);
        retrieved.ShouldNotBeNull();
        retrieved.ExpectedResponses[requestId].Response.ShouldNotBeNull();
        retrieved.ExpectedResponses[requestId].Response!.Success.ShouldBeTrue();
    }
}
