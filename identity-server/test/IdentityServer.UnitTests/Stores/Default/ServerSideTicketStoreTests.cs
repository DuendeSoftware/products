// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Validation.Setup;

namespace UnitTests.Stores.Default;

public class ServerSideTicketStoreTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly InMemoryServerSideSessionStore _sessionStore;
    private readonly MockPersistedGrantStore _persistedGrantStore = new();
    private readonly ServerSideTicketStore _subject;

    public ServerSideTicketStoreTests()
    {
        _sessionStore = new InMemoryServerSideSessionStore(_timeProvider);
        _subject = new ServerSideTicketStore(
            new IdentityServerOptions(),
            new TestIssuerNameService(),
            _sessionStore,
            _persistedGrantStore,
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
    public async Task RenewAsync_when_sub_changes_revokes_grants_for_prior_subject_and_removes_the_session()
    {
        var key = await _subject.StoreAsync(CreateTicket("bob", "sid1"));

        await _subject.RenewAsync(key, CreateTicket("alice", "sid2"));

        _persistedGrantStore.RemovedFilters.Count.ShouldBe(1);
        var filter = _persistedGrantStore.RemovedFilters.Single();
        filter.SubjectId.ShouldBe("bob");
        filter.SessionId.ShouldBe("sid1");

        (await _sessionStore.GetSessionAsync(key, default)).ShouldBeNull();
    }

    [Fact]
    public async Task RenewAsync_when_only_sid_changes_revokes_grants_and_updates_the_session_in_place()
    {
        var key = await _subject.StoreAsync(CreateTicket("bob", "sid1"));

        await _subject.RenewAsync(key, CreateTicket("bob", "sid2"));

        _persistedGrantStore.RemovedFilters.Count.ShouldBe(1);
        var filter = _persistedGrantStore.RemovedFilters.Single();
        filter.SubjectId.ShouldBe("bob");
        filter.SessionId.ShouldBe("sid1");

        var session = await _sessionStore.GetSessionAsync(key, default);
        session.ShouldNotBeNull();
        session.SubjectId.ShouldBe("bob");
        session.SessionId.ShouldBe("sid2");
    }

    [Fact]
    public async Task RenewAsync_when_sub_and_sid_unchanged_does_not_revoke_grants()
    {
        var key = await _subject.StoreAsync(CreateTicket("bob", "sid1"));

        await _subject.RenewAsync(key, CreateTicket("bob", "sid1"));

        _persistedGrantStore.RemovedFilters.ShouldBeEmpty();

        var session = await _sessionStore.GetSessionAsync(key, default);
        session.ShouldNotBeNull();
    }

    [Fact]
    public async Task RenewAsync_for_unknown_key_does_not_create_a_session_and_does_not_revoke_grants()
    {
        await _subject.RenewAsync("unknown-key", CreateTicket("bob", "sid1"));

        _persistedGrantStore.RemovedFilters.ShouldBeEmpty();

        var session = await _sessionStore.GetSessionAsync("unknown-key", default);
        session.ShouldBeNull();
    }
}
