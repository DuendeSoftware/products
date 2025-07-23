// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.Tests.TestInfra;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints.Management;

public class BackchannelLogoutEndpointTests : BffTestBase
{
    public BackchannelLogoutEndpointTests(ITestOutputHelper output) : base(output) => Bff.OnConfigureBff += bff =>
    {
        bff.AddServerSideSessions();
    };

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        foreach (var client in IdentityServer.Clients)
        {
            client.BackChannelLogoutUri = Bff.Url("/bff/backchannel").ToString();
            client.BackChannelLogoutSessionRequired = true;
        }
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task backchannel_logout_should_allow_anonymous(BffSetupType setup)
    {
        Bff.OnConfigureServices += svcs =>
        {
            svcs.AddAuthorization(opts =>
            {
                opts.FallbackPolicy =
                    new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
            });
        };
        await ConfigureBff(setup);

        // if you call the endpoint without a token, it should return 400
        await Bff.BrowserClient.PostAsync(Bff.Url("/bff/backchannel"), null)
            .CheckHttpStatusCode(HttpStatusCode.BadRequest);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task backchannel_logout_endpoint_should_signout(BffSetupType setup)
    {
        await ConfigureBff(setup);

        await Bff.BrowserClient.Login();

        await Bff.BrowserClient.RevokeIdentityServerSession();

        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task backchannel_logout_endpoint_for_incorrect_sub_should_not_logout_user(BffSetupType setup)
    {
        await ConfigureBff(setup);

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, The.Sid);

        await Bff.BrowserClient.Login();

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, "different_sub", The.Sid);

        await Bff.BrowserClient.RevokeIdentityServerSession();

        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task backchannel_logout_endpoint_for_incorrect_sid_should_not_logout_user(BffSetupType setup)
    {
        await ConfigureBff(setup);

        await Bff.BrowserClient.Login();

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, "different_sid");

        await Bff.BrowserClient.RevokeIdentityServerSession();

        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeTrue();
    }


    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task when_BackchannelLogoutAllUserSessions_is_false_backchannel_logout_should_only_logout_one_session(
        BffSetupType setup)
    {
        await ConfigureBff(setup);
        Bff.BffOptions.BackchannelLogoutAllUserSessions = false;

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, The.Sid);

        await Bff.BrowserClient.Login();

        // Set a Set-Cookie header to clear the "__Host-bff-auth" cookie
        Bff.BrowserClient.Cookies.Clear(Bff.Url());

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, "different");

        await Bff.BrowserClient.Login();

        (await GetUserSessions()).Count.ShouldBe(2);

        await Bff.BrowserClient.RevokeIdentityServerSession();

        {
            var sessions = await GetUserSessions();
            var session = sessions.Single();
            session.SessionId.ShouldBe(The.Sid);
        }
    }


    /// <summary>
    /// Get the user sessions for the provided frontend. If not provided, then the current frontend (if any) is used.
    /// </summary>
    /// <param name="forFrontend"></param>
    /// <returns></returns>
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

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task when_BackchannelLogoutAllUserSessions_is_false_backchannel_logout_should_logout_all_sessions(
        BffSetupType setup)
    {
        await ConfigureBff(setup);
        Bff.BffOptions.BackchannelLogoutAllUserSessions = true;

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, The.Sid);

        await Bff.BrowserClient.Login();

        // Set a Set-Cookie header to clear the "__Host-bff-auth" cookie
        Bff.BrowserClient.Cookies.Clear(Bff.Url());

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, "different");

        await Bff.BrowserClient.Login();

        (await GetUserSessions()).Count.ShouldBe(2);

        await Bff.BrowserClient.RevokeIdentityServerSession();

        (await GetUserSessions()).ShouldBeEmpty();
    }
    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task when_session_expires_then_token_is_not_revoked(
        BffSetupType setup)
    {
        await ConfigureBff(setup, configureCookie: c => c.ExpireTimeSpan = TimeSpan.FromSeconds(2));
        Bff.BffOptions.BackchannelLogoutAllUserSessions = true;

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, The.Sid);

        await Bff.BrowserClient.Login();

        // Set a Set-Cookie header to clear the "__Host-bff-auth" cookie
        Bff.BrowserClient.Cookies.Clear(Bff.Url());

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, "different");

        await Bff.BrowserClient.Login();

        (await GetUserSessions()).Count.ShouldBe(2);

        await Task.Delay(TimeSpan.FromSeconds(4));

        var result = await Bff.BrowserClient.GetIsUserLoggedInAsync();
        result.ShouldBeTrue();

    }
}
