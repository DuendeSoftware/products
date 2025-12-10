// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.AspNetCore.Authentication.JwtBearer.DPoP;
using Duende.AspNetCore.TestFramework;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Xunit.Abstractions;

namespace Duende.AspNetCore.Authentication.JwtBearer;

public class DPoPIntegrationTests(ITestOutputHelper testOutputHelper)
{
    private Client DPoPOnlyClient = new()
    {
        ClientId = "client1",
        ClientSecrets = [new Secret("secret".ToSha256())],
        RequireDPoP = true,
        AllowedScopes = ["openid", "profile", "scope1"],
        AllowedGrantTypes = GrantTypes.Code,
        RedirectUris = ["https://app/signin-oidc"],
        PostLogoutRedirectUris = ["https://app/signout-callback-oidc"]
    };

    [Fact]
    [Trait("Category", "Integration")]
    public async Task missing_token_fails()
    {
        var api = await CreateDPoPApi();

        var result = await api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task incorrect_token_type_fails()
    {
        var api = await CreateDPoPApi();
        var bearerToken = "unimportant opaque value";
        api.HttpClient.SetBearerToken(bearerToken);

        var result = await api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task multiple_proof_tokens_fails()
    {
        var identityServer = await CreateIdentityServer();
        identityServer.Clients.Add(DPoPOnlyClient);
        var jwk = CreateJwk();
        var api = await CreateDPoPApi();

        var app = new AppHost(identityServer, api, "client1", testOutputHelper,
            configureUserTokenManagementOptions: opt => opt.DPoPJsonWebKey = jwk);
        await app.Initialize();

        // Login and get token for api call
        await app.LoginAsync("sub");
        var response = await app.BrowserClient.GetAsync(app.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserToken>();
        token.ShouldNotBeNull();
        token.AccessToken.ToString().ShouldNotBeNull();
        token.DPoPJsonWebKey.ShouldNotBeNull();
        api.HttpClient.SetToken("DPoP", token.AccessToken);

        // Create proof token for api call
        var dpopService = app.Server.Services.GetRequiredService<IDPoPProofService>();
        var proof = await dpopService.CreateProofTokenAsync(new DPoPProofRequest
        {
            AccessToken = token.AccessToken,
            DPoPProofKey = jwk,
            Method = HttpMethod.Get,
            Url = new Uri("http://localhost/")
        });
        proof.ShouldNotBeNull();
        api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, [proof.Value, proof.Value]);

        var result = await api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var error = result.Headers.GetValues(HeaderNames.WWWAuthenticate).FirstOrDefault();
        error.ShouldBe("DPoP error=\"invalid_request\"");
    }

    [Theory]
    [InlineData("DPOP")] // upper
    [InlineData("dpop")] // lower
    [InlineData("DPoP")] // mixed, but normal
    [InlineData("dpOP")] // nonsense
    [Trait("Category", "Integration")]
    public async Task valid_token_and_proof_succeeds_with_case_insensitive_http_header_authentication_scheme(string scheme)
    {
        var identityServer = await CreateIdentityServer();
        identityServer.Clients.Add(DPoPOnlyClient);
        var jwk = CreateJwk();
        var api = await CreateDPoPApi();

        var app = new AppHost(identityServer, api, "client1", testOutputHelper,
            configureUserTokenManagementOptions: opt => opt.DPoPJsonWebKey = jwk);
        await app.Initialize();

        // Login and get token for api call
        await app.LoginAsync("sub");
        var response = await app.BrowserClient.GetAsync(app.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserToken>();
        token.ShouldNotBeNull();
        token.AccessToken.ToString().ShouldNotBeNull();
        token.DPoPJsonWebKey.ShouldNotBeNull();
        api.HttpClient.SetToken(scheme, token.AccessToken);

        // Create proof token for api call
        var dpopService = app.Server.Services.GetRequiredService<IDPoPProofService>();
        var proof = await dpopService.CreateProofTokenAsync(new DPoPProofRequest
        {
            AccessToken = token.AccessToken,
            DPoPProofKey = jwk,
            Method = HttpMethod.Get,
            Url = new Uri("http://localhost/")
        });
        proof.ShouldNotBeNull();
        api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, proof.Value);

        var result = await api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task access_token_without_proof_token_should_fail()
    {
        var identityServer = await CreateIdentityServer();
        identityServer.Clients.Add(DPoPOnlyClient);
        var jwk = CreateJwk();
        var api = await CreateDPoPApi();

        var app = new AppHost(identityServer, api, "client1", testOutputHelper,
            configureUserTokenManagementOptions: opt => opt.DPoPJsonWebKey = jwk);
        await app.Initialize();

        // Login and get token for api call
        await app.LoginAsync("sub");
        var response = await app.BrowserClient.GetAsync(app.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserToken>();
        token.ShouldNotBeNull();
        token.AccessToken.ToString().ShouldNotBeNull();
        token.DPoPJsonWebKey.ShouldNotBeNull();
        api.HttpClient.SetToken("DPoP", token.AccessToken);

        var result = await api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var error = result.Headers.GetValues(HeaderNames.WWWAuthenticate).FirstOrDefault();
        error.ShouldBe("DPoP error=\"invalid_request\"");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task excessively_large_proof_fails()
    {
        var identityServer = await CreateIdentityServer(idsrv =>
        {
            idsrv.Clients.Add(DPoPOnlyClient);
        });

        var jwk = CreateJwk();
        var maxLength = 50;
        var api = await CreateDPoPApi(opt => opt.ProofTokenMaxLength = maxLength);

        var app = new AppHost(identityServer, api, "client1", testOutputHelper,
            configureUserTokenManagementOptions: opt => opt.DPoPJsonWebKey = jwk);
        await app.Initialize();

        // Login and get token for api call
        await app.LoginAsync("sub");
        var response = await app.BrowserClient.GetAsync(app.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserToken>();
        token.ShouldNotBeNull();
        token.AccessToken.ToString().ShouldNotBeNull();
        token.DPoPJsonWebKey.ShouldNotBeNull();
        api.HttpClient.SetToken(OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP, token.AccessToken);

        // Create proof token for api call
        var dpopService = app.Server.Services.GetRequiredService<IDPoPProofService>();
        var proof = await dpopService.CreateProofTokenAsync(new DPoPProofRequest
        {
            AccessToken = token.AccessToken,
            DPoPProofKey = jwk,
            Method = HttpMethod.Get,
            Url = new Uri("http://localhost/"),
            DPoPNonce = DPoPNonce.Parse(new string('x', maxLength + 1)) // <--- Most important part of the test
        });
        proof.ShouldNotBeNull();
        api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, proof.Value);

        var result = await api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task missing_nonce_generates_server_issued_nonce()
    {
        var identityServer = await CreateIdentityServer();
        identityServer.Clients.Add(DPoPOnlyClient);
        var jwk = CreateJwk();

        // Configure API to require nonce validation
        var api = await CreateDPoPApi(opt =>
        {
            opt.ValidationMode = ExpirationValidationMode.Nonce;
        });

        var app = new AppHost(identityServer, api, "client1", testOutputHelper,
            configureUserTokenManagementOptions: opt => opt.DPoPJsonWebKey = jwk);
        await app.Initialize();

        // Login and get token for api call
        await app.LoginAsync("sub");
        var response = await app.BrowserClient.GetAsync(app.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserToken>();
        token.ShouldNotBeNull();
        token.AccessToken.ToString().ShouldNotBeNull();
        token.DPoPJsonWebKey.ShouldNotBeNull();
        api.HttpClient.SetToken("DPoP", token.AccessToken);

        // Create proof token WITHOUT a nonce for api call
        var dpopService = app.Server.Services.GetRequiredService<IDPoPProofService>();
        var proof = await dpopService.CreateProofTokenAsync(new DPoPProofRequest
        {
            AccessToken = token.AccessToken,
            DPoPProofKey = jwk,
            Method = HttpMethod.Get,
            Url = new Uri("http://localhost/")
            // No DPoPNonce provided
        });
        proof.ShouldNotBeNull();
        api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, proof.Value);

        // Expect failure with a server-issued nonce
        var result = await api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Verify that a nonce was issued in the response
        result.Headers.TryGetValues("DPoP-Nonce", out var nonceValues).ShouldBeTrue();
        var serverNonce = nonceValues?.FirstOrDefault();
        serverNonce.ShouldNotBeNull();
        serverNonce.ShouldNotBeEmpty();

        // Verify the WWW-Authenticate header contains the use_dpop_nonce error
        var wwwAuthValues = result.Headers.GetValues(HeaderNames.WWWAuthenticate).FirstOrDefault();
        wwwAuthValues.ShouldNotBeNull();
        wwwAuthValues.ShouldContain("use_dpop_nonce");

        // Make a new request with the server issued nonce
        var proofWithNonce = await dpopService.CreateProofTokenAsync(new DPoPProofRequest
        {
            AccessToken = token.AccessToken,
            DPoPProofKey = jwk,
            Method = HttpMethod.Get,
            Url = new Uri("http://localhost/"),
            DPoPNonce = DPoPNonce.Parse(serverNonce)
        });
        api.HttpClient.DefaultRequestHeaders.Remove(OidcConstants.HttpHeaders.DPoP);
        api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, proofWithNonce.Value);
        var secondResult = await api.HttpClient.GetAsync("/");
        secondResult.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task custom_nonce_validator_generates_and_validates_custom_nonce()
    {
        var identityServer = await CreateIdentityServer();
        identityServer.Clients.Add(DPoPOnlyClient);
        var jwk = CreateJwk();

        // Create API with custom nonce validator
        var baseAddress = "https://api";
        var api = new ApiHost(identityServer, testOutputHelper, baseAddress);
        var customValidator = new CustomDPoPNonceValidator();
        api.OnConfigureServices += services =>
        {
            // Register custom nonce validator before calling ConfigureDPoPTokensForScheme
            services.AddSingleton<IDPoPNonceValidator>(customValidator);

            services.ConfigureDPoPTokensForScheme(ApiHost.AuthenticationScheme,
                opt =>
                {
                    opt.AllowBearerTokens = false;
                    opt.ValidationMode = ExpirationValidationMode.Nonce;
                });
        };
        api.OnConfigure += app =>
            app.MapGet("/", () => "default route")
                .RequireAuthorization();
        await api.Initialize();

        var app = new AppHost(identityServer, api, "client1", testOutputHelper,
            configureUserTokenManagementOptions: opt => opt.DPoPJsonWebKey = jwk);
        await app.Initialize();

        // Login and get token for api call
        await app.LoginAsync("sub");
        var response = await app.BrowserClient.GetAsync(app.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserToken>();
        token.ShouldNotBeNull();
        token.AccessToken.ToString().ShouldNotBeNull();
        token.DPoPJsonWebKey.ShouldNotBeNull();
        api.HttpClient.SetToken("DPoP", token.AccessToken);

        // Create proof token WITHOUT a nonce for api call
        var dpopService = app.Server.Services.GetRequiredService<IDPoPProofService>();
        var proof = await dpopService.CreateProofTokenAsync(new DPoPProofRequest
        {
            AccessToken = token.AccessToken,
            DPoPProofKey = jwk,
            Method = HttpMethod.Get,
            Url = new Uri("http://localhost/")
        });
        proof.ShouldNotBeNull();
        api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, proof.Value);

        // Expect failure with a custom server-issued nonce
        var result = await api.HttpClient.GetAsync("/");
        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Verify that a custom nonce was issued in the response
        result.Headers.TryGetValues("DPoP-Nonce", out var nonceValues).ShouldBeTrue();
        var serverNonce = nonceValues?.FirstOrDefault();
        serverNonce.ShouldNotBeNull();
        serverNonce.ShouldStartWith("custom-nonce-");

        // Make a new request with the custom server issued nonce
        var proofWithNonce = await dpopService.CreateProofTokenAsync(new DPoPProofRequest
        {
            AccessToken = token.AccessToken,
            DPoPProofKey = jwk,
            Method = HttpMethod.Get,
            Url = new Uri("http://localhost/"),
            DPoPNonce = DPoPNonce.Parse(serverNonce)
        });
        api.HttpClient.DefaultRequestHeaders.Remove(OidcConstants.HttpHeaders.DPoP);
        api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, proofWithNonce.Value);
        var secondResult = await api.HttpClient.GetAsync("/");
        secondResult.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public async Task<IdentityServerHost> CreateIdentityServer(Action<IdentityServerHost>? setup = null)
    {
        var host = new IdentityServerHost(testOutputHelper);
        setup?.Invoke(host);
        await host.Initialize();
        return host;
    }

    private async Task<ApiHost> CreateDPoPApi(Action<DPoPOptions>? configureDPoP = null)
    {
        var baseAddress = "https://api";
        var identityServer = await CreateIdentityServer();
        var api = new ApiHost(identityServer, testOutputHelper, baseAddress);
        api.OnConfigureServices += services =>
        {
            services.ConfigureDPoPTokensForScheme(ApiHost.AuthenticationScheme,
                opt =>
                {
                    opt.AllowBearerTokens = false;
                    configureDPoP?.Invoke(opt);
                });
        };
        api.OnConfigure += app =>
            app.MapGet("/", () => "default route")
                .RequireAuthorization();
        await api.Initialize();
        return api;
    }

    private DPoPProofKey CreateJwk()
    {
        var rsaKey = new RsaSecurityKey(RSA.Create(2048));
        var jwkKey = JsonWebKeyConverter.ConvertFromSecurityKey(rsaKey);
        jwkKey.Alg = "RS256";
        var jwk = JsonSerializer.Serialize(jwkKey);
        return DPoPProofKey.Parse(jwk);
    }
}
