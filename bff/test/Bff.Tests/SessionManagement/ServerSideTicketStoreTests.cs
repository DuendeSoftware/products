// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestHosts;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.SessionManagement
{
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
            PlantBffCookies(from: attackerBrowser, into: BffHost.BrowserClient);

            await AttemptBffLoginAsync("alice");

            var attackerCookieStillSignedIn = await GetIsUserLoggedInAsync(attackerBrowser);
            attackerCookieStillSignedIn.ShouldBeFalse("the planted session cookie must not lead to the other user's session");
        }

        [Fact]
        public async Task when_signing_in_twice_with_another_users_session_cookie_expect_the_second_sign_in_to_succeed()
        {
            var attackerBrowser = await SignInAsAttacker();
            PlantBffCookies(from: attackerBrowser, into: BffHost.BrowserClient);
            await AttemptBffLoginAsync("alice");

            await AttemptBffLoginAsync("alice");

            var aliceClaims = await BffHost.CallUserEndpointAsync();
            aliceClaims.Single(claim => claim.Type == JwtClaimTypes.Subject).Value.ToString().ShouldBe("alice");

            var attackerCookieStillSignedIn = await GetIsUserLoggedInAsync(attackerBrowser);
            attackerCookieStillSignedIn.ShouldBeFalse("the planted session cookie must not lead to the other user's session");
        }

        [Fact]
        public async Task when_signing_out_and_signing_in_another_user_in_one_request_expect_the_old_session_cookie_to_lead_to_no_session()
        {
            var attackerBrowser = await SignInAsAttacker();
            PlantBffCookies(from: attackerBrowser, into: BffHost.BrowserClient);

            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/sign-out-and-sign-in-as-victim"));
            var signOutAndSignInResponse = await BffHost.BrowserClient.SendAsync(req);

            signOutAndSignInResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

            var attackerCookieStillSignedIn = await GetIsUserLoggedInAsync(attackerBrowser);
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

        /// <summary>
        /// Drives the OIDC sign-in flow for the main (victim) browser, without asserting that it ends up
        /// logged in: when a planted session cookie belonging to another user is still present and valid,
        /// <see cref="ServerSideTicketStore.RenewAsync"/> intentionally removes that other user's session
        /// (see fix under test) instead of overwriting it, so a first sign-in attempt can be a silent no-op.
        /// A second attempt (no stale cookie left) succeeds.
        /// </summary>
        private async Task AttemptBffLoginAsync(string sub, string? sid = null)
        {
            await IdentityServerHost.CreateIdentityServerSessionCookieAsync(sub, sid);

            var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/login"));
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // authorize
            response.Headers.Location!.ToString().ToLowerInvariant()
                .ShouldStartWith(IdentityServerHost.Url("/connect/authorize"));

            response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // client callback
            response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(BffHost.Url("/signin-oidc"));

            response = await BffHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // root
            response.Headers.Location!.ToString().ToLowerInvariant().ShouldBe("/");
        }

        /// <summary>
        /// Signs in a separate, independent browser session as a different user ("attacker") against the same
        /// BFF host, without disturbing the main BffHost.BrowserClient (which stands in
        /// for the other user's / "victim"'s browser in these tests).
        /// </summary>
        private async Task<TestBrowserClient> SignInAsAttacker()
        {
            var attackerBrowser = new TestBrowserClient(BffHost.Server.CreateHandler())
            {
                BaseAddress = new Uri(BffHost.Url())
            };

            await IdentityServerHost.CreateIdentityServerSessionCookieAsync("attacker");

            var response = await attackerBrowser.GetAsync(BffHost.Url("/bff/login"));
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // authorize
            response.Headers.Location!.ToString().ToLowerInvariant()
                .ShouldStartWith(IdentityServerHost.Url("/connect/authorize"));

            response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // client callback
            response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(BffHost.Url("/signin-oidc"));

            response = await attackerBrowser.GetAsync(response.Headers.Location.ToString());
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // root
            response.Headers.Location!.ToString().ToLowerInvariant().ShouldBe("/");

            (await GetIsUserLoggedInAsync(attackerBrowser)).ShouldBeTrue();

            return attackerBrowser;
        }

        private async Task<bool> GetIsUserLoggedInAsync(TestBrowserClient browser)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/bff/user"));
            req.Headers.Add("x-csrf", "1");
            var response = await browser.SendAsync(req);

            (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Unauthorized)
                .ShouldBeTrue();

            return response.StatusCode == HttpStatusCode.OK;
        }

        private void PlantBffCookies(TestBrowserClient from, TestBrowserClient into)
        {
            var uri = new Uri(BffHost.Url());
            foreach (System.Net.Cookie cookie in from.Cookies.GetCookies(uri))
            {
                into.Cookies.Add(uri, new System.Net.Cookie(cookie.Name, cookie.Value, cookie.Path)
                {
                    Secure = cookie.Secure,
                    HttpOnly = cookie.HttpOnly
                });
            }
        }
    }
}
