// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.Api;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Interaction.SharedHosts.MvcClient;
using Duende.IdentityServer.Interaction.Tests.Infrastructure;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using Shouldly;

namespace Duende.IdentityServer.Interaction.Scenarios.MvcCode;

/// <summary>
/// Scenario: Code flow with JAR (JWT Authorization Request) sent via request_uri
/// (self-hosted on the client), and private_key_jwt client authentication at the token endpoint.
/// PAR is explicitly disabled; instead the client hosts the signed request object at a /ro endpoint
/// and passes the URI to the authorize endpoint.
/// </summary>
public sealed class JarUriJwt : IScenario
{
    private IdentityServerTestHost? _identityServer;
    private ClientWebAppTestHost? _webApp;
    private ApiHost? _api;
    private RSA? _rsaKey;

    public string Name => "JarUriJwt";
    public string Description => "JAR via request_uri with private_key_jwt client authentication";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        // 1. Start IdentityServer with JWT bearer client authentication and request_uri support
        _identityServer = new IdentityServerTestHost(configurator, "identity-server",
            configureOptions: options =>
            {
                options.Endpoints.EnableJwtRequestUri = true;
            },
            configureIdentityServer: isBuilder =>
            {
                isBuilder.AddJwtBearerClientAuthentication();
            });
        _identityServer.AddDefaultUsers();
        _identityServer.AddDefaultResources();
        await _identityServer.StartAsync(ct);

        var authority = _identityServer.BuildUri().ToString().TrimEnd('/');

        // 2. Start the API
        _api = new ApiHost(configurator, "api", authority);
        await _api.StartAsync(ct);

