// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

public class DPoPIntegrationTests
{
    public DPoPIntegrationTests()
    {
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
        ApiOptions = opt => opt.ProofTokenExpirationMode = DPoPProofExpirationMode.Nonce;
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
        _ = serverNonce.ShouldNotBeNull();
        serverNonce.ShouldNotBeEmpty();

        // Verify the WWW-Authenticate header contains the use_dpop_nonce error
        var wwwAuthValues = result.Headers.GetValues(HeaderNames.WWWAuthenticate).FirstOrDefault();
        _ = wwwAuthValues.ShouldNotBeNull();
        wwwAuthValues.ShouldContain("use_dpop_nonce");

        // Make a new request with the server issued nonce
        var proofWithNonce = await CreateProofToken(token, Jwk, HttpMethod.Get, new Uri("http://localhost/"),
            nonce: DPoPNonce.Parse(serverNonce));
        _ = Api.HttpClient.DefaultRequestHeaders.Remove(OidcConstants.HttpHeaders.DPoP);
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
            opt.ProofTokenExpirationMode = DPoPProofExpirationMode.Nonce;
        };
        Api.OnConfigureServices += services =>
        {
            _ = services.AddSingleton<IDPoPNonceValidator>(customValidator);
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
        _ = serverNonce.ShouldNotBeNull();
        serverNonce.ShouldStartWith("custom-nonce-");

        // Make a new request with the custom server issued nonce
        var proofWithNonce = await CreateProofToken(token, Jwk, HttpMethod.Get, new Uri("http://localhost/"),
            nonce: DPoPNonce.Parse(serverNonce));
        _ = Api.HttpClient.DefaultRequestHeaders.Remove(OidcConstants.HttpHeaders.DPoP);
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
            _ = services.AddKeyedSingleton<HybridCache>(ServiceProviderKeys.ProofTokenReplayHybridCache, cache);
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

    [Fact]
    public async Task proof_signed_by_x5c_key_instead_of_bound_jwk_fails()
    {
        await Initialize();
        var token = await LoginAndGetToken();
        using var attackerKey = RSA.Create(2048);
        var algorithmTheClientHasNotUsed = SecurityAlgorithms.RsaSsaPssSha256;
        var forgedProof = CreateProofWithClientJwkAndAttackerCertificate(token, attackerKey, algorithmTheClientHasNotUsed);
        Api.HttpClient.SetToken(OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP, token.AccessToken);
        Api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, forgedProof);

        var forgedResponse = await Api.HttpClient.GetAsync("/");

        forgedResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task poisoning_the_shared_jwt_verifier_cache_does_not_affect_a_valid_proof()
    {
        await Initialize();
        var clientToken = await LoginAndGetToken();
        var clientDPoPKey = Jwk.ToJsonWebKey();
        var algorithmTheClientHasNotUsed = SecurityAlgorithms.RsaSsaPssSha256;

        using var attackerKey = RSA.Create(2048);
        using var attackerCertificate = new CertificateRequest("CN=attacker", attackerKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var clientKeyWithAttackerCertificate = new Dictionary<string, object>
        {
            { JsonWebKeyParameterNames.Kty, clientDPoPKey.Kty },
            { JsonWebKeyParameterNames.E, clientDPoPKey.E },
            { JsonWebKeyParameterNames.N, clientDPoPKey.N },
            { JsonWebKeyParameterNames.X5c, new[] { Convert.ToBase64String(attackerCertificate.RawData) } },
        };
        var cachePoisoningTokenHandler = new JsonWebTokenHandler();
        var tokenSignedByAttacker = cachePoisoningTokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(attackerKey), algorithmTheClientHasNotUsed)
        });
        var cachePoisoningAttempt = await cachePoisoningTokenHandler.ValidateTokenAsync(tokenSignedByAttacker, new TokenValidationParameters
        {
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.JsonWebKey(JsonSerializer.Serialize(clientKeyWithAttackerCertificate)),
            ValidateIssuer = false,
            ValidateAudience = false
        });
        cachePoisoningAttempt.IsValid.ShouldBeTrue();

