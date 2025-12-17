// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.AspNetCore.Authentication.JwtBearer.TestFramework;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Xunit.Abstractions;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

public class DPoPIntegrationTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public DPoPIntegrationTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        IdentityServer = CreateIdentityServer();
        App = CreateApp();
        Api = CreateApi();
        Jwk = CreateJwk();
    }

    private IdentityServerHost IdentityServer { get; }
    private ApiHost Api { get; }
    private AppHost App { get; }
    private DPoPProofKey Jwk { get; }

    private Action<DPoPOptions>? ApiOptions { get; set; }

    [Fact]
    public async Task missing_token_fails()
    {
        await Initialize();
        var result = await Api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task incorrect_token_type_fails()
    {
        await Initialize();
        var bearerToken = "unimportant opaque value";
        Api.HttpClient.SetBearerToken(bearerToken);

        var result = await Api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task multiple_proof_tokens_fails()
    {
        await Initialize();
        var token = await LoginAndGetToken();
        Api.HttpClient.SetToken("DPoP", token.AccessToken);

        var proof = await CreateProofToken(token, Jwk, HttpMethod.Get, new Uri("http://localhost/"));
        Api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, [proof, proof]);

        var result = await Api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var error = result.Headers.GetValues(HeaderNames.WWWAuthenticate).FirstOrDefault();
        error.ShouldBe("DPoP error=\"invalid_request\"");
    }

    [Theory]
    [InlineData("DPOP")] // upper
    [InlineData("dpop")] // lower
    [InlineData("DPoP")] // mixed, but normal
    [InlineData("dpOP")] // nonsense
    public async Task valid_token_and_proof_succeeds_with_case_insensitive_http_header_authentication_scheme(string scheme)
    {
        await Initialize();
        var token = await LoginAndGetToken();
        Api.HttpClient.SetToken(scheme, token.AccessToken);

        var proof = await CreateProofToken(token, Jwk, HttpMethod.Get, new Uri("http://localhost/"));
        Api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, proof);

        var result = await Api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task replayed_proofs_fail_when_replay_detection_is_enabled(bool enableReplayDetection)
    {
        ApiOptions = opt => opt.EnableReplayDetection = enableReplayDetection;
        await Initialize();
        var token = await LoginAndGetToken();
        Api.HttpClient.SetToken("DPoP", token.AccessToken);

        var proof = await CreateProofToken(token, Jwk, HttpMethod.Get, new Uri("http://localhost/"));
        Api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, proof);

        var result = await Api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Attempt to reuse the proof
        var secondResult = await Api.HttpClient.GetAsync("/");
        if (enableReplayDetection)
        {
            secondResult.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
        else
        {
            secondResult.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task access_token_without_proof_token_should_fail()
    {
        await Initialize();
        var token = await LoginAndGetToken();
        Api.HttpClient.SetToken("DPoP", token.AccessToken);

        var result = await Api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var error = result.Headers.GetValues(HeaderNames.WWWAuthenticate).FirstOrDefault();
        error.ShouldBe("DPoP error=\"invalid_request\"");
    }

    [Fact]
    public async Task excessively_large_proof_fails()
    {
        var maxLength = 50;
        ApiOptions = opt => opt.ProofTokenMaxLength = maxLength;
        await Initialize();
        var token = await LoginAndGetToken();
        Api.HttpClient.SetToken(OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP, token.AccessToken);

        // Create proof token with excessively long nonce
        var proof = await CreateProofToken(token, Jwk, HttpMethod.Get, new Uri("http://localhost/"),
            nonce: DPoPNonce.Parse(new string('x', maxLength + 1))); // <--- Most important part of the test
        Api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, proof);

        var result = await Api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task missing_nonce_generates_server_issued_nonce()
    {
        ApiOptions = opt => opt.ValidationMode = ExpirationValidationMode.Nonce;
        await Initialize();
        var token = await LoginAndGetToken();
        Api.HttpClient.SetToken("DPoP", token.AccessToken);

        // Create proof token WITHOUT a nonce for api call
        var proof = await CreateProofToken(token, Jwk, HttpMethod.Get, new Uri("http://localhost/"));
        Api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, proof);

        // Expect failure with a server-issued nonce
        var result = await Api.HttpClient.GetAsync("/");

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
        var proofWithNonce = await CreateProofToken(token, Jwk, HttpMethod.Get, new Uri("http://localhost/"),
            nonce: DPoPNonce.Parse(serverNonce));
        Api.HttpClient.DefaultRequestHeaders.Remove(OidcConstants.HttpHeaders.DPoP);
        Api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, proofWithNonce);
        var secondResult = await Api.HttpClient.GetAsync("/");
        secondResult.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task custom_nonce_validator_generates_and_validates_custom_nonce()
    {
        var customValidator = new CustomDPoPNonceValidator();

        ApiOptions = opt =>
        {
            opt.ValidationMode = ExpirationValidationMode.Nonce;
        };
        Api.OnConfigureServices += services =>
        {
            services.AddSingleton<IDPoPNonceValidator>(customValidator);
        };
        await Initialize();

        var token = await LoginAndGetToken();
        Api.HttpClient.SetToken("DPoP", token.AccessToken);

        // Create proof token WITHOUT a nonce for api call
        var proof = await CreateProofToken(token, Jwk, HttpMethod.Get, new Uri("http://localhost/"));
        Api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, proof);

        // Expect failure with a custom server-issued nonce
        var result = await Api.HttpClient.GetAsync("/");
        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Verify that a custom nonce was issued in the response
        result.Headers.TryGetValues("DPoP-Nonce", out var nonceValues).ShouldBeTrue();
        var serverNonce = nonceValues?.FirstOrDefault();
        serverNonce.ShouldNotBeNull();
        serverNonce.ShouldStartWith("custom-nonce-");

        // Make a new request with the custom server issued nonce
        var proofWithNonce = await CreateProofToken(token, Jwk, HttpMethod.Get, new Uri("http://localhost/"),
            nonce: DPoPNonce.Parse(serverNonce));
        Api.HttpClient.DefaultRequestHeaders.Remove(OidcConstants.HttpHeaders.DPoP);
        Api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, proofWithNonce);
        var secondResult = await Api.HttpClient.GetAsync("/");
        secondResult.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task replay_cache_can_be_customized_by_replacing_keyed_hybrid_cache()
    {
        var cache = new TestHybridCache();
        Api.OnConfigureServices += services =>
        {
            services.AddKeyedSingleton<HybridCache>(ServiceProviderKeys.ProofTokenReplayHybridCache, cache);
        };
        await Initialize();

        var token = await LoginAndGetToken();
        Api.HttpClient.SetToken("DPoP", token.AccessToken);

        var proof = await CreateProofToken(token, Jwk, HttpMethod.Get, new Uri("http://localhost/"));
        Api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, proof);

        var result = await Api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify that the custom cache was used for replay detection
        cache.SetAsyncCalls.Count.ShouldBeGreaterThan(0);
        cache.GetOrCreateAsyncCalls.Count.ShouldBeGreaterThan(0);
    }

    // Initialize all the hosts. We separate this from creation of the hosts so that tests can customize their behavior
    private async Task Initialize()
    {
        await IdentityServer.Initialize();
        await Api.Initialize();
        await App.Initialize();
    }

    /// <summary>
    /// Logs in and retrieves a user token from the app.
    /// </summary>
    private async Task<UserToken> LoginAndGetToken()
    {
        await App.LoginAsync("sub");
        var response = await App.BrowserClient.GetAsync(App.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserToken>();
        token.ShouldNotBeNull();
        token.AccessToken.ToString().ShouldNotBeNull();
        token.DPoPJsonWebKey.ShouldNotBeNull();
        return token;
    }

    /// <summary>
    /// Creates a DPoP proof token for an API request.
    /// </summary>
    private async Task<string> CreateProofToken(
        UserToken token,
        DPoPProofKey jwk,
        HttpMethod method,
        Uri url,
        DPoPNonce? nonce = null)
    {
        var dpopService = App.Server.Services.GetRequiredService<IDPoPProofService>();

        var proofRequest = new DPoPProofRequest
        {
            AccessToken = token.AccessToken,
            DPoPProofKey = jwk,
            Method = method,
            Url = url,
            DPoPNonce = nonce
        };

        var proof = await dpopService.CreateProofTokenAsync(proofRequest);
        proof.ShouldNotBeNull();
        return proof.Value;
    }

    private IdentityServerHost CreateIdentityServer(Action<IdentityServerHost>? setup = null)
    {
        var host = new IdentityServerHost(_testOutputHelper);
        setup?.Invoke(host);

        host.Clients.Add(new Client
        {
            ClientId = "client1",
            ClientSecrets = [new Secret("secret".ToSha256())],
            RequireDPoP = true,
            AllowedScopes = ["openid", "profile", "scope1"],
            AllowedGrantTypes = GrantTypes.Code,
            RedirectUris = ["https://app/signin-oidc"],
            PostLogoutRedirectUris = ["https://app/signout-callback-oidc"]
        });
        return host;
    }

    private ApiHost CreateApi()
    {
        var baseAddress = "https://api";
        var api = new ApiHost(IdentityServer, _testOutputHelper, baseAddress);
        api.OnConfigureServices += services =>
        {
            services.ConfigureDPoPTokensForScheme(ApiHost.AuthenticationScheme,
                opt => ApiOptions?.Invoke(opt));

            services.AddKeyedHybridCache(ServiceProviderKeys.ProofTokenReplayHybridCache);

        };
        api.OnConfigure += app =>
            app.MapGet("/", () => "default route")
                .RequireAuthorization();

        return api;
    }

    private AppHost CreateApp() => new(
        IdentityServer,
        Api,
        "client1",
        _testOutputHelper,
        configureUserTokenManagementOptions: opt => opt.DPoPJsonWebKey = Jwk);

    private DPoPProofKey CreateJwk()
    {
        var rsaKey = new RsaSecurityKey(RSA.Create(2048));
        var jwkKey = JsonWebKeyConverter.ConvertFromSecurityKey(rsaKey);
        jwkKey.Alg = "RS256";
        var jwk = JsonSerializer.Serialize(jwkKey);
        return DPoPProofKey.Parse(jwk);
    }
}