        // 3. Generate RSA key for client authentication and request signing
        _rsaKey = RSA.Create(2048);
        var rsaSecurityKey = new RsaSecurityKey(_rsaKey)
        {
            KeyId = Guid.NewGuid().ToString("N")
        };
        var signingCredentials = new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256);
        var publicJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaSecurityKey);
        var publicJwkJson = JsonSerializer.Serialize(publicJwk);

        // 4. Service to store request objects for the request_uri endpoint
        var requestUriStore = new RequestUriStore();

        // 5. URL provider that will be populated after the webapp starts
        var urlProvider = new WebAppUrlProvider();

        // 6. Start the MVC client with JAR via request_uri and private_key_jwt
        _webApp = new ClientWebAppTestHost(configurator,
            _identityServer,
            _api,
            name: "mvc-jar-uri",
            configureCookie: cookieOptions =>
            {
                cookieOptions.Events.OnSigningOut = async e =>
                {
                    // Revoke the refresh token on signout
                    await e.HttpContext.RevokeRefreshTokenAsync();
                };
            },
            configureOpenIdConnect: options =>
            {
                options.ResponseType = "code";
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

                // Disable PAR because we use request_uri instead
                options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;

                // Wire up JAR via request_uri and private_key_jwt via events
                options.Events = new JarUriJwtOidcEvents(signingCredentials, requestUriStore, urlProvider, authority);
            },
            configureServices: services =>
            {
                // Register access token management with private_key_jwt for token refresh
                services.AddOpenIdConnectAccessTokenManagement();
                services.AddSingleton(signingCredentials);
                services.AddTransient<IClientAssertionService, JarUriJwtClientAssertionService>();

                // HTTP client for calling the API with managed tokens
                services.AddUserAccessTokenHttpClient("api", configureClient: client =>
                {
                    // BaseAddress will be set by the shared infrastructure
                });
            },
            configureApp: app =>
            {
                // Host the request_uri endpoint that IdentityServer will fetch
                app.MapGet("/ro", (string id) =>
                {
                    var value = requestUriStore.Get(id);
                    return value is not null
                        ? Results.Content(value, "application/oauth-authz-req+jwt")
                        : Results.NotFound();
                }).AllowAnonymous();
            });

        await _webApp.StartAsync(ct);

        // Now that the webapp is started, set the URL provider
        urlProvider.BaseUrl = _webApp.BuildUri().ToString().TrimEnd('/');

        // 6. Register the client with JWK-only secret (no shared secret)
        _identityServer.AddClient(_webApp, c =>
        {
            c.ClientId = _webApp.Name;
            c.ClientName = "JAR URI/JWT Client";
            c.RequireConsent = false;
            c.AllowedGrantTypes = GrantTypes.Code;
            c.RequirePkce = true;
            c.AllowOfflineAccess = true;
            c.RefreshTokenUsage = TokenUsage.ReUse;
            c.RequireRequestObject = true;
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

        _rsaKey?.Dispose();
    }

    public Command[] GetCommands() => [];

    public class Tests(ScenarioFixture<JarUriJwt> fixture) : PageTest, IClassFixture<ScenarioFixture<JarUriJwt>>
    {
        public override BrowserNewContextOptions ContextOptions() => new()
        {
            IgnoreHTTPSErrors = true
        };

        [Fact]
        public async Task Login_with_jar_request_uri_and_private_key_jwt()
        {
            var webappUrl = fixture.Link("mvc-jar-uri").ToString();

            // 1. Navigate to the webapp
            await Page.GotoAsync(webappUrl);

            // 2. Click "Secure" to trigger the code flow with JAR via request_uri
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");

            // 3. Sign in as alice
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
        public async Task Call_api_with_managed_access_token()
        {
            var webappUrl = fixture.Link("mvc-jar-uri").ToString();

            // 1. Login first
            await Page.GotoAsync(webappUrl);
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=Claims");

            // 2. Call the API (exercises token management and IClientAssertionService)
            await Page.GetByRole(AriaRole.Link, new() { Name = "Call API" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=sub");

            var body = await Page.TextContentAsync("body");
            body.ShouldNotBeNull();
            body.ShouldContain("sub");
        }
    }
}

/// <summary>
/// In-memory store for request objects served at the /ro endpoint.
/// </summary>
internal sealed class RequestUriStore
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    public string Set(string value)
    {
        var id = Guid.NewGuid().ToString();
        _store.TryAdd(id, value);
        return id;
    }

    public string? Get(string id) => _store.TryGetValue(id, out var value) ? value : null;
}

/// <summary>
/// Provides the webapp base URL once it becomes available after startup.
/// </summary>
internal sealed class WebAppUrlProvider
{
    private string? _baseUrl;

    public string BaseUrl
    {
        get => _baseUrl ?? throw new InvalidOperationException("WebAppUrlProvider.BaseUrl has not been set. The webapp must be started first.");
        set => _baseUrl = value;
    }
}

/// <summary>
/// OpenID Connect events that implement JAR (signed authorization request via request_uri)
/// and private_key_jwt client authentication at the token endpoint.
/// </summary>
internal sealed class JarUriJwtOidcEvents(
    SigningCredentials signingCredentials,
    RequestUriStore requestUriStore,
    WebAppUrlProvider urlProvider,
    string authority) : OpenIdConnectEvents
{
    public override Task AuthorizationCodeReceived(AuthorizationCodeReceivedContext context)
    {
        // Authenticate at the token endpoint using private_key_jwt
        context.TokenEndpointRequest!.ClientAssertionType = OidcConstants.ClientAssertionTypes.JwtBearer;
        context.TokenEndpointRequest.ClientAssertion = CreateClientAssertion(context.Options.ClientId ?? throw new InvalidOperationException());
        return Task.CompletedTask;
    }

    public override Task RedirectToIdentityProvider(RedirectContext context)
    {
        // Sign the entire authorization request as a JWT (JAR)
        var signedRequest = SignAuthorizationRequest(context.ProtocolMessage);
        var id = requestUriStore.Set(signedRequest);

        var clientId = context.ProtocolMessage.ClientId;
        var redirectUri = context.ProtocolMessage.RedirectUri;

        // Clear all parameters and send only client_id and request_uri
        context.ProtocolMessage.Parameters.Clear();
        context.ProtocolMessage.ClientId = clientId;
        context.ProtocolMessage.RedirectUri = redirectUri;
        context.ProtocolMessage.SetParameter("request_uri", $"{urlProvider.BaseUrl}/ro?id={id}");

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

        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.OutboundClaimTypeMap.Clear();
        return tokenHandler.WriteToken(token);
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

        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.OutboundClaimTypeMap.Clear();
        return tokenHandler.WriteToken(token);
    }
}

/// <summary>
/// Provides private_key_jwt client assertions for Duende.AccessTokenManagement token refresh.
/// </summary>
internal sealed class JarUriJwtClientAssertionService(
    SigningCredentials signingCredentials,
    IOptionsMonitor<OpenIdConnectOptions> oidcOptions) : IClientAssertionService
{
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

        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.OutboundClaimTypeMap.Clear();
        return tokenHandler.WriteToken(token);
    }
}
