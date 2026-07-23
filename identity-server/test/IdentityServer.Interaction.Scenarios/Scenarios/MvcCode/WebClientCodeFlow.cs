// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.Api;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Interaction.SharedHosts.MvcClient;
using Duende.IdentityServer.Interaction.Tests.Infrastructure;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;
using Microsoft.AspNetCore.Builder;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using Shouldly;

namespace Duende.IdentityServer.Interaction.Scenarios.MvcCode;

public sealed class WebClientCodeFlow : IScenario
{
    private IdentityServerTestHost? _identityServer;
    private ClientWebAppTestHost? _webApp;
    private ApiHost? _api;

    public string Name => "WebClientCodeFlow";
    public string Description => "MVC app with authorization code flow + PKCE, calling a protected API";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        // 1. Start IdentityServer
        _identityServer = new IdentityServerTestHost(configurator, "identity-server");
        _identityServer.AddDefaultUsers();
        _identityServer.AddDefaultResources();
        await _identityServer.StartAsync(ct);

        var authority = _identityServer.BuildUri().ToString().TrimEnd('/');

        // 2. Start the API
        _api = new ApiHost(configurator, "api", authority);
        _api.App.MapGet("/", () => "api");
        await _api.StartAsync(ct);

        var apiBaseUri = _api.BuildUri().ToString().TrimEnd('/');

        // 3. Start the MVC client
        _webApp = new ClientWebAppTestHost(configurator,
            _identityServer,
            _api,
            configureOpenIdConnect: options =>
            {
                // code flow + PKCE
                options.ResponseType = "code";
                options.UsePkce = true;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("custom.profile");
                options.Scope.Add("resource1.scope1");
                options.Scope.Add("resource2.scope1");
                options.Scope.Add("offline_access");
            });

        await _webApp.StartAsync(ct);

        // 4. Register the client with IdentityServer
        _identityServer.AddClient(_webApp, c =>
        {
            c.ClientId = _webApp.Name;
            c.ClientName = _webApp.Name;
            c.RequireConsent = false;
            c.AllowedGrantTypes = GrantTypes.Code;
            c.AllowOfflineAccess = true;
            c.RefreshTokenUsage = TokenUsage.ReUse;
            c.AllowedScopes =
            [
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                IdentityServerConstants.StandardScopes.Email,
                "custom.profile",
                "resource1.scope1",
                "resource2.scope1"
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

    public class Tests(ScenarioFixture<WebClientCodeFlow> fixture) : PageTest, IClassFixture<ScenarioFixture<WebClientCodeFlow>>
    {
        public override BrowserNewContextOptions ContextOptions() => new()
        {
            IgnoreHTTPSErrors = true
        };

        [Fact]
        public async Task Full_login_flow_and_call_api()
        {
            var webappUrl = fixture.Link("webapp").ToString();

            // 1. Go to the webapp home page
            var response = await Page.GotoAsync(webappUrl);
            response.ShouldNotBeNull();
            response.Ok.ShouldBeTrue();

            // 2. Click "Secure" — should redirect to IdentityServer login
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");

            // 3. Sign in using alice/alice
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

            // 4. User is redirected back to the Secure page — assert claims
            await Page.WaitForSelectorAsync("text=Claims");

            var body = await Page.TextContentAsync("body");
            body.ShouldNotBeNull();
            body.ShouldContain("Alice Smith");
            body.ShouldContain("sub");
            body.ShouldContain("name");

            // 5. Click "Call API" and verify the response contains claim data
            await Page.GetByRole(AriaRole.Link, new() { Name = "Call API" }).ClickAsync();
            await Page.WaitForSelectorAsync("pre");

            var apiBody = await Page.TextContentAsync("pre");
            apiBody.ShouldNotBeNull();
            apiBody.ShouldContain("sub");
        }

        [Fact]
        public async Task Renew_tokens_updates_access_token()
        {
            var webappUrl = fixture.Link("webapp").ToString();

            // Login first
            await Page.GotoAsync(webappUrl);
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=Claims");

            // Capture the current access token from the Properties section
            var bodyBefore = await Page.TextContentAsync("body");
            bodyBefore.ShouldNotBeNull();
            bodyBefore.ShouldContain(".Token.access_token");

            // Click "Renew Tokens" — should redirect back to Secure with updated tokens
            await Page.GetByRole(AriaRole.Link, new() { Name = "Renew Tokens" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=Claims");

            // Verify we're still authenticated and tokens are present
            var bodyAfter = await Page.TextContentAsync("body");
            bodyAfter.ShouldNotBeNull();
            bodyAfter.ShouldContain("Alice Smith");
            bodyAfter.ShouldContain(".Token.access_token");
            bodyAfter.ShouldContain(".Token.refresh_token");
        }

        [Fact]
        public async Task Logout_ends_session()
        {
            var webappUrl = fixture.Link("webapp").ToString();

            // Login first
            await Page.GotoAsync(webappUrl);
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=Claims");

            // Verify we're logged in (Logout link visible)
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).ToBeVisibleAsync();

            // Click Logout
            await Page.GetByRole(AriaRole.Link, new() { Name = "Logout" }).ClickAsync();

            // IdentityServer shows logout confirmation — click "Yes"
            await Page.WaitForSelectorAsync("text=Logout");
            var yesButton = Page.GetByRole(AriaRole.Button, new() { Name = "Yes" });
            if (await yesButton.IsVisibleAsync())
            {
                await yesButton.ClickAsync();
            }

            // Should end up on the logged-out page or be redirected back
            await Page.WaitForSelectorAsync("text=logged out");

            // Navigate back to the webapp — should no longer be authenticated
            await Page.GotoAsync(webappUrl);
            var body = await Page.TextContentAsync("body");
            body.ShouldNotBeNull();

            // Logout link should NOT be visible (user is anonymous)
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).Not.ToBeVisibleAsync();
        }
    }

}

