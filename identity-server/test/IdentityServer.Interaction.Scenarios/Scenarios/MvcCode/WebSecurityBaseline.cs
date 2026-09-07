// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.AspNetCore.Authentication.JwtBearer.DPoP;
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
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using Shouldly;

namespace Duende.IdentityServer.Interaction.Scenarios.MvcCode;

/// <summary>
/// Scenario: Web security baseline combining DPoP + JAR + PAR + private_key_jwt + back-channel logout.
/// The client uses code flow with PKCE, authenticates at the token endpoint using a signed JWT assertion,
/// pushes a signed authorization request via PAR, binds access tokens with DPoP proof-of-possession,
/// and handles back-channel logout notifications from IdentityServer.
/// </summary>
public sealed class WebSecurityBaseline : IScenario
{
    private IdentityServerTestHost? _identityServer;
    private ClientWebAppTestHost? _webApp;
    private ApiHost? _api;
    private RSA? _rsaSigningKey;
    private RSA? _dpopRsaKey;

    public string Name => "WebSecurityBaseline";
    public string Description => "DPoP + JAR + PAR + private_key_jwt + back-channel logout";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        // 1. Start IdentityServer with JWT bearer client authentication
        _identityServer = new IdentityServerTestHost(configurator, "identity-server",
            configureIdentityServer: isBuilder =>
            {
                isBuilder.AddJwtBearerClientAuthentication();
            });
        _identityServer.AddDefaultUsers();
        _identityServer.AddDefaultResources();
        await _identityServer.StartAsync(ct);

        var authority = _identityServer.BuildUri().ToString().TrimEnd('/');

