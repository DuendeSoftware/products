// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.IntegrationTests.Stores;

/// <summary>
/// Abstract contract tests for IServerSideSessionStore implementations.
/// Each derived class provides a specific store backend (EF, Storage, InMemory).
/// </summary>
public abstract class ServerSideSessionStoreContractTests : IAsyncLifetime
{
    protected readonly Ct _ct = TestContext.Current.CancellationToken;

    /// <summary>
    /// Creates a store instance that operates against the shared test database.
    /// Callers must dispose the returned <see cref="StoreHandle{T}"/> after use.
    /// </summary>
    protected abstract StoreHandle<IServerSideSessionStore> CreateStore();

    /// <summary>
    /// Lifecycle hook for derived classes that need async initialization.
    /// </summary>
    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Lifecycle hook for derived classes that need async cleanup.
    /// </summary>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected static ServerSideSession CreateTestSession(
        string? key = null,
        string? subjectId = null,
        string? sessionId = null,
        string? scheme = null,
        string? displayName = null,
        DateTime? expires = null,
        string? ticket = null) =>
        new()
        {
            Key = key ?? Guid.NewGuid().ToString("N"),
            SubjectId = subjectId ?? $"sub_{Guid.NewGuid():N}",
            SessionId = sessionId ?? $"sid_{Guid.NewGuid():N}",
            Scheme = scheme ?? "cookie",
            DisplayName = displayName ?? "Test User",
            Created = DateTime.UtcNow,
            Renewed = DateTime.UtcNow,
            Expires = expires,
            Ticket = ticket ?? $"ticket_{Guid.NewGuid():N}"
        };

    [Fact]
    public async Task CreateSessionAsync_ThenGetSessionAsync_ReturnsSession()
    {
        await using var handle = CreateStore();
        var session = CreateTestSession(expires: DateTime.UtcNow.AddHours(1));

        await handle.Store.CreateSessionAsync(session, _ct);

        var result = await handle.Store.GetSessionAsync(session.Key, _ct);

        result.ShouldNotBeNull();
        result.Key.ShouldBe(session.Key);
        result.SubjectId.ShouldBe(session.SubjectId);
        result.SessionId.ShouldBe(session.SessionId);
        result.Scheme.ShouldBe(session.Scheme);
        result.DisplayName.ShouldBe(session.DisplayName);
        result.Ticket.ShouldBe(session.Ticket);
    }

    [Fact]
    public async Task GetSessionAsync_WhenNotExists_ReturnsNull()
    {
        await using var handle = CreateStore();

        var result = await handle.Store.GetSessionAsync(Guid.NewGuid().ToString("N"), _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateSessionAsync_UpdatesExistingSession()
    {
        await using var handle = CreateStore();
        var session = CreateTestSession(expires: DateTime.UtcNow.AddHours(1));
        await handle.Store.CreateSessionAsync(session, _ct);

        session.Ticket = "updated_ticket";
        session.Renewed = DateTime.UtcNow.AddMinutes(5);
        var expectedRenewed = session.Renewed;
        await handle.Store.UpdateSessionAsync(session, _ct);

        var result = await handle.Store.GetSessionAsync(session.Key, _ct);

        result.ShouldNotBeNull();
        result.Ticket.ShouldBe("updated_ticket");
        result.Renewed.ShouldBeCloseTo(expectedRenewed, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task DeleteSessionAsync_RemovesSession()
    {
        await using var handle = CreateStore();
        var session = CreateTestSession(expires: DateTime.UtcNow.AddHours(1));
        await handle.Store.CreateSessionAsync(session, _ct);

        await handle.Store.DeleteSessionAsync(session.Key, _ct);

        var result = await handle.Store.GetSessionAsync(session.Key, _ct);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetSessionsAsync_BySubjectId_ReturnsMatchingSessions()
    {
        await using var handle = CreateStore();
        var subjectId = $"sub_{Guid.NewGuid():N}";
        var session1 = CreateTestSession(subjectId: subjectId, expires: DateTime.UtcNow.AddHours(1));
        var session2 = CreateTestSession(subjectId: subjectId, expires: DateTime.UtcNow.AddHours(1));
        var session3 = CreateTestSession(expires: DateTime.UtcNow.AddHours(1)); // different subject

        await handle.Store.CreateSessionAsync(session1, _ct);
        await handle.Store.CreateSessionAsync(session2, _ct);
        await handle.Store.CreateSessionAsync(session3, _ct);

        var results = await handle.Store.GetSessionsAsync(new SessionFilter { SubjectId = subjectId }, _ct);

        results.Count.ShouldBe(2);
        results.ShouldAllBe(s => s.SubjectId == subjectId);
    }

    [Fact]
    public async Task GetSessionsAsync_BySessionId_ReturnsMatchingSession()
    {
        await using var handle = CreateStore();
        var session = CreateTestSession(expires: DateTime.UtcNow.AddHours(1));
        var other = CreateTestSession(expires: DateTime.UtcNow.AddHours(1));

        await handle.Store.CreateSessionAsync(session, _ct);
        await handle.Store.CreateSessionAsync(other, _ct);

        var results = await handle.Store.GetSessionsAsync(new SessionFilter { SessionId = session.SessionId }, _ct);

        results.Count.ShouldBe(1);
        results.First().SessionId.ShouldBe(session.SessionId);
    }

    [Fact]
    public async Task DeleteSessionsAsync_BySubjectId_RemovesMatchingSessions()
    {
        await using var handle = CreateStore();
        var subjectId = $"sub_{Guid.NewGuid():N}";
        var session1 = CreateTestSession(subjectId: subjectId, expires: DateTime.UtcNow.AddHours(1));
        var session2 = CreateTestSession(subjectId: subjectId, expires: DateTime.UtcNow.AddHours(1));
        var session3 = CreateTestSession(expires: DateTime.UtcNow.AddHours(1));

        await handle.Store.CreateSessionAsync(session1, _ct);
        await handle.Store.CreateSessionAsync(session2, _ct);
        await handle.Store.CreateSessionAsync(session3, _ct);

        await handle.Store.DeleteSessionsAsync(new SessionFilter { SubjectId = subjectId }, _ct);

        var results = await handle.Store.GetSessionsAsync(new SessionFilter { SubjectId = subjectId }, _ct);
        results.ShouldBeEmpty();

        // Other session should still exist
        var other = await handle.Store.GetSessionAsync(session3.Key, _ct);
        other.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAndRemoveExpiredSessionsAsync_RemovesExpiredOnly()
    {
        await using var handle = CreateStore();
        var expired1 = CreateTestSession(expires: DateTime.UtcNow.AddDays(-1));
        var expired2 = CreateTestSession(expires: DateTime.UtcNow.AddDays(-1));
        var active = CreateTestSession(expires: DateTime.UtcNow.AddDays(1));

        await handle.Store.CreateSessionAsync(expired1, _ct);
        await handle.Store.CreateSessionAsync(expired2, _ct);
        await handle.Store.CreateSessionAsync(active, _ct);

        // Some implementations auto-purge expired records before this call returns them;
        // others return the expired sessions in the result. Either way, the expired sessions
        // must no longer be retrievable after this call.
        _ = await handle.Store.GetAndRemoveExpiredSessionsAsync(10, _ct);
        var e1 = await handle.Store.GetSessionAsync(expired1.Key, _ct);
        var e2 = await handle.Store.GetSessionAsync(expired2.Key, _ct);
        e1.ShouldBeNull();
        e2.ShouldBeNull();

        // Active session should still exist
        var result = await handle.Store.GetSessionAsync(active.Key, _ct);
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task QuerySessionsAsync_ReturnsFirstPage()
    {
        await using var handle = CreateStore();
        for (var i = 0; i < 5; i++)
        {
            await handle.Store.CreateSessionAsync(CreateTestSession(expires: DateTime.UtcNow.AddHours(1)), _ct);
        }

        var result = await handle.Store.QuerySessionsAsync(_ct, new SessionQuery { CountRequested = 2 });

        result.ShouldNotBeNull();
        result.Results.ShouldNotBeNull();
        result.Results.Count.ShouldBeInRange(1, 2);
        result.HasNextResults.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateSessionAsync_WhenTheSessionWasSavedSinceItWasRead_IsRefusedAndKeepsTheNewerSession()
    {
        await using var handle = CreateStore();
        var aliceSession = CreateTestSession(expires: DateTime.UtcNow.AddHours(1));
        await handle.Store.CreateSessionAsync(aliceSession, _ct);
        var tokenCallSessionRead = await handle.Store.GetSessionAsync(aliceSession.Key, _ct);

        var browserSessionSave = await handle.Store.GetSessionAsync(aliceSession.Key, _ct);
        browserSessionSave!.Ticket = "browser_ticket";
        await handle.Store.UpdateSessionAsync(browserSessionSave, _ct);
        tokenCallSessionRead!.Ticket = "token_call_ticket";

        var tokenCallSaveAccepted = await handle.Store.UpdateSessionAsync(tokenCallSessionRead, _ct);

        tokenCallSaveAccepted.ShouldBeFalse("a save based on a read older than the stored session must be refused");
        var storedSession = await handle.Store.GetSessionAsync(aliceSession.Key, _ct);
        storedSession!.Ticket.ShouldBe("browser_ticket", "the refused save must leave the newer session in the store");
    }

    [Fact]
    public async Task UpdateSessionAsync_WhenTheSessionWasDeletedSinceItWasRead_IsRefusedAndDoesNotRecreateIt()
    {
        await using var handle = CreateStore();
        var aliceSession = CreateTestSession(expires: DateTime.UtcNow.AddHours(1));
        await handle.Store.CreateSessionAsync(aliceSession, _ct);
        var tokenCallSessionRead = await handle.Store.GetSessionAsync(aliceSession.Key, _ct);
        await handle.Store.DeleteSessionAsync(aliceSession.Key, _ct);

        var tokenCallSaveAfterSignOutAccepted = await handle.Store.UpdateSessionAsync(tokenCallSessionRead!, _ct);

        tokenCallSaveAfterSignOutAccepted.ShouldBeFalse("a save of a deleted session must be refused");
        var storedSession = await handle.Store.GetSessionAsync(aliceSession.Key, _ct);
        storedSession.ShouldBeNull("a refused save must not bring back a deleted session");
    }

    [Fact]
    public async Task UpdateSessionAsync_WhenTheSessionWasNotReadFromTheStore_SavesWithoutTheVersionCheck()
    {
        await using var handle = CreateStore();
        var aliceSession = CreateTestSession(expires: DateTime.UtcNow.AddHours(1));
        await handle.Store.CreateSessionAsync(aliceSession, _ct);
        var replacementBuiltWithoutRead = CreateTestSession(key: aliceSession.Key, subjectId: aliceSession.SubjectId, sessionId: aliceSession.SessionId, expires: aliceSession.Expires, ticket: "replacement_ticket");

        var replacementAccepted = await handle.Store.UpdateSessionAsync(replacementBuiltWithoutRead, _ct);

        replacementAccepted.ShouldBeTrue("a session that was not read from the store has version 0 and is saved without the check");
        var storedSession = await handle.Store.GetSessionAsync(aliceSession.Key, _ct);
        storedSession!.Ticket.ShouldBe("replacement_ticket", "a session not read from the store must be saved as given");
    }

    [Fact]
    public async Task DeleteSessionAsync_WhenTheTokenCallStoreSavedTheSessionAfterTheSignOutStore_RemovesSession()
    {
        await using var signOutHandle = CreateStore();
        await using var tokenCallHandle = CreateStore();
        var aliceSession = CreateTestSession(expires: DateTime.UtcNow.AddHours(1));
        await signOutHandle.Store.CreateSessionAsync(aliceSession, _ct);

        var signOutStoreSessionSave = await signOutHandle.Store.GetSessionAsync(aliceSession.Key, _ct);
        var signOutStoreSaveAccepted = await signOutHandle.Store.UpdateSessionAsync(signOutStoreSessionSave!, _ct);
        var tokenCallStoreSessionSave = await tokenCallHandle.Store.GetSessionAsync(aliceSession.Key, _ct);
        var tokenCallStoreSaveAccepted = await tokenCallHandle.Store.UpdateSessionAsync(tokenCallStoreSessionSave!, _ct);

        signOutStoreSaveAccepted.ShouldBeTrue("precondition: the sign-out store saves the session");
        tokenCallStoreSaveAccepted.ShouldBeTrue("precondition: the token call's store saves the session after the sign-out store did");

        await signOutHandle.Store.DeleteSessionAsync(aliceSession.Key, _ct);

        var storedSession = await tokenCallHandle.Store.GetSessionAsync(aliceSession.Key, _ct);
        storedSession.ShouldBeNull("sign-out must delete the session even when the token call's store saved it after the sign-out store did");
    }

    [Fact]
    public async Task DeleteSessionsAsync_WhenTheTokenCallStoreSavedTheSessionAfterTheSignOutStore_RemovesMatchingSessions()
    {
        await using var signOutHandle = CreateStore();
        await using var tokenCallHandle = CreateStore();
        var aliceSession = CreateTestSession(expires: DateTime.UtcNow.AddHours(1));
        await signOutHandle.Store.CreateSessionAsync(aliceSession, _ct);

        var signOutStoreSessionSave = await signOutHandle.Store.GetSessionAsync(aliceSession.Key, _ct);
        var signOutStoreSaveAccepted = await signOutHandle.Store.UpdateSessionAsync(signOutStoreSessionSave!, _ct);
        var tokenCallStoreSessionSave = await tokenCallHandle.Store.GetSessionAsync(aliceSession.Key, _ct);
        var tokenCallStoreSaveAccepted = await tokenCallHandle.Store.UpdateSessionAsync(tokenCallStoreSessionSave!, _ct);

        signOutStoreSaveAccepted.ShouldBeTrue("precondition: the sign-out store saves the session");
        tokenCallStoreSaveAccepted.ShouldBeTrue("precondition: the token call's store saves the session after the sign-out store did");

        await signOutHandle.Store.DeleteSessionsAsync(new SessionFilter { SubjectId = aliceSession.SubjectId }, _ct);

        var storedSession = await tokenCallHandle.Store.GetSessionAsync(aliceSession.Key, _ct);
        storedSession.ShouldBeNull("removing a user's sessions must delete them even when the token call's store saved one after the sign-out store did");
    }
}
