// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.Api;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Interaction.SharedHosts.MvcClient;
using Duende.IdentityServer.Interaction.Tests.Infrastructure;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using Shouldly;

namespace Duende.IdentityServer.Interaction.Scenarios.MvcCode;

/// <summary>
/// Scenario: Hybrid flow with back-channel logout.
/// The MVC client uses the hybrid grant type (code + id_token) and registers a
/// back-channel logout URI. When the user logs out at IdentityServer, IS sends a
/// back-channel logout notification (logout_token JWT) to the client, which invalidates
/// the local session on the next request.
/// </summary>
public sealed class HybridBackChannel : IScenario
{
    private IdentityServerTestHost? _identityServer;
    private ClientWebAppTestHost? _webApp;
    private ApiHost? _api;

    public string Name => "HybridBackChannel";
    public string Description => "Hybrid flow with back-channel logout notification";
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
        await _api.StartAsync(ct);

        // 3. Start the MVC client with hybrid flow and back-channel logout
        _webApp = new ClientWebAppTestHost(configurator,
            _identityServer,
            _api,
            name: "mvc-hybrid",
            configureOpenIdConnect: options =>
            {
                options.ResponseType = "code id_token";

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("resource1.scope1");
                options.Scope.Add("offline_access");

                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;
            },
            configureCookie: options =>
            {
                options.EventsType = typeof(BackChannelCookieEventHandler);
            },
            configureServices: services =>
            {
                services.AddSingleton<LogoutSessionManager>();
                services.AddTransient<BackChannelCookieEventHandler>();
            },
            configureApp: app =>
            {
                app.MapPost("/backchannel-logout", async ([FromForm] string logout_token, [FromServices] LogoutSessionManager sessions, [FromServices] IDiscoveryCache disco, HttpResponse response) =>
                {
                    response.Headers.Append("Cache-Control", "no-cache, no-store");
                    response.Headers.Append("Pragma", "no-cache");

                    try
                    {
                        var user = await ValidateLogoutToken(logout_token, disco);
                        var sub = user.FindFirst("sub")?.Value;
                        var sid = user.FindFirst("sid")?.Value;
                        sessions.Add(sub, sid);
                        return Results.Ok();
                    }
                    catch
                    {
                        return Results.BadRequest();
                    }
                }).AllowAnonymous().DisableAntiforgery();
            });

        await _webApp.StartAsync(ct);

        // 4. Register the client with back-channel logout
        _identityServer.AddClient(_webApp, c =>
        {
            c.ClientId = _webApp.Name;
            c.ClientName = "Hybrid Back-Channel Logout Client";
            c.RequireConsent = false;
            c.AllowedGrantTypes = GrantTypes.HybridAndClientCredentials;
            c.RequirePkce = false;
            c.AllowOfflineAccess = true;
            c.RefreshTokenUsage = TokenUsage.ReUse;
            c.BackChannelLogoutUri = _webApp.BuildUri("backchannel-logout").ToString();
            c.BackChannelLogoutSessionRequired = true;
            c.AllowedScopes =
            [
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                IdentityServerConstants.StandardScopes.Email,
                "resource1.scope1"
            ];
            c.PostLogoutRedirectUris = [_webApp.BuildUri("signout-callback-oidc").ToString()];
        });