        // 2. Start the DPoP-enabled API
        _api = new ApiHost(configurator, "dpop-api",
            authority,
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

        // 3. Generate RSA key for client authentication and JAR signing
        _rsaSigningKey = RSA.Create(2048);
        var rsaSecurityKey = new RsaSecurityKey(_rsaSigningKey)
        {
            KeyId = Guid.NewGuid().ToString("N")
        };
        var signingCredentials = new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256);
        var publicJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaSecurityKey);
        var publicJwkJson = JsonSerializer.Serialize(publicJwk);

        // 4. Generate DPoP key pair
        _dpopRsaKey = RSA.Create(2048);
        var dpopRsaKey = new RsaSecurityKey(_dpopRsaKey);
        var dpopJwk = JsonWebKeyConverter.ConvertFromSecurityKey(dpopRsaKey);
        dpopJwk.Alg = "PS256";
        var dpopKey = DPoPProofKey.Parse(JsonSerializer.Serialize(dpopJwk));

        // 5. Start the MVC client with the full security baseline
        var clientId = "web";
        _webApp = new ClientWebAppTestHost(configurator,
            _identityServer,
            _api,
            name: clientId,
            configureOpenIdConnect: options =>
            {
                options.ResponseType = "code";
                options.ResponseMode = "query";
                options.UsePkce = true;

                // No client secret; we use private_key_jwt
                options.ClientSecret = null;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("resource1.scope1");
                options.Scope.Add("offline_access");

                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };

                options.DisableTelemetry = true;

                // Wire up JAR/PAR and private_key_jwt via events.
                // We must wrap the existing delegates rather than replacing the Events object,
                // because AccessTokenManagement's IConfigureNamedOptions has already wrapped
                // the default delegates with DPoP proof logic. Replacing the Events object
                // would discard those wrappers and prevent DPoP headers from being sent.
                var jarParEvents = new WebBaselineOidcEvents(signingCredentials, authority);

                var innerCodeReceived = options.Events.OnAuthorizationCodeReceived;
                options.Events.OnAuthorizationCodeReceived = async context =>
                {
                    await jarParEvents.AuthorizationCodeReceived(context);
                    await innerCodeReceived(context);
                };

                var innerPushAuthorization = options.Events.OnPushAuthorization;
                options.Events.OnPushAuthorization = async context =>
                {
                    await jarParEvents.PushAuthorization(context);
                    await innerPushAuthorization(context);
                };
            },
            configureCookie: options =>
            {
                options.EventsType = typeof(WebBaselineLogoutCookieEvents);
            },
            configureServices: services =>
            {
                // Back-channel logout session tracking
                services.AddSingleton<WebBaselineLogoutSessionManager>();
                services.AddTransient<WebBaselineLogoutCookieEvents>();

                // Access token management with DPoP and private_key_jwt
                services.AddOpenIdConnectAccessTokenManagement(options =>
                {
                    options.DPoPJsonWebKey = dpopKey;
                });
                services.AddSingleton(signingCredentials);
                services.AddTransient<IClientAssertionService, WebBaselineClientAssertionService>();

                // HTTP client for calling the DPoP-protected API
                services.AddUserAccessTokenHttpClient("api", configureClient: client =>
                {
                    client.BaseAddress = _api!.BuildUri();
                });
            },
            configureApp: app =>
            {
                // Back-channel logout endpoint
                app.MapPost("/backchannel-logout", async (
                    [FromForm] string logout_token,
                    [FromServices] WebBaselineLogoutSessionManager sessions,
                    [FromServices] IDiscoveryCache disco,
                    HttpResponse response) =>
                {
                    response.Headers.Append("Cache-Control", "no-cache, no-store");
                    response.Headers.Append("Pragma", "no-cache");

                    try
                    {
                        var user = await ValidateLogoutToken(logout_token, disco, clientId);
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

        // 6. Register the client with IdentityServer
        _identityServer.AddClient(_webApp, c =>
        {
            c.ClientId = _webApp.Name;
            c.ClientName = "Web Security Baseline";
            c.RequireConsent = false;
            c.AllowedGrantTypes = GrantTypes.Code;
            c.RequirePkce = true;
            c.RequireDPoP = true;
            c.RequireRequestObject = true;
            c.RequirePushedAuthorization = true;
            c.AllowOfflineAccess = true;
            c.RefreshTokenUsage = TokenUsage.ReUse;
            c.BackChannelLogoutUri = _webApp.BuildUri("backchannel-logout").ToString();
            c.BackChannelLogoutSessionRequired = true;
            c.ClientSecrets =
            [
                new Secret
                {
                    Type = IdentityServerConstants.SecretTypes.JsonWebKey,
                    Value = publicJwkJson
                }
            ];
            c.AllowedScopes =
            [
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                "resource1.scope1"
            ];
            c.RedirectUris = [_webApp.BuildUri("signin-oidc").ToString()];
            c.PostLogoutRedirectUris = [_webApp.BuildUri("signout-callback-oidc").ToString()];
            c.FrontChannelLogoutUri = _webApp.BuildUri("signout-oidc").ToString();
        });

        Links = [_identityServer.Link, _webApp.Link, _api.Link];
    }

    private static async Task<ClaimsPrincipal> ValidateLogoutToken(
        string logoutToken,
        IDiscoveryCache discoveryCache,
        string clientId)
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
            ValidAudience = clientId,
            IssuerSigningKeys = keys,
            NameClaimType = JwtClaimTypes.Name,
            RoleClaimType = JwtClaimTypes.Role
        };

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();

        var user = handler.ValidateToken(logoutToken, parameters, out _);

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

        _rsaSigningKey?.Dispose();
        _dpopRsaKey?.Dispose();
    }

    public Command[] GetCommands() => [];

    public class Tests(ScenarioFixture<WebSecurityBaseline> fixture) : PageTest, IClassFixture<ScenarioFixture<WebSecurityBaseline>>
    {
        public override BrowserNewContextOptions ContextOptions() => new()
        {
            IgnoreHTTPSErrors = true
        };

        [Fact]
        public async Task Login_with_full_security_baseline()
        {
            var webappUrl = fixture.Link("web").ToString();

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

            // Access token should be DPoP-bound
            body.ShouldContain("DPoP");
        }

        [Fact]
        public async Task Call_dpop_protected_api()
        {
            var webappUrl = fixture.Link("web").ToString();

            // Login
            await Page.GotoAsync(webappUrl);
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=Claims");

            // Call DPoP-protected API
            await Page.GetByRole(AriaRole.Link, new() { Name = "Call API" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=API Response");

            var apiBody = await Page.TextContentAsync("body");
            apiBody.ShouldNotBeNull();
            apiBody.ShouldContain("client_id");
        }

        [Fact]
        public async Task Back_channel_logout_invalidates_session()
        {
            var webappUrl = fixture.Link("web").ToString();
            var identityServerUrl = fixture.Link("identity-server");
            var endSessionUrl = new Uri(identityServerUrl, "connect/endsession").ToString();

            // Login
            await Page.GotoAsync(webappUrl);
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=Claims");

            // Confirm the client cookie exists before logout
            var cookiesBeforeLogout = await Context.CookiesAsync([webappUrl]);
            cookiesBeforeLogout.ShouldContain(cookie => cookie.Name == "web");

            // Trigger logout directly at IdentityServer, bypassing the client's
            // own logout page (which would delete the cookie locally).
            await Page.GotoAsync(endSessionUrl);
            await Page.WaitForSelectorAsync("text=Logout");
            var yesButton = Page.GetByRole(AriaRole.Button, new() { Name = "Yes" });
            if (await yesButton.IsVisibleAsync())
            {
                await yesButton.ClickAsync();
            }

            await Page.WaitForSelectorAsync("text=logged out");

            // The back-channel notification cannot remove a browser cookie, so it
            // should still be present until the client validates it server-side.
            var cookiesAfterLogout = await Context.CookiesAsync([webappUrl]);
            cookiesAfterLogout.ShouldContain(cookie => cookie.Name == "web");

            // Navigate back to the client. The cookie validation hook detects the
            // back-channel invalidation, rejects the principal, and signs out locally.
            await Page.GotoAsync(webappUrl);
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).Not.ToBeVisibleAsync();

            // Attempting to access a protected page should redirect to the IdP login
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");
        }
    }
}

/// <summary>
/// OpenID Connect events that implement JAR (signed authorization request via PAR)
/// and private_key_jwt client authentication at the token endpoint.
/// </summary>
internal sealed class WebBaselineOidcEvents(SigningCredentials signingCredentials, string authority) : OpenIdConnectEvents
{
    private static readonly JwtSecurityTokenHandler TokenHandler = CreateTokenHandler();

    public override Task AuthorizationCodeReceived(AuthorizationCodeReceivedContext context)
    {
        context.TokenEndpointRequest!.ClientAssertionType = OidcConstants.ClientAssertionTypes.JwtBearer;
        context.TokenEndpointRequest.ClientAssertion = CreateClientAssertion(context.Options.ClientId ?? throw new InvalidOperationException());
        return Task.CompletedTask;
    }

    public override Task PushAuthorization(PushedAuthorizationContext context)
    {
        var request = SignAuthorizationRequest(context.ProtocolMessage);
        var clientId = context.ProtocolMessage.ClientId;

        context.ProtocolMessage.Parameters.Clear();
        context.ProtocolMessage.ClientId = clientId;
        context.ProtocolMessage.ClientAssertionType = OidcConstants.ClientAssertionTypes.JwtBearer;
        context.ProtocolMessage.ClientAssertion = CreateClientAssertion(clientId);
        context.ProtocolMessage.SetParameter("request", request);

        return Task.CompletedTask;
    }

    private string CreateClientAssertion(string clientId)
    {
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            clientId,
            authority + "/connect/token",
            [
                new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString()),
                new Claim(JwtClaimTypes.Subject, clientId),
                new Claim(JwtClaimTypes.IssuedAt, ((DateTimeOffset)now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            ],
            now,
            now.AddMinutes(1),
            signingCredentials
        );

        return TokenHandler.WriteToken(token);
    }

    private string SignAuthorizationRequest(OpenIdConnectMessage message)
    {
        var now = DateTime.UtcNow;

        var claims = message.Parameters.Select(p => new Claim(p.Key, p.Value)).ToList();

        var token = new JwtSecurityToken(
            message.ClientId,
            authority,
            claims,
            now,
            now.AddMinutes(1),
            signingCredentials
        );

        return TokenHandler.WriteToken(token);
    }

    private static JwtSecurityTokenHandler CreateTokenHandler()
    {
        var handler = new JwtSecurityTokenHandler();
        handler.OutboundClaimTypeMap.Clear();
        return handler;
    }
}

/// <summary>
/// Provides private_key_jwt client assertions for Duende.AccessTokenManagement token refresh.
/// </summary>
internal sealed class WebBaselineClientAssertionService(
    SigningCredentials signingCredentials,
    IOptionsMonitor<OpenIdConnectOptions> oidcOptions) : IClientAssertionService
{
    private static readonly JwtSecurityTokenHandler TokenHandler = CreateTokenHandler();

    public Task<ClientAssertion?> GetClientAssertionAsync(
        ClientCredentialsClientName? clientName = null,
        TokenRequestParameters? parameters = null,
        CancellationToken ct = default) =>
        Task.FromResult<ClientAssertion?>(new ClientAssertion
        {
            Type = OidcConstants.ClientAssertionTypes.JwtBearer,
            Value = CreateAssertionJwt()
        });

    private string CreateAssertionJwt()
    {
        var now = DateTime.UtcNow;
        var options = oidcOptions.Get("oidc");
        var clientId = options.ClientId ?? throw new InvalidOperationException("ClientId not configured");
        var optionsAuthority = options.Authority?.TrimEnd('/') ?? "";

        var token = new JwtSecurityToken(
            clientId,
            optionsAuthority + "/connect/token",
            [
                new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString()),
                new Claim(JwtClaimTypes.Subject, clientId),
                new Claim(JwtClaimTypes.IssuedAt, ((DateTimeOffset)now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            ],
            now,
            now.AddMinutes(1),
            signingCredentials
        );

        return TokenHandler.WriteToken(token);
    }

    private static JwtSecurityTokenHandler CreateTokenHandler()
    {
        var handler = new JwtSecurityTokenHandler();
        handler.OutboundClaimTypeMap.Clear();
        return handler;
    }
}

/// <summary>
/// Tracks sessions that have been logged out via back-channel notification.
/// </summary>
internal sealed class WebBaselineLogoutSessionManager
{
    private readonly ConcurrentBag<(string? Sub, string? Sid)> _sessions = [];

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
internal sealed class WebBaselineLogoutCookieEvents(WebBaselineLogoutSessionManager logoutSessions) : CookieAuthenticationEvents
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
