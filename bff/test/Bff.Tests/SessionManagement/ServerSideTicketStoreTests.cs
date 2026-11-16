// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestHosts;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.SessionManagement;

public class ServerSideTicketStoreTests : BffIntegrationTestBase
{
    readonly InMemoryUserSessionStore _sessionStore = new();

    public ServerSideTicketStoreTests(ITestOutputHelper output) : base(output)
    {
        BffHost.OnConfigureServices += services =>
        {
            services.AddSingleton<IUserSessionStore>(_sessionStore);
        };

        BffHost.OnConfigure += app => app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/sign-out-and-sign-in-as-victim", async context =>
            {
                var cookieScheme = await context.RequestServices
                    .GetRequiredService<IAuthenticationSchemeProvider>()
                    .GetDefaultAuthenticateSchemeAsync();

                var victim = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(JwtClaimTypes.Subject, "alice"), new Claim(JwtClaimTypes.SessionId, "victim-sid")],
                    "test", "name", "role"));

                await context.SignOutAsync(cookieScheme!.Name);
                await context.SignInAsync(cookieScheme.Name, victim);
            });
        });
    }

    [Fact]
    public async Task StoreAsync_should_remove_conflicting_entries_prior_to_creating_new_entry()
    {
        await BffHost.BffLoginAsync("alice");

        BffHost.BrowserClient.RemoveCookie("bff");
        (await _sessionStore.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "alice" })).Count().ShouldBe(1);

        await BffHost.BffOidcLoginAsync();

        (await _sessionStore.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "alice" })).Count().ShouldBe(1);
    }

    [Fact]
    public async Task when_signing_in_with_another_users_session_cookie_expect_that_cookie_to_lead_to_no_session()
    {
        var attackerBrowser = await SignInAsAttacker();
        var victimBrowser = BffHost.BrowserClient;
        PlantBffCookies(from: attackerBrowser, into: victimBrowser);

        _ = await BffHost.BffLoginAsyncWithoutAssertingSuccess(victimBrowser, "alice");

        var attackerCookieStillSignedIn = await BffHost.GetIsUserLoggedInAsync(attackerBrowser);
        attackerCookieStillSignedIn.ShouldBeFalse("the planted session cookie must not lead to another user's session");
    }

    [Fact]
    public async Task when_signing_in_twice_with_another_users_session_cookie_expect_the_second_sign_in_to_succeed()
    {
        var attackerBrowser = await SignInAsAttacker();
        var victimBrowser = BffHost.BrowserClient;
        PlantBffCookies(from: attackerBrowser, into: victimBrowser);
        _ = await BffHost.BffLoginAsyncWithoutAssertingSuccess(victimBrowser, "alice");

        _ = await BffHost.BffLoginAsync(victimBrowser, "alice");

        var victimClaims = await BffHost.CallUserEndpointAsync(victimBrowser);
        victimClaims.Single(claim => claim.Type == "sub").Value.GetString().ShouldBe("alice");
        var attackerCookieStillSignedIn = await BffHost.GetIsUserLoggedInAsync(attackerBrowser);
        attackerCookieStillSignedIn.ShouldBeFalse("the planted session cookie must not lead to another user's session");
    }

    [Fact]
    public async Task when_signing_out_and_signing_in_another_user_in_one_request_expect_the_old_session_cookie_to_lead_to_no_session()
    {
        var attackerBrowser = await SignInAsAttacker();
        PlantBffCookies(from: attackerBrowser, into: BffHost.BrowserClient);

        var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/sign-out-and-sign-in-as-victim"));
        var signOutAndSignInResponse = await BffHost.BrowserClient.SendAsync(req);

        signOutAndSignInResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        var attackerCookieStillSignedIn = await BffHost.GetIsUserLoggedInAsync(attackerBrowser);
        attackerCookieStillSignedIn.ShouldBeFalse("the planted session cookie must not lead to the other user's session");
    }

    [Fact]
    public async Task when_the_same_user_signs_in_with_a_new_identity_provider_session_expect_the_session_to_keep_working()
    {
        await BffHost.BffLoginAsync("alice", "sid-1");
        var firstSessions = await _sessionStore.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "alice" });
        firstSessions.Single().SessionId.ShouldBe("sid-1");

        await BffHost.BffLoginAsync("alice", "sid-2");

        var secondSessions = await _sessionStore.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "alice" });
        secondSessions.Single().SessionId.ShouldBe("sid-2");
        (await BffHost.GetIsUserLoggedInAsync()).ShouldBeTrue();
    }

    private async Task<TestBrowserClient> SignInAsAttacker()
    {
        var attackerBrowser = BffHost.BuildBrowserClient();
        await BffHost.BffLoginAsync(attackerBrowser, "attacker");
        return attackerBrowser;
    }

    private void PlantBffCookies(TestBrowserClient from, TestBrowserClient into)
    {
        foreach (System.Net.Cookie cookie in from.Cookies.GetCookies(new Uri(BffHost.Url())))
        {
            into.Cookies.Add(new Uri(BffHost.Url()), new System.Net.Cookie(cookie.Name, cookie.Value, cookie.Path)
            {
                Secure = cookie.Secure,
                HttpOnly = cookie.HttpOnly
            });
        }
    }
}

public class ServerSideTicketStoreRenewAsyncTests
{
    private readonly InMemoryUserSessionStore _sessionStore = new();
    private readonly ServerSideTicketStore _sut;

    public ServerSideTicketStoreRenewAsyncTests() => _sut = new ServerSideTicketStore(
        _sessionStore,
        new EphemeralDataProtectionProvider(),
        NullLogger<ServerSideTicketStore>.Instance);

    private static AuthenticationTicket CreateTicket(string sub, string sid) => new(
        new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", sub), new Claim("sid", sid)], "test", "name", "role")),
        new AuthenticationProperties(),
        "test");

    [Fact]
    public async Task RenewAsync_when_session_is_missing_should_not_write_a_new_session()
    {
        await _sut.RenewAsync("missing-key", CreateTicket("alice", "sid1"));

        (await _sessionStore.GetUserSessionAsync("missing-key")).ShouldBeNull();
        (await _sessionStore.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "alice" })).ShouldBeEmpty();
    }

    [Fact]
    public async Task RenewAsync_when_session_belongs_to_another_subject_should_remove_the_session_and_not_write_the_new_ticket()
    {
        var key = await _sut.StoreAsync(CreateTicket("alice", "sid1"));

        await _sut.RenewAsync(key, CreateTicket("bob", "sid2"));

        (await _sessionStore.GetUserSessionAsync(key)).ShouldBeNull();
        (await _sessionStore.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "alice" })).ShouldBeEmpty();
        (await _sessionStore.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "bob" })).ShouldBeEmpty();
    }

    [Fact]
    public async Task RenewAsync_when_session_belongs_to_the_same_subject_with_a_new_session_id_should_still_renew()
    {
        var key = await _sut.StoreAsync(CreateTicket("alice", "sid1"));

        await _sut.RenewAsync(key, CreateTicket("alice", "sid2"));

        var session = await _sessionStore.GetUserSessionAsync(key);
        session.ShouldNotBeNull();
        session.SubjectId.ShouldBe("alice");
        session.SessionId.ShouldBe("sid2");
    }
}
