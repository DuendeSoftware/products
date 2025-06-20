// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.Tests.TestInfra;
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
        ConfigureBff(BffSetupType.BffWithFrontend, UseSlidingCookieExpiration);
        await InitializeAsync();

        Internet.AddCustomHandler(map: The.DomainName, to: Bff);
        var frontendWithOrigin = new BffFrontend(BffFrontendName.Parse("frontend-with-origin"))
            .WithOpenIdConnectOptions(The.DefaultOpenIdConnectConfiguration)
            .MappedToOrigin(Origin.Parse(The.DomainName));
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
        ConfigureBff(BffSetupType.BffWithFrontend, UseSlidingCookieExpiration);
        await InitializeAsync();

        Internet.AddCustomHandler(map: The.DomainName, to: Bff);
        var frontendWithOrigin = new BffFrontend(BffFrontendName.Parse("frontend-with-origin"))
            .WithOpenIdConnectOptions(The.DefaultOpenIdConnectConfiguration)
            .MappedToOrigin(Origin.Parse(The.DomainName));
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

    /// <summary>
    /// Get's the user sessions, optionally for a specific frontend.
    /// if not specified, the current frontend is used.
    /// </summary>
    private async Task<IReadOnlyCollection<UserSession>> GetUserSessions(BffFrontend? forFrontend = null)
    {
        using (var scope = Bff.ResolveForFrontend(forFrontend ?? CurrentFrontend))
        {
            var sessionStore = scope.Resolve<IUserSessionStore>();
            return await sessionStore.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = The.Sub });
        }
    }

    private static void UseSlidingCookieExpiration(CookieAuthenticationOptions options)
    {
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    }
}

public class DelegateDisposable(Action onDispose) : IDisposable
{
    public void Dispose() => onDispose();
}
