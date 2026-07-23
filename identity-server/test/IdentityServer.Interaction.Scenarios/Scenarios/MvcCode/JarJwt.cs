// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using Shouldly;

namespace Duende.IdentityServer.Interaction.Scenarios.MvcCode;

/// <summary>
/// Scenario: Code flow with JAR (JWT Authorization Request) sent via PAR (Pushed Authorization Request),
/// and private_key_jwt client authentication at the token endpoint.
/// The client authenticates using a signed JWT assertion (no shared secret).
/// </summary>
public sealed class JarJwt : IScenario
{
    private IdentityServerTestHost? _identityServer;
    private ClientWebAppTestHost? _webApp;
    private ApiHost? _api;
    private RSA? _rsaKey;

    public string Name => "JarJwt";
    public string Description => "JAR + PAR with private_key_jwt client authentication";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        // 1. Start IdentityServer with JWT bearer client authentication support
        _identityServer = new IdentityServerTestHost(configurator, "identity-server",
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

        // 4. Start the MVC client with JAR/PAR and private_key_jwt
        _webApp = new ClientWebAppTestHost(configurator,
            _identityServer,
            _api,
            name: "mvc-jar",
            configureOpenIdConnect: options =>
            {
                options.ResponseType = "code";
                options.UsePkce = true;

                // No client secret — we use private_key_jwt
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

                // Wire up JAR/PAR and private_key_jwt via events
                options.Events = new JarJwtOidcEvents(signingCredentials, authority);
            },
            configureServices: services =>
            {
                // Register access token management with private_key_jwt for token refresh
                services.AddOpenIdConnectAccessTokenManagement();
                services.AddSingleton(signingCredentials);
                services.AddTransient<IClientAssertionService, JarJwtClientAssertionService>();

                // HTTP client for calling the API with managed tokens
                services.AddUserAccessTokenHttpClient("api", configureClient: client =>
                {
                    // BaseAddress will be set after API starts
                });
            });

        await _webApp.StartAsync(ct);

        // 5. Register the client with JWK-only secret (no shared secret)
        _identityServer.AddClient(_webApp, c =>
        {
            c.ClientId = _webApp.Name;
            c.ClientName = "JAR/JWT Client";
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

    public class Tests(ScenarioFixture<JarJwt> fixture) : PageTest, IClassFixture<ScenarioFixture<JarJwt>>
    {
        public override BrowserNewContextOptions ContextOptions() => new()
        {
            IgnoreHTTPSErrors = true
        };

        [Fact]
        public async Task Login_with_jar_par_and_private_key_jwt()
        {
            var webappUrl = fixture.Link("mvc-jar").ToString();

            // 1. Navigate to the webapp
            await Page.GotoAsync(webappUrl);

            // 2. Click "Secure" — triggers JAR/PAR code flow
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
    }
}

/// <summary>
/// OpenID Connect events that implement JAR (signed authorization request via PAR)
/// and private_key_jwt client authentication at the token endpoint.
/// </summary>
internal sealed class JarJwtOidcEvents(SigningCredentials signingCredentials, string authority) : OpenIdConnectEvents
{
    public override Task AuthorizationCodeReceived(AuthorizationCodeReceivedContext context)
    {
        // Authenticate at the token endpoint using private_key_jwt
        context.TokenEndpointRequest!.ClientAssertionType = OidcConstants.ClientAssertionTypes.JwtBearer;
        context.TokenEndpointRequest.ClientAssertion = CreateClientAssertion(context.Options.ClientId ?? throw new InvalidOperationException());
        return Task.CompletedTask;
    }

    public override Task PushAuthorization(PushedAuthorizationContext context)
    {
        // Sign the entire authorization request as a JWT (JAR)
        var signedRequest = SignAuthorizationRequest(context.ProtocolMessage);
        var clientId = context.ProtocolMessage.ClientId;

        // Clear all parameters and send only the signed request + client auth
        context.ProtocolMessage.Parameters.Clear();
        context.ProtocolMessage.ClientId = clientId;
        context.ProtocolMessage.ClientAssertionType = OidcConstants.ClientAssertionTypes.JwtBearer;
        context.ProtocolMessage.ClientAssertion = CreateClientAssertion(clientId);
        context.ProtocolMessage.SetParameter("request", signedRequest);

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
/// Provides private_key_jwt client assertions for both Duende.AccessTokenManagement
/// and the shared RenewTokens UI page.
/// </summary>
internal sealed class JarJwtClientAssertionService(SigningCredentials signingCredentials, IOptionsMonitor<OpenIdConnectOptions> oidcOptions)
    : IClientAssertionService
{
    private string CreateAssertionJwt()
    {
        var now = DateTime.UtcNow;
        var authority = oidcOptions.Get("oidc").Authority?.TrimEnd('/') ?? "";

        var token = new JwtSecurityToken(
            "mvc-jar",
            authority + "/connect/token",
            [
                new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString()),
                new Claim(JwtClaimTypes.Subject, "mvc-jar"),
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

    public Task<ClientAssertion?> GetClientAssertionAsync(ClientCredentialsClientName? clientName = null, TokenRequestParameters? parameters = null, CancellationToken ct = default) =>
        Task.FromResult<ClientAssertion?>(new ClientAssertion
        {
            Type = OidcConstants.ClientAssertionTypes.JwtBearer,
            Value = CreateAssertionJwt()
        });
}
