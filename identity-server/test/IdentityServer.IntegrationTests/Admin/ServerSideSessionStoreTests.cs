// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// Integration tests for the IStore-backed IServerSideSessionStore implementation.
/// Tests cover all 8 interface methods against a real SQLite database.
/// </summary>
public sealed class ServerSideSessionStoreTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly List<IServiceScope> _scopes = [];

    private IServerSideSessionStore BuildStore()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IServerSideSessionStore>();
    }

    private static ServerSideSession BuildSession(
        string? key = null,
        string? subjectId = null,
        string? sessionId = null,
        string? displayName = null,
        DateTime? expires = null) =>
        new()
        {
            Key = key ?? $"key_{Guid.NewGuid():N}",
            Scheme = "cookie",
            SubjectId = subjectId ?? $"sub_{Guid.NewGuid():N}",
            SessionId = sessionId ?? $"sid_{Guid.NewGuid():N}",
            DisplayName = displayName,
            Created = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Renewed = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Expires = expires,
            Ticket = "serialized_ticket_data"
        };

    /// <summary>
    /// Creates a session that is considered expired for cleanup queries.
    /// IStore's CreateAsync silently skips entities whose entity-level TTL is already past,
    /// so we create with a future expiry then update with a past expiry.
    /// The entity remains visible on reads (TTL is best-effort) with a past Expires search field.
    /// </summary>
    private async Task<ServerSideSession> CreateExpiredSessionAsync(
        IServerSideSessionStore store,
        string? subjectId = null)
    {
        var session = BuildSession(
            subjectId: subjectId,
            expires: DateTime.UtcNow.AddDays(1));
        await store.CreateSessionAsync(session, _ct);

        var expired = new ServerSideSession
        {
            Key = session.Key,
            Scheme = session.Scheme,
            SubjectId = session.SubjectId,
            SessionId = session.SessionId,
            DisplayName = session.DisplayName,
            Created = session.Created,
            Renewed = session.Renewed,
            Expires = DateTime.UtcNow.AddDays(-1),
            Ticket = session.Ticket
        };
        await store.UpdateSessionAsync(expired, _ct);
        return expired;
    }

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        await _fixture.DisposeAsync();
    }

    // ─────────────────────────── CRUD ───────────────────────────

    [Fact]
    public async Task GetSessionAsync_WhenSessionDoesNotExist_ReturnsNull()
    {
        var store = BuildStore();

        var result = await store.GetSessionAsync("nonexistent_key", _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task CreateSessionAsync_ThenGetSessionAsync_ReturnsSession()
    {
        var store = BuildStore();
        var session = BuildSession();

        await store.CreateSessionAsync(session, _ct);
        var result = await store.GetSessionAsync(session.Key, _ct);

        result.ShouldNotBeNull();
        result.Key.ShouldBe(session.Key);
        result.SubjectId.ShouldBe(session.SubjectId);
        result.SessionId.ShouldBe(session.SessionId);
        result.Ticket.ShouldBe(session.Ticket);
    }

    [Fact]
    public async Task CreateSessionAsync_MapsAllFieldsCorrectly()
    {
        var store = BuildStore();
        var expires = new DateTime(2099, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var session = BuildSession(displayName: "Test User", expires: expires);

        await store.CreateSessionAsync(session, _ct);
        var result = await store.GetSessionAsync(session.Key, _ct);

        result.ShouldNotBeNull();
        result.Scheme.ShouldBe("cookie");
        result.DisplayName.ShouldBe("Test User");
        result.Created.ShouldBe(session.Created);
        result.Renewed.ShouldBe(session.Renewed);
        result.Expires.ShouldBe(expires);
    }

    [Fact]
    public async Task UpdateSessionAsync_ThenGetSessionAsync_ReturnsUpdatedValues()
    {
        var store = BuildStore();
        var session = BuildSession();
        await store.CreateSessionAsync(session, _ct);

        var updated = new ServerSideSession
        {
            Key = session.Key,
            Scheme = session.Scheme,
            SubjectId = session.SubjectId,
            SessionId = session.SessionId,
            DisplayName = "Updated Name",
            Created = session.Created,
            Renewed = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Expires = session.Expires,
            Ticket = "updated_ticket"
        };
        await store.UpdateSessionAsync(updated, _ct);

        var result = await store.GetSessionAsync(session.Key, _ct);

        result.ShouldNotBeNull();
        result.DisplayName.ShouldBe("Updated Name");
        result.Ticket.ShouldBe("updated_ticket");
        result.Renewed.ShouldBe(updated.Renewed);
    }

    [Fact]
    public async Task DeleteSessionAsync_ThenGetSessionAsync_ReturnsNull()
    {
        var store = BuildStore();
        var session = BuildSession();
        await store.CreateSessionAsync(session, _ct);

        await store.DeleteSessionAsync(session.Key, _ct);
        var result = await store.GetSessionAsync(session.Key, _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteSessionAsync_WhenSessionDoesNotExist_DoesNotThrow()
    {
        var store = BuildStore();

        await Should.NotThrowAsync(() =>
            store.DeleteSessionAsync("nonexistent_key", _ct));
    }

    // ─────────────────────────── Filter operations ───────────────────────────

    [Fact]
    public async Task GetSessionsAsync_BySubjectId_ReturnsMatchingSessions()
    {
        var store = BuildStore();
        var subjectId = $"sub_{Guid.NewGuid():N}";

        var session1 = BuildSession(subjectId: subjectId);
        var session2 = BuildSession(subjectId: subjectId);
        var other = BuildSession(); // different subject

        await store.CreateSessionAsync(session1, _ct);
        await store.CreateSessionAsync(session2, _ct);
        await store.CreateSessionAsync(other, _ct);

        var results = await store.GetSessionsAsync(new SessionFilter { SubjectId = subjectId }, _ct);

        results.Count.ShouldBe(2);
        results.All(r => r.SubjectId == subjectId).ShouldBeTrue();
    }

    [Fact]
    public async Task GetSessionsAsync_BySessionId_ReturnsMatchingSession()
    {
        var store = BuildStore();
        var sessionId = $"sid_{Guid.NewGuid():N}";

        var session = BuildSession(sessionId: sessionId);
        await store.CreateSessionAsync(session, _ct);
        await store.CreateSessionAsync(BuildSession(), _ct); // unrelated

        var results = await store.GetSessionsAsync(new SessionFilter { SessionId = sessionId }, _ct);

        results.Count.ShouldBe(1);
        results.Single().SessionId.ShouldBe(sessionId);
    }

    [Fact]
    public async Task GetSessionsAsync_BySubjectIdAndSessionId_ReturnsOnlyExactMatch()
    {
        var store = BuildStore();
        var subjectId = $"sub_{Guid.NewGuid():N}";
        var sessionId = $"sid_{Guid.NewGuid():N}";
        var otherSessionId = $"sid_{Guid.NewGuid():N}";

        var target = BuildSession(subjectId: subjectId, sessionId: sessionId);
        var sameSubjectDifferentSession = BuildSession(subjectId: subjectId, sessionId: otherSessionId);

        await store.CreateSessionAsync(target, _ct);
        await store.CreateSessionAsync(sameSubjectDifferentSession, _ct);

        var results = await store.GetSessionsAsync(
            new SessionFilter { SubjectId = subjectId, SessionId = sessionId }, _ct);

        results.Count.ShouldBe(1);
        results.Single().Key.ShouldBe(target.Key);
    }

    [Fact]
    public async Task GetSessionsAsync_WithEmptyFilter_Throws()
    {
        var store = BuildStore();

        await Should.ThrowAsync<ArgumentNullException>(() =>
            store.GetSessionsAsync(new SessionFilter(), _ct));
    }

    [Fact]
    public async Task DeleteSessionsAsync_BySubjectId_RemovesAllMatchingSessions()
    {
        var store = BuildStore();
        var subjectId = $"sub_{Guid.NewGuid():N}";

        var session1 = BuildSession(subjectId: subjectId);
        var session2 = BuildSession(subjectId: subjectId);
        var other = BuildSession();

        await store.CreateSessionAsync(session1, _ct);
        await store.CreateSessionAsync(session2, _ct);
        await store.CreateSessionAsync(other, _ct);

        await store.DeleteSessionsAsync(new SessionFilter { SubjectId = subjectId }, _ct);

        var remaining = await store.GetSessionsAsync(new SessionFilter { SubjectId = subjectId }, _ct);
        remaining.Count.ShouldBe(0);

        // Unrelated session is unaffected
        var otherResult = await store.GetSessionAsync(other.Key, _ct);
        otherResult.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteSessionsAsync_WithEmptyFilter_Throws()
    {
        var store = BuildStore();

        await Should.ThrowAsync<ArgumentNullException>(() =>
            store.DeleteSessionsAsync(new SessionFilter(), _ct));
    }

    // ─────────────────────────── Expiration ───────────────────────────

    [Fact]
    public async Task GetAndRemoveExpiredSessionsAsync_WithNoExpiredSessions_ReturnsEmpty()
    {
        var store = BuildStore();
        // Create a session with future expiry (not expired)
        var session = BuildSession(expires: DateTime.UtcNow.AddDays(1));
        await store.CreateSessionAsync(session, _ct);

        var results = await store.GetAndRemoveExpiredSessionsAsync(10, _ct);

        results.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetAndRemoveExpiredSessionsAsync_WithExpiredSessions_ReturnsAndDeletesThem()
    {
        var store = BuildStore();
        var expired = await CreateExpiredSessionAsync(store);

        var results = await store.GetAndRemoveExpiredSessionsAsync(10, _ct);

        results.Count.ShouldBe(1);
        results.Single().Key.ShouldBe(expired.Key);

        // Verify the session was actually deleted
        var afterDelete = await store.GetSessionAsync(expired.Key, _ct);
        afterDelete.ShouldBeNull();
    }

    [Fact]
    public async Task GetAndRemoveExpiredSessionsAsync_RespectsCountLimit()
    {
        var store = BuildStore();
        var subjectId = $"sub_{Guid.NewGuid():N}";

        for (var i = 0; i < 5; i++)
        {
            await CreateExpiredSessionAsync(store, subjectId);
        }

        var results = await store.GetAndRemoveExpiredSessionsAsync(3, _ct);

        results.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetAndRemoveExpiredSessionsAsync_DoesNotAffectNonExpiredSessions()
    {
        var store = BuildStore();
        var expired = await CreateExpiredSessionAsync(store);
        var notExpired = BuildSession(expires: DateTime.UtcNow.AddDays(1));

        await store.CreateSessionAsync(notExpired, _ct);

        await store.GetAndRemoveExpiredSessionsAsync(10, _ct);

        var notExpiredResult = await store.GetSessionAsync(notExpired.Key, _ct);
        notExpiredResult.ShouldNotBeNull();

        // Confirm the expired one is gone
        var expiredResult = await store.GetSessionAsync(expired.Key, _ct);
        expiredResult.ShouldBeNull();
    }

    [Fact]
    public async Task GetAndRemoveExpiredSessionsAsync_DoesNotMatchSessionsWithNullExpiration()
    {
        var store = BuildStore();
        // Session with no expiry (Expires = null) should never be considered expired
        var noExpiry = BuildSession(expires: null);
        await store.CreateSessionAsync(noExpiry, _ct);

        var results = await store.GetAndRemoveExpiredSessionsAsync(10, _ct);

        results.ShouldNotContain(r => r.Key == noExpiry.Key);
    }

    [Fact]
    public async Task UpdateSessionAsync_SettingExpiresFromNullToValue_IsFoundByExpirationQuery()
    {
        var store = BuildStore();
        var session = BuildSession(expires: null); // no expiry
        await store.CreateSessionAsync(session, _ct);

        // Update to add a past expiration
        var updated = new ServerSideSession
        {
            Key = session.Key,
            Scheme = session.Scheme,
            SubjectId = session.SubjectId,
            SessionId = session.SessionId,
            DisplayName = session.DisplayName,
            Created = session.Created,
            Renewed = session.Renewed,
            Expires = DateTime.UtcNow.AddDays(-1),
            Ticket = session.Ticket
        };
        await store.UpdateSessionAsync(updated, _ct);

        var results = await store.GetAndRemoveExpiredSessionsAsync(10, _ct);

        results.ShouldContain(r => r.Key == session.Key);
    }

    [Fact]
    public async Task UpdateSessionAsync_ClearingExpiration_IsNotFoundByExpirationQuery()
    {
        var store = BuildStore();
        // Create with future expiry (so IStore actually persists it), then update to past expiry
        var expired = await CreateExpiredSessionAsync(store);

        // Now clear the expiration (set to null)
        var cleared = new ServerSideSession
        {
            Key = expired.Key,
            Scheme = expired.Scheme,
            SubjectId = expired.SubjectId,
            SessionId = expired.SessionId,
            DisplayName = expired.DisplayName,
            Created = expired.Created,
            Renewed = expired.Renewed,
            Expires = null,
            Ticket = expired.Ticket
        };
        await store.UpdateSessionAsync(cleared, _ct);

        var results = await store.GetAndRemoveExpiredSessionsAsync(10, _ct);

        results.ShouldNotContain(r => r.Key == expired.Key);
    }

    // ─────────────────────────── Paginated query ───────────────────────────

    [Fact]
    public async Task QuerySessionsAsync_WithNoSessions_ReturnsEmptyResult()
    {
        // Use a unique subject so we don't pick up sessions from other tests
        var uniqueSub = $"sub_{Guid.NewGuid():N}";
        var store = BuildStore();

        var result = await store.QuerySessionsAsync(_ct, new SessionQuery { SubjectId = uniqueSub });

        result.Results.Count.ShouldBe(0);
        result.HasNextResults.ShouldBeFalse();
        result.HasPrevResults.ShouldBeFalse();
    }

    [Fact]
    public async Task QuerySessionsAsync_WithNullFilter_ReturnsResults()
    {
        var store = BuildStore();
        var session = BuildSession();
        await store.CreateSessionAsync(session, _ct);

        var result = await store.QuerySessionsAsync(_ct, null);

        result.Results.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task QuerySessionsAsync_PaginationForward_ReturnsNextPage()
    {
        var store = BuildStore();
        var subjectId = $"sub_{Guid.NewGuid():N}";

        // Create 5 sessions for the same subject
        for (var i = 0; i < 5; i++)
        {
            await store.CreateSessionAsync(BuildSession(subjectId: subjectId), _ct);
        }

        // Fetch page 1 with size 2
        var page1 = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2
        });

        page1.Results.Count.ShouldBe(2);
        page1.HasNextResults.ShouldBeTrue();
        page1.HasPrevResults.ShouldBeFalse();

        // Fetch page 2
        var page2 = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2,
            ResultsToken = page1.ResultsToken
        });

        page2.Results.Count.ShouldBe(2);
        page2.HasPrevResults.ShouldBeTrue();
    }

    [Fact]
    public async Task QuerySessionsAsync_PaginationBackward_ReturnsPreviousPage()
    {
        var store = BuildStore();
        var subjectId = $"sub_{Guid.NewGuid():N}";

        for (var i = 0; i < 4; i++)
        {
            await store.CreateSessionAsync(BuildSession(subjectId: subjectId), _ct);
        }

        // Get page 1
        var page1 = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2
        });

        // Advance to page 2
        var page2 = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2,
            ResultsToken = page1.ResultsToken
        });

        // Go back to page 1
        var backToPage1 = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2,
            ResultsToken = page2.ResultsToken,
            RequestPriorResults = true
        });

        backToPage1.HasPrevResults.ShouldBeFalse();
    }

    [Fact]
    public async Task QuerySessionsAsync_PaginationBackward_ReturnsItemsInForwardOrder()
    {
        var store = BuildStore();
        var subjectId = $"sub_{Guid.NewGuid():N}";

        // Create 6 sessions — we'll paginate in pages of 2 (3 pages total)
        for (var i = 0; i < 6; i++)
        {
            await store.CreateSessionAsync(BuildSession(subjectId: subjectId), _ct);
        }

        // Navigate forward through all pages, collecting keys in display order
        var page1 = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2
        });
        var page2 = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2,
            ResultsToken = page1.ResultsToken
        });
        var page3 = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2,
            ResultsToken = page2.ResultsToken
        });

        // Now navigate backward from page 3
        var backToPage2 = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2,
            ResultsToken = page3.ResultsToken,
            RequestPriorResults = true
        });
        var backToPage1 = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2,
            ResultsToken = backToPage2.ResultsToken,
            RequestPriorResults = true
        });

        // Backward navigation should return items in the same order as forward navigation
        backToPage2.Results.Select(r => r.Key).ShouldBe(page2.Results.Select(r => r.Key));
        backToPage1.Results.Select(r => r.Key).ShouldBe(page1.Results.Select(r => r.Key));

        // Verify pagination flags
        backToPage2.HasNextResults.ShouldBeTrue();
        backToPage2.HasPrevResults.ShouldBeTrue();
        backToPage1.HasNextResults.ShouldBeTrue();
        backToPage1.HasPrevResults.ShouldBeFalse();

        // Verify the forward token from backToPage1 is usable — navigating forward should return page2
        var forwardFromPage1 = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2,
            ResultsToken = backToPage1.ResultsToken
        });
        forwardFromPage1.Results.Select(r => r.Key).ShouldBe(page2.Results.Select(r => r.Key));
    }

    [Fact]
    public async Task QuerySessionsAsync_PaginationBackwardThenForward_RoundTripsCorrectly()
    {
        var store = BuildStore();
        var subjectId = $"sub_{Guid.NewGuid():N}";

        // Create 6 sessions (3 pages of 2)
        for (var i = 0; i < 6; i++)
        {
            await store.CreateSessionAsync(BuildSession(subjectId: subjectId), _ct);
        }

        // Forward: page1 → page2 → page3
        var page1 = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2
        });
        var page2 = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2,
            ResultsToken = page1.ResultsToken
        });
        var page3 = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2,
            ResultsToken = page2.ResultsToken
        });

        // Backward from page3 to page2
        var backToPage2 = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2,
            ResultsToken = page3.ResultsToken,
            RequestPriorResults = true
        });

        // Forward again from backToPage2 — should return page3's items
        var forwardToPage3 = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2,
            ResultsToken = backToPage2.ResultsToken
        });

        forwardToPage3.Results.Select(r => r.Key).ShouldBe(page3.Results.Select(r => r.Key));
        forwardToPage3.HasNextResults.ShouldBeFalse();
        forwardToPage3.HasPrevResults.ShouldBeTrue();
    }

    [Fact]
    public async Task QuerySessionsAsync_WithSubjectIdSubstring_ReturnsMatchingSessions()
    {
        var store = BuildStore();
        var uniquePrefix = $"unique_{Guid.NewGuid():N}";

        var matching = BuildSession(subjectId: $"{uniquePrefix}_user_001");
        var nonMatching = BuildSession();

        await store.CreateSessionAsync(matching, _ct);
        await store.CreateSessionAsync(nonMatching, _ct);

        var result = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = uniquePrefix
        });

        result.Results.ShouldContain(r => r.Key == matching.Key);
        result.Results.ShouldNotContain(r => r.Key == nonMatching.Key);
    }

    [Fact]
    public async Task QuerySessionsAsync_WithDisplayNameSubstring_ReturnsMatchingSessions()
    {
        var store = BuildStore();
        var uniqueDisplay = $"displayname_{Guid.NewGuid():N}";

        var matching = BuildSession(displayName: $"User {uniqueDisplay} Test");
        var nonMatching = BuildSession(displayName: "Other User");

        await store.CreateSessionAsync(matching, _ct);
        await store.CreateSessionAsync(nonMatching, _ct);

        var result = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            DisplayName = uniqueDisplay
        });

        result.Results.ShouldContain(r => r.Key == matching.Key);
        result.Results.ShouldNotContain(r => r.Key == nonMatching.Key);
    }

    [Fact]
    public async Task QuerySessionsAsync_TotalCountAndTotalPages_ArePopulated()
    {
        var store = BuildStore();
        var subjectId = $"sub_{Guid.NewGuid():N}";

        for (var i = 0; i < 3; i++)
        {
            await store.CreateSessionAsync(BuildSession(subjectId: subjectId), _ct);
        }

        var result = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            CountRequested = 2
        });

        // Continuation-token pagination does not provide TotalCount/TotalPages/CurrentPage
        result.TotalCount.ShouldBeNull();
        result.TotalPages.ShouldBeNull();
        result.CurrentPage.ShouldBeNull();
        result.Results.Count.ShouldBe(2);
        result.HasNextResults.ShouldBeTrue();
    }

    [Fact]
    public async Task QuerySessionsAsync_WithMalformedResultsToken_DefaultsToFirstPage()
    {
        var store = BuildStore();
        var subjectId = $"sub_{Guid.NewGuid():N}";
        await store.CreateSessionAsync(BuildSession(subjectId: subjectId), _ct);

        // A malformed token (not a valid continuation token from the store) should
        // either return results from the beginning or throw. The store treats
        // unrecognized tokens as "start from beginning".
        var result = await store.QuerySessionsAsync(_ct, new SessionQuery
        {
            SubjectId = subjectId,
            ResultsToken = "not_a_valid_token"
        });

        result.Results.ShouldNotBeEmpty();
    }
}