        Links = [_identityServer.Link, _webApp.Link, _api.Link];
    }

    private static async Task<ClaimsPrincipal> ValidateLogoutToken(string logoutToken, IDiscoveryCache discoveryCache)
    {
        var disco = await discoveryCache.GetAsync();
        if (disco.IsError)
        {
            throw new Exception(disco.Error);
        }

        var keys = new List<SecurityKey>();
        foreach (var webKey in disco.KeySet!.Keys)
        {
            var key = new Microsoft.IdentityModel.Tokens.JsonWebKey
            {
                Kty = webKey.Kty,
                Alg = webKey.Alg,
                Kid = webKey.Kid,
                X = webKey.X,
                Y = webKey.Y,
                Crv = webKey.Crv,
                E = webKey.E,
                N = webKey.N,
            };
            keys.Add(key);
        }

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = disco.Issuer,
            ValidAudience = "mvc-hybrid",
            IssuerSigningKeys = keys,
            NameClaimType = JwtClaimTypes.Name,
            RoleClaimType = JwtClaimTypes.Role
        };

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();

        var user = handler.ValidateToken(logoutToken, parameters, out _);

        // Validate required claims
        if (user.FindFirst("sub") == null && user.FindFirst("sid") == null)
        {
            throw new Exception("Invalid logout token");
        }

        if (!string.IsNullOrWhiteSpace(user.FindFirstValue("nonce")))
        {
            throw new Exception("Invalid logout token");
        }

        var eventsJson = user.FindFirst("events")?.Value;
        if (string.IsNullOrWhiteSpace(eventsJson))
        {
            throw new Exception("Invalid logout token");
        }

        var events = JsonDocument.Parse(eventsJson).RootElement;
        if (!events.TryGetProperty("http://schemas.openid.net/event/backchannel-logout", out _))
        {
            throw new Exception("Invalid logout token");
        }

        return user;
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

    public class Tests(ScenarioFixture<HybridBackChannel> fixture) : PageTest, IClassFixture<ScenarioFixture<HybridBackChannel>>
    {
        public override BrowserNewContextOptions ContextOptions() => new()
        {
            IgnoreHTTPSErrors = true
        };

        [Fact]
        public async Task Login_and_verify_claims()
        {
            var webappUrl = fixture.Link("mvc-hybrid").ToString();

            // 1. Navigate to the webapp
            await Page.GotoAsync(webappUrl);

            // 2. Click "Secure" — should redirect to IdentityServer login
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");

            // 3. Sign in
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

            // 4. Should be redirected back with claims
            await Page.WaitForSelectorAsync("text=Claims");

            var body = await Page.TextContentAsync("body");
            body.ShouldNotBeNull();
            body.ShouldContain("Alice Smith");
            body.ShouldContain("sub");
        }

        [Fact]
        public async Task Back_channel_logout_invalidates_session()
        {
            var webappUrl = fixture.Link("mvc-hybrid").ToString();

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
            await Page.WaitForSelectorAsync("text=Logout");
            var yesButton = Page.GetByRole(AriaRole.Button, new() { Name = "Yes" });
            if (await yesButton.IsVisibleAsync())
            {
                await yesButton.ClickAsync();
            }

            await Page.WaitForSelectorAsync("text=logged out");

            // 4. Navigate back to webapp — session should be invalidated by back-channel logout
            await Page.GotoAsync(webappUrl);
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).Not.ToBeVisibleAsync();
        }
    }
}

/// <summary>
/// Tracks sessions that have been logged out via back-channel notification.
/// </summary>
internal sealed class LogoutSessionManager
{
    private readonly List<(string? Sub, string? Sid)> _sessions = [];

    public void Add(string? sub, string? sid) => _sessions.Add((sub, sid));

    public bool IsLoggedOut(string? sub, string? sid) =>
        _sessions.Any(s =>
            (s.Sid == sid && s.Sub == sub) ||
            (s.Sid == sid && s.Sub == null) ||
            (s.Sid == null && s.Sub == sub));
}

/// <summary>
/// Cookie event handler that rejects principals whose sessions have been
/// invalidated via back-channel logout.
/// </summary>
internal sealed class BackChannelCookieEventHandler(LogoutSessionManager logoutSessions) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (context.Principal?.Identity?.IsAuthenticated == true)
        {
            var sub = context.Principal.FindFirst("sub")?.Value;
            var sid = context.Principal.FindFirst("sid")?.Value;

            if (logoutSessions.IsLoggedOut(sub, sid))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync();
            }
        }
    }
}