        var clientPublicKey = new Dictionary<string, object>
        {
            { JsonWebKeyParameterNames.Kty, clientDPoPKey.Kty },
            { JsonWebKeyParameterNames.E, clientDPoPKey.E },
            { JsonWebKeyParameterNames.N, clientDPoPKey.N },
        };
        var validClientProof = CreateProof(clientToken, clientDPoPKey, algorithmTheClientHasNotUsed, clientPublicKey);
        Api.HttpClient.SetToken(OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP, clientToken.AccessToken);
        Api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, validClientProof);
        var dpopProtectedEndpoint = "/";

        var responseToValidClientProof = await Api.HttpClient.GetAsync(dpopProtectedEndpoint);

        responseToValidClientProof.StatusCode.ShouldBe(HttpStatusCode.OK);
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
        _ = await App.LoginAsync("sub");
        var response = await App.BrowserClient.GetAsync(App.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserToken>();
        _ = token.ShouldNotBeNull();
        _ = token.AccessToken.ToString().ShouldNotBeNull();
        _ = token.DPoPJsonWebKey.ShouldNotBeNull();
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
        _ = proof.ShouldNotBeNull();
        return proof.Value;
    }

    private string CreateProofWithClientJwkAndAttackerCertificate(UserToken token, RSA attackerKey, string algorithm)
    {
        var clientJwk = Jwk.ToJsonWebKey();
        using var attackerCertificate = new CertificateRequest("CN=attacker", attackerKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var proofJwk = new Dictionary<string, object>
        {
            { JsonWebKeyParameterNames.Kty, clientJwk.Kty },
            { JsonWebKeyParameterNames.E, clientJwk.E },
            { JsonWebKeyParameterNames.N, clientJwk.N },
            { JsonWebKeyParameterNames.X5c, new[] { Convert.ToBase64String(attackerCertificate.RawData) } },
        };
        return CreateProof(token, new RsaSecurityKey(attackerKey), algorithm, proofJwk);
    }

    private IdentityServerHost CreateIdentityServer(Action<IdentityServerHost>? setup = null)
    {
        var host = new IdentityServerHost();
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
        var api = new ApiHost(IdentityServer, baseAddress);
        api.OnConfigureServices += services =>
        {
            _ = services.ConfigureDPoPTokensForScheme(ApiHost.AuthenticationScheme,
                opt => ApiOptions?.Invoke(opt));

            _ = services.AddKeyedHybridCache(ServiceProviderKeys.ProofTokenReplayHybridCache);

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
        configureUserTokenManagementOptions: opt => opt.DPoPJsonWebKey = Jwk);

    private DPoPProofKey CreateJwk()
    {
        var rsaKey = new RsaSecurityKey(RSA.Create(2048));
        var jwkKey = JsonWebKeyConverter.ConvertFromSecurityKey(rsaKey);
        jwkKey.Alg = "RS256";
        var jwk = JsonSerializer.Serialize(jwkKey);
        return DPoPProofKey.Parse(jwk);
    }

    private static string CreateProof(UserToken token, SecurityKey signingKey, string algorithm, Dictionary<string, object> proofJwk)
    {
        var accessTokenHash = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(token.AccessToken.ToString())));
        var descriptor = new SecurityTokenDescriptor
        {
            TokenType = JwtClaimTypes.JwtTypes.DPoPProofToken,
            IssuedAt = DateTime.UtcNow,
            AdditionalHeaderClaims = new Dictionary<string, object> { { JwtClaimTypes.JsonWebKey, proofJwk } },
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString()),
                new Claim(JwtClaimTypes.DPoPHttpMethod, HttpMethod.Get.Method),
                new Claim(JwtClaimTypes.DPoPHttpUrl, "http://localhost/"),
                new Claim(JwtClaimTypes.DPoPAccessTokenHash, accessTokenHash),
            ]),
            SigningCredentials = new SigningCredentials(signingKey, algorithm)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
