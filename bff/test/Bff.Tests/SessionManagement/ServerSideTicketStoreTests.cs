// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.Tests.TestInfra;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.SessionManagement;

public class ServerSideTicketStoreTests : BffTestBase
{
    public ServerSideTicketStoreTests(ITestOutputHelper output) : base(output) =>
        Bff.OnConfigureBff += bff =>
        {
            bff.AddServerSideSessions();
        };

    [Theory, MemberData(nameof(AllSetups))]
    public async Task StoreAsync_should_remove_conflicting_entries_prior_to_creating_new_entry(BffSetupType setup)
    {
        Bff.OnConfigureBff += bff => bff.AddServerSideSessions();
        await ConfigureBff(setup);
        await Bff.BrowserClient.Login();

        Bff.BrowserClient.Cookies.Clear(Bff.Url());
        (await GetUserSessions()).Count.ShouldBe(1);

        await Bff.BrowserClient.Login();

        (await GetUserSessions()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Given_multiple_frontends_each_frontend_gets_a_session()
    {
        await ConfigureBff(BffSetupType.BffWithFrontend, UseSlidingCookieExpiration);

        Internet.AddCustomHandler(map: The.DomainName, to: Bff);
        var frontendWithOrigin = new BffFrontend(BffFrontendName.Parse("frontend-with-origin"))
            .WithOpenIdConnectOptions(The.DefaultOpenIdConnectConfiguration)
            .MapToHost(HostHeaderValue.Parse(The.DomainName));
        AddOrUpdateFrontend(frontendWithOrigin);

        await Bff.BrowserClient.Login();
        (await GetUserSessions()).Count.ShouldBe(1);

        var bffClientForOtherOrigin = Bff.BuildBrowserClient(The.DomainName, Bff.BrowserClient.Cookies);
        await bffClientForOtherOrigin.Login();

        (await Bff.BrowserClient.GetIsUserLoggedInAsync("slide=false")).ShouldBeTrue();
        (await bffClientForOtherOrigin.GetIsUserLoggedInAsync("slide=false")).ShouldBeTrue();

        var defaultFrontendSessions = (await GetUserSessions());
        defaultFrontendSessions.Count.ShouldBe(1);

        var frontendWithOriginSessions = (await GetUserSessions(frontendWithOrigin));
        frontendWithOriginSessions.Count.ShouldBe(1);

        // Make sure it's a different session
        frontendWithOriginSessions.Single().Key.ShouldNotBe(defaultFrontendSessions.Single().Key);
    }

    [Fact]
    public async Task Given_multiple_frontends_logout_only_affects_single_frontend()
    {
        await ConfigureBff(BffSetupType.BffWithFrontend, UseSlidingCookieExpiration);

        Internet.AddCustomHandler(map: The.DomainName, to: Bff);
        var frontendWithOrigin = new BffFrontend(BffFrontendName.Parse("frontend-with-origin"))
            .WithOpenIdConnectOptions(The.DefaultOpenIdConnectConfiguration)
            .MapToHost(HostHeaderValue.Parse(The.DomainName));
        AddOrUpdateFrontend(frontendWithOrigin);

        await Bff.BrowserClient.Login();
        (await GetUserSessions()).Count.ShouldBe(1);

        var bffClientForOtherOrigin = Bff.BuildBrowserClient(The.DomainName, Bff.BrowserClient.Cookies);
        await bffClientForOtherOrigin.Login();

        (await Bff.BrowserClient.GetIsUserLoggedInAsync("slide=false")).ShouldBeTrue();
        (await bffClientForOtherOrigin.GetIsUserLoggedInAsync("slide=false")).ShouldBeTrue();

        await Bff.BrowserClient.Logout();
        (await Bff.BrowserClient.GetIsUserLoggedInAsync("slide=false")).ShouldBeFalse();

        (await bffClientForOtherOrigin.GetIsUserLoggedInAsync("slide=false")).ShouldBeTrue();
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task when_signing_in_with_another_users_session_cookie_expect_that_cookie_to_lead_to_no_session(BffSetupType setup)
    {
        await ConfigureBff(setup);
        var attackerBrowser = await SignInAsAttacker();
        var victimBrowser = Bff.BrowserClient;
        PlantBffCookies(from: attackerBrowser, into: victimBrowser);

        _ = await victimBrowser.Login();

        var attackerCookieStillSignedIn = await attackerBrowser.GetIsUserLoggedInAsync();
        attackerCookieStillSignedIn.ShouldBeFalse("the attacker's session cookie must not lead to the victim's session");
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task when_signing_in_twice_with_another_users_session_cookie_expect_the_second_sign_in_to_succeed(BffSetupType setup)
    {
        await ConfigureBff(setup);
        var attackerBrowser = await SignInAsAttacker();
        var victimBrowser = Bff.BrowserClient;
        PlantBffCookies(from: attackerBrowser, into: victimBrowser);
        _ = await victimBrowser.Login();

        _ = await victimBrowser.Login();

        var victimClaims = await victimBrowser.CallUserEndpointAsync();
        victimClaims.Single(claim => claim.Type == JwtClaimTypes.Subject).Value.ToString().ShouldBe(The.Sub);
        var attackerCookieStillSignedIn = await attackerBrowser.GetIsUserLoggedInAsync();
        attackerCookieStillSignedIn.ShouldBeFalse("the attacker's session cookie must not lead to the victim's session");
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task when_signing_out_and_signing_in_another_user_in_one_request_expect_the_old_session_cookie_to_lead_to_no_session(BffSetupType setup)
    {
        Bff.OnConfigureApp += app => app.MapGet("/sign-out-and-sign-in-as-victim", async context =>
        {
            var cookieScheme = await context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>().GetDefaultAuthenticateSchemeAsync();
            var victim = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(JwtClaimTypes.Subject, The.Sub), new Claim(JwtClaimTypes.SessionId, "victim-sid")], "test", "name", "role"));

            await context.SignOutAsync(cookieScheme!.Name);
            await context.SignInAsync(cookieScheme.Name, victim);
        });
        await ConfigureBff(setup);
        var attackerBrowser = await SignInAsAttacker();
        var victimBrowser = Bff.BrowserClient;
        PlantBffCookies(from: attackerBrowser, into: victimBrowser);

        var signOutAndSignInResponse = await victimBrowser.GetAsync("/sign-out-and-sign-in-as-victim");

        signOutAndSignInResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var attackerCookieStillSignedIn = await attackerBrowser.GetIsUserLoggedInAsync();
        attackerCookieStillSignedIn.ShouldBeFalse("the attacker's session cookie must not lead to the victim's session");
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task when_the_same_user_signs_in_with_a_new_identity_provider_session_expect_the_session_to_keep_working(BffSetupType setup)
    {
        await ConfigureBff(setup);
        _ = await Bff.BrowserClient.Login();
        var firstIdentityProviderSessionId = await Bff.BrowserClient.GetSid();
        await Bff.BrowserClient.RevokeIdentityServerSession();

        _ = await Bff.BrowserClient.Login();

        var secondIdentityProviderSessionId = await Bff.BrowserClient.GetSid();
        secondIdentityProviderSessionId.ShouldNotBe(firstIdentityProviderSessionId);
    }

    /// <summary>
    /// Get's the user sessions, optionally for a specific frontend.
    /// if not specified, the current frontend is used.
    /// </summary>
    private async Task<IReadOnlyCollection<UserSession>> GetUserSessions(BffFrontend? forFrontend = null)
    {
        using (var scope = Bff.ResolveForFrontend(forFrontend ?? CurrentFrontend))
        {
            var sessionStore = scope.Resolve<IUserSessionStore>();

            var partitionKey = scope.Resolve<BuildUserSessionPartitionKey>()();
            var userSessionsFilter = new UserSessionsFilter
            {
                SubjectId = The.Sub
            };
            return await sessionStore.GetUserSessionsAsync(partitionKey, userSessionsFilter);
        }
    }

    private static void UseSlidingCookieExpiration(CookieAuthenticationOptions options)
    {
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    }

    private async Task<BffHttpClient> SignInAsAttacker()
    {
        IdentityServer.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtClaimTypes.Subject, "attacker"), new Claim(JwtClaimTypes.Name, "attacker")], "test", "name", "role"));
        var attackerBrowser = Bff.BuildBrowserClient(Bff.Url());
        _ = await attackerBrowser.Login();
        IdentityServer.UserToSignIn = Some.ClaimsPrincipal();
        return attackerBrowser;
    }

    private void PlantBffCookies(BffHttpClient from, BffHttpClient into)
    {
        foreach (var cookie in from.Cookies.GetCookies(Bff.Url()).Cast<Cookie>())
        {
            into.Cookies.Add(Bff.Url(), new Cookie(cookie.Name, cookie.Value, cookie.Path) { Secure = cookie.Secure, HttpOnly = cookie.HttpOnly });
        }
    }
}

public class DelegateDisposable(Action onDispose) : IDisposable
{
    public void Dispose() => onDispose();
}
