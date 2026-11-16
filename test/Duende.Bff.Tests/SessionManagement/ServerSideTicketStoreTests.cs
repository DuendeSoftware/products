// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestHosts;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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

        // These tests exercise ServerSideTicketStore.RenewAsync's protection against a session cookie
        // (the "bff" auth cookie, whose value is the session store key) being planted into another
        // user's browser: renewing a session whose stored row belongs to a different subject must not
        // write the new ticket into that row, and must remove the row instead.
        //
        // Note: with this test infrastructure's plain OIDC round-trip (no sliding-expiration/short
        // expiry tuning), the very first sign-in attempt after planting the cookie does not yet result
        // in the victim being logged in (see when_signing_in_twice_... below, which performs the sign-in
        // a second time and asserts success). Every variant below asserts the planted/attacker cookie
        // never leads back to the attacker's session, which is the behavior this backport restores.

        [Fact]
        public async Task when_signing_in_with_another_users_session_cookie_expect_that_cookie_to_lead_to_no_session()
        {
            var attackerBrowser = await SignInAsAttackerAsync();
            var victimBrowser = BffHost.BrowserClient;
            PlantBffCookies(from: attackerBrowser, into: victimBrowser);

            await LoginAsAsync(victimBrowser, "alice");

            (await IsLoggedInAsync(attackerBrowser)).Should()
                .BeFalse("the attacker's session cookie must not lead to the victim's session");
        }

        [Fact]
        public async Task when_signing_in_twice_with_another_users_session_cookie_expect_the_second_sign_in_to_succeed()
        {
            var attackerBrowser = await SignInAsAttackerAsync();
            var victimBrowser = BffHost.BrowserClient;
            PlantBffCookies(from: attackerBrowser, into: victimBrowser);
            await LoginAsAsync(victimBrowser, "alice");

            await LoginAsAsync(victimBrowser, "alice");

            (await IsLoggedInAsync(victimBrowser)).Should().BeTrue();
            var victimClaims = await BffHost.CallUserEndpointAsync();
            victimClaims.Single(claim => claim.type == "sub").value.GetString().Should().Be("alice");
            (await IsLoggedInAsync(attackerBrowser)).Should()
                .BeFalse("the attacker's session cookie must not lead to the victim's session");
        }

        [Fact]
        public async Task when_signing_out_and_signing_in_another_user_in_one_request_expect_the_old_session_cookie_to_lead_to_no_session()
        {
            BffHost.OnConfigure += app => app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/sign-out-and-sign-in-as-victim", async context =>
                {
                    var cookieScheme = await context.RequestServices
                        .GetRequiredService<IAuthenticationSchemeProvider>().GetDefaultAuthenticateSchemeAsync();
                    var victim = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim("sub", "alice"), new Claim("sid", "victim-sid") }, "test", "name", "role"));

                    await context.SignOutAsync(cookieScheme.Name);
                    await context.SignInAsync(cookieScheme.Name, victim);
                });
            });
            await BffHost.InitializeAsync();

            var attackerBrowser = await SignInAsAttackerAsync();
            var victimBrowser = BffHost.BrowserClient;
            PlantBffCookies(from: attackerBrowser, into: victimBrowser);

            var signOutAndSignInResponse = await victimBrowser.GetAsync(BffHost.Url("/sign-out-and-sign-in-as-victim"));

            signOutAndSignInResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            (await IsLoggedInAsync(attackerBrowser)).Should()
                .BeFalse("the attacker's session cookie must not lead to the victim's session");
        }

        [Fact]
        public async Task when_the_same_user_signs_in_with_a_new_identity_provider_session_expect_the_session_to_keep_working()
        {
            await BffHost.BffLoginAsync("alice", "sid1");
            (await BffHost.GetIsUserLoggedInAsync()).Should().BeTrue();

            await BffHost.BffLoginAsync("alice", "sid2");

            (await BffHost.GetIsUserLoggedInAsync()).Should().BeTrue();
            var claims = await BffHost.CallUserEndpointAsync();
            claims.Single(c => c.type == "sid").value.GetString().Should().Be("sid2");
        }

        private async Task<TestBrowserClient> SignInAsAttackerAsync()
        {
            var attackerBrowser = new TestBrowserClient(BffHost.Server.CreateHandler());
            await LoginAsAsync(attackerBrowser, "attacker");

            (await IsLoggedInAsync(attackerBrowser)).Should().BeTrue();

            return attackerBrowser;
        }

        /// <summary>
        /// Performs a full OIDC sign-in round-trip for the given browser client, signing in at the
        /// identity server as <paramref name="sub"/>. This does not assert that the browser ends up
        /// logged in: when a previous (unrelated) session's cookie was planted into this browser
        /// client, the round trip can legitimately leave the browser logged out (see
        /// when_signing_in_twice_... above).
        /// </summary>
        private async Task LoginAsAsync(TestBrowserClient bffBrowserClient, string sub, string sid = null)
        {
            await IdentityServerHost.CreateIdentityServerSessionCookieAsync(sub, sid);

            var response = await bffBrowserClient.GetAsync(BffHost.Url("/bff/login"));
            response.StatusCode.Should().Be(HttpStatusCode.Redirect); // authorize

            response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.StatusCode.Should().Be(HttpStatusCode.Redirect); // client callback

            response = await bffBrowserClient.GetAsync(response.Headers.Location.ToString());
            response.StatusCode.Should().Be(HttpStatusCode.Redirect); // root
        }

        private async Task<bool> IsLoggedInAsync(TestBrowserClient bffBrowserClient)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/bff/user"));
            req.Headers.Add("x-csrf", "1");
            var response = await bffBrowserClient.SendAsync(req);

            (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Unauthorized).Should()
                .BeTrue();

            return response.StatusCode == HttpStatusCode.OK;
        }

        private void PlantBffCookies(TestBrowserClient from, TestBrowserClient into)
        {
            var baseUri = new Uri(BffHost.Url());
            foreach (Cookie cookie in from.CookieContainer.GetCookies(baseUri))
            {
                into.CookieContainer.Add(baseUri, new Cookie(cookie.Name, cookie.Value, cookie.Path)
                {
                    Secure = cookie.Secure,
                    HttpOnly = cookie.HttpOnly
                });
            }
        }
    }
}
