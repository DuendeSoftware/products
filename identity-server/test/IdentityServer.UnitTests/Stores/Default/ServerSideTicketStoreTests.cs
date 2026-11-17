// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Common;
using UnitTests.Validation.Setup;

namespace UnitTests.Stores.Default;

public class ServerSideTicketStoreTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly InMemoryServerSideSessionStore _sessionStore;
    private readonly MockSessionReplacementService _replacementService = new();
    private readonly ServerSideTicketStore _subject;

    public ServerSideTicketStoreTests()
    {
        _sessionStore = new InMemoryServerSideSessionStore(_timeProvider);
        _subject = new ServerSideTicketStore(
            new IdentityServerOptions(),
            new TestIssuerNameService(),
            _sessionStore,
            _replacementService,
            new EphemeralDataProtectionProvider(),
            new HttpContextAccessor(),
            NullLogger<ServerSideTicketStore>.Instance,
            _timeProvider);
    }

    private AuthenticationTicket CreateTicket(string sub, string sid, string clientId = "client1")
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Subject, sub)
        ], "test"));

        var properties = new AuthenticationProperties
        {
            IssuedUtc = _timeProvider.GetUtcNow(),
            ExpiresUtc = _timeProvider.GetUtcNow().AddHours(1)
        };
        properties.SetSessionId(sid);
        properties.AddClientId(clientId);

        return new AuthenticationTicket(principal, properties, "idsrv");
    }

    [Fact]
    public async Task RenewAsync_when_sub_and_sid_both_change_invokes_replacement_with_expected_context_and_removes_the_session()
    {
        var key = await _subject.StoreAsync(CreateTicket("bob", "sid1"));

        await _subject.RenewAsync(key, CreateTicket("alice", "sid2"));

        _replacementService.Calls.Count.ShouldBe(1);
        var call = _replacementService.Calls.Single();
        call.PreviousSubjectId.ShouldBe("bob");
        call.PreviousSessionId.ShouldBe("sid1");
        call.PreviousSession.ShouldNotBeNull();
        call.PreviousSession.SubjectId.ShouldBe("bob");
        call.PreviousSession.SessionId.ShouldBe("sid1");
        call.PreviousSession.ClientIds.ShouldContain("client1");
        call.NewSubjectId.ShouldBe("alice");
        call.NewSessionId.ShouldBe("sid2");

        (await _sessionStore.GetSessionAsync(key, Ct.None)).ShouldBeNull();
    }

    [Fact]
    public async Task RenewAsync_when_only_sid_changes_invokes_replacement_with_expected_context()
    {
        var key = await _subject.StoreAsync(CreateTicket("bob", "sid1"));

        await _subject.RenewAsync(key, CreateTicket("bob", "sid2"));

        _replacementService.Calls.Count.ShouldBe(1);
        var call = _replacementService.Calls.Single();
        call.PreviousSubjectId.ShouldBe("bob");
        call.PreviousSessionId.ShouldBe("sid1");
        call.PreviousSession.ShouldNotBeNull();
        call.PreviousSession.SubjectId.ShouldBe("bob");
        call.PreviousSession.SessionId.ShouldBe("sid1");
        call.NewSubjectId.ShouldBe("bob");
        call.NewSessionId.ShouldBe("sid2");
    }

    [Fact]
    public async Task RenewAsync_when_sub_and_sid_unchanged_does_not_invoke_replacement()
    {
        var key = await _subject.StoreAsync(CreateTicket("bob", "sid1"));

        await _subject.RenewAsync(key, CreateTicket("bob", "sid1"));

        _replacementService.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task RenewAsync_for_unknown_key_does_not_invoke_replacement()
    {
        await _subject.RenewAsync("unknown-key", CreateTicket("bob", "sid1"));

        _replacementService.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task RenewAsync_when_previous_ticket_cannot_be_deserialized_invokes_replacement_with_row_data_and_null_PreviousSession()
    {
        const string key = "unreadable-ticket-key";
        await _sessionStore.CreateSessionAsync(new ServerSideSession
        {
            Key = key,
            Scheme = "idsrv",
            SubjectId = "bob",
            SessionId = "sid1",
            Created = _timeProvider.GetUtcNow().UtcDateTime,
            Renewed = _timeProvider.GetUtcNow().UtcDateTime,
            Expires = _timeProvider.GetUtcNow().AddHours(1).UtcDateTime,
            Ticket = "not-a-valid-protected-ticket"
        }, Ct.None);

        await _subject.RenewAsync(key, CreateTicket("alice", "sid2"));

        _replacementService.Calls.Count.ShouldBe(1);
        var call = _replacementService.Calls.Single();
        call.PreviousSubjectId.ShouldBe("bob");
        call.PreviousSessionId.ShouldBe("sid1");
        call.PreviousSession.ShouldBeNull();
        call.NewSubjectId.ShouldBe("alice");
        call.NewSessionId.ShouldBe("sid2");
    }

    [Fact]
    public async Task RetrieveAsync_when_stored_ticket_cannot_be_deserialized_returns_null_and_deletes_record()
    {
        const string key = "bad-key";
        await _sessionStore.CreateSessionAsync(new ServerSideSession
        {
            Key = key,
            Scheme = "idsrv",
            SubjectId = "bob",
            SessionId = "sid1",
            Created = _timeProvider.GetUtcNow().UtcDateTime,
            Renewed = _timeProvider.GetUtcNow().UtcDateTime,
            Expires = _timeProvider.GetUtcNow().AddHours(1).UtcDateTime,
            Ticket = "not-a-valid-protected-ticket"
        }, Ct.None);

        var ticket = await _subject.RetrieveAsync(key);

        ticket.ShouldBeNull();
        var stored = await _sessionStore.GetSessionAsync(key, Ct.None);
        stored.ShouldBeNull();
    }
}
