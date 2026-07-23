// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.Api;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Interaction.SharedHosts.MvcClient;
using Duende.IdentityServer.Interaction.Tests.Infrastructure;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using Shouldly;

namespace Duende.IdentityServer.Interaction.Scenarios.TokenManagement;

/// <summary>
/// Scenario: MVC app with Duende.AccessTokenManagement for automatic token refresh.
/// The access token has a short lifetime (75s). The token management library
/// transparently refreshes it before it expires.
/// A shared <see cref="FakeTimeProvider"/> is registered in all hosts so time can
/// be advanced from tests to trigger token expiry without real delays.
/// </summary>
public sealed class AutomaticTokenManagement : IScenario
{
    private IdentityServerTestHost? _identityServer;
    private ClientWebAppTestHost? _webApp;
    private ApiHost? _api;

    /// <summary>
    /// Shared fake time provider registered in all hosts. Advance this from tests
    /// to simulate token expiry.
    /// </summary>
    public FakeTimeProvider TimeProvider { get; } = new(DateTimeOffset.UtcNow);

    public string Name => "AutomaticTokenManagement";
    public string Description => "MVC app with automatic token management (short-lived access tokens, auto-refresh)";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    /// <summary>Advance the shared clock. All hosts see the same time.</summary>
    public void AdvanceTime(TimeSpan duration) => TimeProvider.Advance(duration);

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        var fakeTime = TimeProvider;

        // 1. Start IdentityServer — registers FakeTimeProvider BEFORE AddIdentityServer()
        //    so IS uses it for token issuance (exp claims).
        _identityServer = new IdentityServerTestHost(configurator, "identity-server",
            configureServices: services =>
            {
                services.AddSingleton<TimeProvider>(fakeTime);
            });
        _identityServer.AddDefaultUsers();
        _identityServer.AddDefaultResources();
        await _identityServer.StartAsync(ct);

        // 2. Start the API — disable lifetime validation since the API's JwtBearer
        //    handler doesn't use TimeProvider (it uses DateTime.UtcNow internally).
        //    In a real deployment, token expiry is validated; here we skip it so
        //    the FakeTimeProvider advancement doesn't cause false 401s.
        _api = new ApiHost(configurator, "api", _identityServer.BuildUri().ToString().TrimEnd('/'),
            configureServices: services =>
            {
                services.AddSingleton<TimeProvider>(fakeTime);
                services.PostConfigure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>("token", opt =>
                {
                    opt.TokenValidationParameters.ValidateLifetime = false;
                });
            });
        await _api.StartAsync(ct);

        // 3. Start the MVC client with automatic token management + FakeTimeProvider
        _webApp = new ClientWebAppTestHost(configurator,
            _identityServer,
            _api,
            name: "mvc-tokenmanagement",
            configureOpenIdConnect: options =>
            {
                options.ResponseType = "code";
                options.UsePkce = true;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("resource1.scope1");
                options.Scope.Add("offline_access");

                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;
            },
            configureServices: services =>
            {
                services.AddSingleton<TimeProvider>(fakeTime);
                services.AddOpenIdConnectAccessTokenManagement();
            });

        await _webApp.StartAsync(ct);

        // 4. Register the client with IdentityServer
        _identityServer.AddClient(_webApp, c =>
        {
            c.ClientId = _webApp.Name;
            c.ClientName = "MVC Automatic Token Management";
            c.RequireConsent = false;
            c.AllowedGrantTypes = GrantTypes.Code;
            c.RequirePkce = true;
            c.AllowOfflineAccess = true;
            c.RefreshTokenUsage = TokenUsage.ReUse;

            // Short access token lifetime to exercise automatic refresh
            c.AccessTokenLifetime = 75;

            c.AllowedScopes =
            [
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                "resource1.scope1"
            ];
            c.PostLogoutRedirectUris = [_webApp.BuildUri("signout-callback-oidc").ToString()];
            c.FrontChannelLogoutUri = _webApp.BuildUri("signout-oidc").ToString();
        });

