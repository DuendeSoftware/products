// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestHosts;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace Duende.Bff.Tests.SessionManagement
{
    public class ServerSideTicketStoreTests : BffIntegrationTestBase
    {
        InMemoryUserSessionStore _sessionStore = new InMemoryUserSessionStore();

        public ServerSideTicketStoreTests()
        {
            BffHost.OnConfigureServices += services =>
            {
                services.AddSingleton<IUserSessionStore>(_sessionStore);
            };
            BffHost.InitializeAsync().Wait();
        }

        [Fact]
        public async Task StoreAsync_should_remove_conflicting_entries_prior_to_creating_new_entry()
        {
            await BffHost.BffLoginAsync("alice");

            BffHost.BrowserClient.RemoveCookie("bff");
            (await _sessionStore.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "alice" })).Count().Should().Be(1);
            
            await BffHost.BffOidcLoginAsync();

            (await _sessionStore.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "alice" })).Count().Should().Be(1);
        }

        [Fact]
        public async Task when_signing_in_with_another_users_session_cookie_expect_that_cookie_to_lead_to_no_session()
        {
            var attackerBrowser = await SignInAsAttackerAsync();
            var victimBrowser = BffHost.BrowserClient;
            PlantBffCookies(from: attackerBrowser, into: victimBrowser);

            await DoLoginAsync(victimBrowser, "alice");

            (await IsUserLoggedInAsync(attackerBrowser)).Should()
                .BeFalse("the attacker's session cookie must not lead to the victim's session");
        }

        [Fact]
        public async Task when_signing_in_twice_with_another_users_session_cookie_expect_the_second_sign_in_to_succeed()
        {
            var attackerBrowser = await SignInAsAttackerAsync();
            var victimBrowser = BffHost.BrowserClient;
            PlantBffCookies(from: attackerBrowser, into: victimBrowser);

            // The cookie handler's sign-in/sign-out within a single request leaves the previous
            // request's authentication state cached, so the first sign-in after the planted
            // cookie's session is removed from the store does not result in a usable session;
            // signing in again succeeds and establishes a fresh session. This is a result of the
            // fix itself: on the first sign-in, RenewAsync removes the attacker's session, so the
            // reissued cookie points to a session that no longer exists, and only the second
            // sign-in stores a new one.
            await DoLoginAsync(victimBrowser, "alice");
            await DoLoginAsync(victimBrowser, "alice");

            (await IsUserLoggedInAsync(victimBrowser)).Should().BeTrue();
            (await IsUserLoggedInAsync(attackerBrowser)).Should()
                .BeFalse("the attacker's session cookie must not lead to the victim's session");
        }

        [Fact]
        public async Task when_the_same_user_signs_in_with_a_new_identity_provider_session_expect_the_session_to_keep_working()
        {
            await DoLoginAsync(BffHost.BrowserClient, "alice", "sid1");
            (await IsUserLoggedInAsync(BffHost.BrowserClient)).Should().BeTrue();

            var firstSessionKey = (await _sessionStore.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "alice" }))
                .Single().Key;

            await DoLoginAsync(BffHost.BrowserClient, "alice", "sid2");

            (await IsUserLoggedInAsync(BffHost.BrowserClient)).Should().BeTrue();

            var sessions = await _sessionStore.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "alice" });
            sessions.Count.Should().Be(1);
            var session = sessions.Single();
            session.Key.Should().Be(firstSessionKey, "the same user's session should be renewed in place, not replaced");
            session.SessionId.Should().Be("sid2");
        }

        [Fact]
        public async Task when_signing_out_and_signing_in_another_user_in_one_request_expect_the_old_session_cookie_to_lead_to_no_session()
        {
            BffHost.OnConfigure += app =>
            {
                app.Use(async (HttpContext ctx, Func<Task> next) =>
                {
                    if (ctx.Request.Path == "/sign-out-and-sign-in-as-victim")
                    {
                        var victim = new ClaimsPrincipal(new ClaimsIdentity(
                            new[] { new Claim("sub", "alice"), new Claim("sid", "victim-sid") }, "test", "name", "role"));

                        await ctx.SignOutAsync("cookie");
                        await ctx.SignInAsync("cookie", victim);
                        ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                        return;
                    }

                    await next();
                });
            };
            BffHost.InitializeAsync().Wait();

            var attackerBrowser = await SignInAsAttackerAsync();
            var victimBrowser = BffHost.BrowserClient;
            PlantBffCookies(from: attackerBrowser, into: victimBrowser);

            var signOutAndSignInResponse = await victimBrowser.GetAsync(BffHost.Url("/sign-out-and-sign-in-as-victim"));

            signOutAndSignInResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            (await IsUserLoggedInAsync(attackerBrowser)).Should()
                .BeFalse("the attacker's session cookie must not lead to the victim's session");
        }

        /// <summary>
        /// Focused unit test of ServerSideTicketStore.RenewAsync for the sign-out-and-sign-in-as-another-user
        /// scenario, in addition to the end-to-end test above.
        /// </summary>
        [Fact]
        public async Task RenewAsync_when_stored_session_belongs_to_another_user_should_remove_it_and_not_write_the_new_ticket()
        {
            var store = new InMemoryUserSessionStore();
            var protector = new EphemeralDataProtectionProvider();
            var ticketStore = new ServerSideTicketStore(store, protector, NullLogger<ServerSideTicketStore>.Instance);

            var attackerTicket = CreateTicket("attacker", "attacker-sid");
            var key = await ticketStore.StoreAsync(attackerTicket);

            var victimTicket = CreateTicket("alice", "victim-sid");
            await ticketStore.RenewAsync(key, victimTicket);

            (await store.GetUserSessionAsync(key)).Should().BeNull("the other user's session must be removed, not overwritten");
            (await ticketStore.RetrieveAsync(key)).Should().BeNull();
        }

        [Fact]
        public async Task RenewAsync_when_session_is_missing_should_not_write_a_new_session()
        {
            var store = new InMemoryUserSessionStore();
            var protector = new EphemeralDataProtectionProvider();
            var ticketStore = new ServerSideTicketStore(store, protector, NullLogger<ServerSideTicketStore>.Instance);

            var ticket = CreateTicket("alice", "sid1");

            await ticketStore.RenewAsync("missing-key", ticket);

            (await store.GetUserSessionAsync("missing-key")).Should().BeNull();
        }

        private static AuthenticationTicket CreateTicket(string sub, string sid)
        {
            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim("sub", sub),
                    new Claim("sid", sid)
                }, "test", "name", "role");
            var principal = new ClaimsPrincipal(identity);
            return new AuthenticationTicket(principal, new AuthenticationProperties(), "cookie");
        }

        private async Task<TestBrowserClient> SignInAsAttackerAsync()
        {
            var attackerBrowser = new TestBrowserClient(BffHost.Server.CreateHandler());
            await DoLoginAsync(attackerBrowser, "attacker");
            (await IsUserLoggedInAsync(attackerBrowser)).Should().BeTrue();
            return attackerBrowser;
        }

        private void PlantBffCookies(TestBrowserClient from, TestBrowserClient into)
        {
            foreach (Cookie cookie in from.CookieContainer.GetCookies(new Uri(BffHost.Url())))
            {
                into.CookieContainer.Add(new Uri(BffHost.Url()),
                    new Cookie(cookie.Name, cookie.Value, cookie.Path) { Secure = cookie.Secure, HttpOnly = cookie.HttpOnly });
            }
        }

        /// <summary>
        /// Performs the full BFF OIDC login redirect chain using the given browser client, without
        /// asserting that the resulting session is actually usable (unlike BffHost.BffOidcLoginAsync).
        /// This is needed because, with the fix, a sign-in that reuses another user's stale session
        /// cookie key is expected to leave the user effectively signed out until they sign in again.
        /// </summary>
        private async Task<HttpResponseMessage> DoLoginAsync(TestBrowserClient client, string sub, string sid = null)
        {
            await IdentityServerHost.CreateIdentityServerSessionCookieAsync(sub, sid);

            var response = await client.GetAsync(BffHost.Url("/bff/login"));
            response.StatusCode.Should().Be(HttpStatusCode.Redirect); // authorize

            response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.StatusCode.Should().Be(HttpStatusCode.Redirect); // client callback

            response = await client.GetAsync(response.Headers.Location.ToString());
            response.StatusCode.Should().Be(HttpStatusCode.Redirect); // root

            response = await client.GetAsync(BffHost.Url(response.Headers.Location.ToString()));
            return response;
        }

        private async Task<bool> IsUserLoggedInAsync(TestBrowserClient client, string userQuery = null)
        {
            if (userQuery != null) userQuery = "?" + userQuery;

            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/bff/user") + userQuery);
            req.Headers.Add("x-csrf", "1");
            var response = await client.SendAsync(req);

            (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Unauthorized).Should()
                .BeTrue();

            return response.StatusCode == HttpStatusCode.OK;
        }
    }
}
