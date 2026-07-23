// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text.Json;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.AspNetCore.Authentication.JwtBearer.DPoP;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.Api;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Interaction.SharedHosts.MvcClient;
using Duende.IdentityServer.Interaction.Tests.Infrastructure;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using Shouldly;

namespace Duende.IdentityServer.Interaction.Scenarios.DPoP;

/// <summary>
/// Scenario: MVC app with DPoP (Demonstrating Proof of Possession).
/// Access tokens are bound to a client-generated key pair. The API validates
/// that the DPoP proof matches the token's key binding (cnf/jkt).
/// </summary>
public sealed class MvcDPoPFlow : IScenario
{
    private IdentityServerTestHost? _identityServer;
    private ClientWebAppTestHost? _webApp;
    private ApiHost? _api;

    public string Name => "MvcDPoP";
    public string Description => "MVC app with DPoP proof-of-possession tokens";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        // 1. Start IdentityServer
        _identityServer = new IdentityServerTestHost(configurator, "identity-server");
        _identityServer.AddDefaultUsers();
        _identityServer.AddDefaultResources();
        await _identityServer.StartAsync(ct);

        // 2. Start the DPoP-enabled API
        _api = new ApiHost(configurator, "dpop-api",
            _identityServer.BuildUri().ToString().TrimEnd('/'),
            configureServices: services =>
            {
                services.AddDistributedMemoryCache();
                services.ConfigureDPoPTokensForScheme("token", options =>
                {
                    options.AllowBearerTokens = true;
                    options.EnableReplayDetection = false;
                });
            });
        await _api.StartAsync(ct);

        // 3. Generate DPoP key pair for the client
        var rsaKey = new RsaSecurityKey(RSA.Create(2048));
        var jwk = JsonWebKeyConverter.ConvertFromSecurityKey(rsaKey);
        jwk.Alg = "PS256";
        var dpopKey = DPoPProofKey.Parse(JsonSerializer.Serialize(jwk));

        // 4. Start the MVC client with DPoP
        _webApp = new ClientWebAppTestHost(configurator,
            _identityServer,
            _api,
            name: "mvc-dpop",
            configureOpenIdConnect: options =>
            {
                options.ResponseType = "code";
                options.ResponseMode = "query";
                options.UsePkce = true;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("resource1.scope1");
                options.Scope.Add("offline_access");

                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;
            },
            configureCookie: options =>
            {
                options.Events.OnSigningOut = async e =>
                {
                    await e.HttpContext.RevokeRefreshTokenAsync();
                };
            },
            configureServices: services =>
            {
                services.AddOpenIdConnectAccessTokenManagement(options =>
                {
                    options.DPoPJsonWebKey = dpopKey;
                });

                // Register the "api" client with DPoP token management so
                // CallApi page's CreateClient("api") gets DPoP proofs attached
                services.AddUserAccessTokenHttpClient("api", configureClient: client =>
                {
                    client.BaseAddress = _api!.BuildUri();
                });
            });

        await _webApp.StartAsync(ct);

        // 5. Register the DPoP client with IdentityServer
        _identityServer.AddClient(_webApp, c =>
        {
            c.ClientId = _webApp.Name;
            c.ClientName = "MVC DPoP Client";
            c.RequireConsent = false;
            c.AllowedGrantTypes = GrantTypes.Code;
            c.RequirePkce = true;
            c.RequireDPoP = true;
            c.AllowOfflineAccess = true;
            c.RefreshTokenUsage = TokenUsage.ReUse;
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

    public class Tests(ScenarioFixture<MvcDPoPFlow> fixture) : PageTest, IClassFixture<ScenarioFixture<MvcDPoPFlow>>
    {
        public override BrowserNewContextOptions ContextOptions() => new()
        {
            IgnoreHTTPSErrors = true
        };

        [Fact]
        public async Task Login_and_call_dpop_api()
        {
            var webappUrl = fixture.Link("mvc-dpop").ToString();

            // Login via browser — proves DPoP OIDC flow works (token endpoint uses DPoP)
            await Page.GotoAsync(webappUrl);
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=Claims");

            var body = await Page.TextContentAsync("body");
            body.ShouldNotBeNull();
            body.ShouldContain("Alice Smith");
            body.ShouldContain("sub");

            // Verify the access token in the session has a DPoP token type
            // (the Properties section shows .Token.token_type = DPoP)
            body.ShouldContain("DPoP");

            // Call the DPoP-protected API — the managed HTTP client should
            // automatically attach a DPoP proof alongside the access token
            await Page.GetByRole(AriaRole.Link, new() { Name = "Call API" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=API Response");

            var apiBody = await Page.TextContentAsync("body");
            apiBody.ShouldNotBeNull();
            apiBody.ShouldContain("client_id");
        }

        [Fact]
        public async Task Logout_ends_session()
        {
            var webappUrl = fixture.Link("mvc-dpop").ToString();

            // Login
            await Page.GotoAsync(webappUrl);
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=Claims");

            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).ToBeVisibleAsync();
            await Page.GetByRole(AriaRole.Link, new() { Name = "Logout" }).ClickAsync();

            await Page.WaitForSelectorAsync("text=Logout");
            var yesButton = Page.GetByRole(AriaRole.Button, new() { Name = "Yes" });
            if (await yesButton.IsVisibleAsync())
            {
                await yesButton.ClickAsync();
            }

            await Page.WaitForSelectorAsync("text=logged out");

            await Page.GotoAsync(webappUrl);
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).Not.ToBeVisibleAsync();
        }
    }
}