        Links = [_identityServer.Link, _webApp.Link, _api.Link];
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_api != null)
        {
            await _api.DisposeAsync();
        }

        if (_webApp != null)
        {
            await _webApp.DisposeAsync();
        }

        if (_identityServer != null)
        {
            await _identityServer.DisposeAsync();
        }
    }

    public Command[] GetCommands() => [];

    public class Tests(ScenarioFixture<AutomaticTokenManagement> fixture) : PageTest, IClassFixture<ScenarioFixture<AutomaticTokenManagement>>
    {
        public override BrowserNewContextOptions ContextOptions() => new()
        {
            IgnoreHTTPSErrors = true
        };

        [Fact]
        public async Task Login_and_call_api()
        {
            var webappUrl = fixture.Link("mvc-tokenmanagement").ToString();

            // Go to the webapp and click Secure
            await Page.GotoAsync(webappUrl);
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");

            // Sign in as alice
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

            // Redirected back to Secure page with claims
            await Page.WaitForSelectorAsync("text=Claims");

            var body = await Page.TextContentAsync("body");
            body.ShouldNotBeNull();
            body.ShouldContain("Alice Smith");
            body.ShouldContain("sub");

            // Call API — token management automatically attaches the access token
            await Page.GetByRole(AriaRole.Link, new() { Name = "Call API" }).ClickAsync();
            await Page.WaitForSelectorAsync("pre");

            var apiBody = await Page.TextContentAsync("pre");
            apiBody.ShouldNotBeNull();
            apiBody.ShouldContain("sub");
        }

        [Fact]
        public async Task Token_is_refreshed_automatically_after_expiry()
        {
            var webappUrl = fixture.Link("mvc-tokenmanagement").ToString();
            var scenario = (AutomaticTokenManagement)fixture.Scenario;

            // Login
            await Page.GotoAsync(webappUrl);
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=Claims");

            // Call API — should succeed with the original token
            await Page.GetByRole(AriaRole.Link, new() { Name = "Call API" }).ClickAsync();
            await Page.WaitForSelectorAsync("pre");
            var firstApiBody = await Page.TextContentAsync("pre");
            firstApiBody.ShouldNotBeNull();
            firstApiBody.ShouldContain("sub");

            // Advance time past the access token lifetime (75s).
            // AccessTokenManagement checks expiry against TimeProvider, so it will
            // detect the token as expired and transparently refresh via refresh_token.
            scenario.AdvanceTime(TimeSpan.FromSeconds(80));

            // Call API again — token management should refresh automatically and succeed
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=Claims");
            await Page.GetByRole(AriaRole.Link, new() { Name = "Call API" }).ClickAsync();
            await Page.WaitForSelectorAsync("pre");
            var secondApiBody = await Page.TextContentAsync("pre");
            secondApiBody.ShouldNotBeNull();
            secondApiBody.ShouldContain("sub");
        }

        [Fact]
        public async Task Logout_ends_session()
        {
            var webappUrl = fixture.Link("mvc-tokenmanagement").ToString();

            // Login
            await Page.GotoAsync(webappUrl);
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=Claims");

            // Verify logged in
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).ToBeVisibleAsync();

            // Logout
            await Page.GetByRole(AriaRole.Link, new() { Name = "Logout" }).ClickAsync();

            // IS shows logout confirmation
            await Page.WaitForSelectorAsync("text=Logout");
            var yesButton = Page.GetByRole(AriaRole.Button, new() { Name = "Yes" });
            if (await yesButton.IsVisibleAsync())
            {
                await yesButton.ClickAsync();
            }

            await Page.WaitForSelectorAsync("text=logged out");

            // Navigate back — user should be anonymous
            await Page.GotoAsync(webappUrl);
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).Not.ToBeVisibleAsync();
        }
    }
}
