// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Hosting.LocalApiAuthentication;
using Duende.IdentityServer.Models;
using IntegrationTests.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IntegrationTests.Hosting.LocalApiAuthentication;

public class LocalApiAuthenticationTests
{
    private const string Category = "Local API Integration";

    private IdentityServerPipeline _pipeline = new IdentityServerPipeline();

    private static string _jwk;
    private Client _client;

    public LocalApiTokenMode Mode { get; set; }

    public bool ApiWasCalled { get; set; }
    public ClaimsPrincipal ApiPrincipal { get; set; }

    static LocalApiAuthenticationTests()
    {
        _jwk = GenerateJwk();
    }

    private static string GenerateJwk()
    {
        var rsaKey = new RsaSecurityKey(RSA.Create(2048));
        var jsonWebKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaKey);
        jsonWebKey.Alg = "PS256";
        return JsonSerializer.Serialize(jsonWebKey);
    }

    public LocalApiAuthenticationTests()
    {
        _pipeline.Clients.AddRange(new Client[] {
            _client = new Client
            {
                ClientId = "client",
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets = { new Secret("secret".Sha256()) },
                AllowedScopes = new List<string> { "api1", "api2" },
            },
            new Client
            {
                ClientId = "introspection",
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets = { new Secret("secret".Sha256()) },
                AllowedScopes = new List<string> { "api1", "api2" },
                AccessTokenType = AccessTokenType.Reference
            }
        });

        _pipeline.ApiResources.AddRange(new ApiResource[] {
            new ApiResource
            {
                Name = "api",
                Scopes = { "api1", "api2" }
            }
        });
        _pipeline.ApiScopes.AddRange(new[] {
            new ApiScope
            {
                Name = "api1"
            },
            new ApiScope
            {
                Name = "api2"
            }
        });

        _pipeline.OnPostConfigureServices += services =>
        {
            services.AddAuthentication()
                .AddLocalApi("local", options =>
                {
                    options.TokenMode = Mode;
                });
        };

        _pipeline.OnPreConfigureServices += services =>
        {
            services.AddRouting();
            services.AddAuthorization(options =>
            {
                options.AddPolicy("token", policy =>
                {
                    policy.AddAuthenticationSchemes("local");
                    policy.RequireAuthenticatedUser();
                });
            });
        };

        _pipeline.OnPreConfigure += app =>
        {
            app.UseRouting();
        };

        _pipeline.OnPostConfigure += app =>
        {
            app.UseAuthorization();

            app.UseEndpoints(eps =>
            {
                eps.MapGet("/api", ctx =>
                {
                    ApiWasCalled = true;
                    ApiPrincipal = ctx.User;
                    return Task.CompletedTask;
                }).RequireAuthorization("token");
            });
        };

        Init();
    }

    private void Init(LocalApiTokenMode mode = LocalApiTokenMode.DPoPAndBearer)
    {
        Mode = mode;
        _pipeline.Initialize();
    }

    private async Task<string> GetAccessTokenAsync(bool dpop = false, bool reference = false)
    {
        var req = new ClientCredentialsTokenRequest
        {
            Address = "https://server/connect/token",
            ClientId = reference ? "introspection" : "client",
            ClientSecret = "secret",
            Scope = "api1",
        };

        if (dpop)
        {
            req.DPoPProofToken = CreateProofToken("POST", "https://server/connect/token");
        }

        var result = await _pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(req);
        result.IsError.ShouldBeFalse();

        if (dpop) result.TokenType.ShouldBe("DPoP");
        else result.TokenType.ShouldBe("Bearer");

        return result.AccessToken;
    }

    private string CreateProofToken(string method, string url, string accessToken = null, string nonce = null, string jwkString = null)
    {
        var jsonWebKey = new Microsoft.IdentityModel.Tokens.JsonWebKey(jwkString ?? _jwk);

        // jwk: representing the public key chosen by the client, in JSON Web Key (JWK) [RFC7517] format,
        // as defined in Section 4.1.3 of [RFC7515]. MUST NOT contain a private key.
        object jwk;
        if (string.Equals(jsonWebKey.Kty, JsonWebAlgorithmsKeyTypes.EllipticCurve))
        {
            jwk = new
            {
                kty = jsonWebKey.Kty,
                x = jsonWebKey.X,
                y = jsonWebKey.Y,
                crv = jsonWebKey.Crv
            };
        }
        else if (string.Equals(jsonWebKey.Kty, JsonWebAlgorithmsKeyTypes.RSA))
        {
            jwk = new Dictionary<string, object>
            {
                { "kty", jsonWebKey.Kty },
                { "e", jsonWebKey.E },
                { "n", jsonWebKey.N }
            };
        }
        else
        {
            throw new InvalidOperationException("invalid key type.");
        }

        var header = new Dictionary<string, object>()
        {
            //{ "alg", "RS265" }, // JsonWebTokenHandler requires adding this itself
            { "typ", JwtClaimTypes.JwtTypes.DPoPProofToken },
            { JwtClaimTypes.JsonWebKey, jwk },
        };

        var payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.JwtId, CryptoRandom.CreateUniqueId() },
            { JwtClaimTypes.DPoPHttpMethod, method },
            { JwtClaimTypes.DPoPHttpUrl, url },
            { JwtClaimTypes.IssuedAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
        };

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            // ath: hash of the access token. The value MUST be the result of a base64url encoding 
            // the SHA-256 hash of the ASCII encoding of the associated access token's value.
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(accessToken));
            var ath = Base64Url.Encode(hash);

            payload.Add(JwtClaimTypes.DPoPAccessTokenHash, ath);
        }

        if (!string.IsNullOrEmpty(nonce))
        {
            payload.Add(JwtClaimTypes.Nonce, nonce);
        }

        var handler = new JsonWebTokenHandler() { SetDefaultTimesOnTokenCreation = false };
        var key = new SigningCredentials(jsonWebKey, jsonWebKey.Alg);
        var proofToken = handler.CreateToken(JsonSerializer.Serialize(payload), key, header);
        return proofToken;
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task bearer_jwt_token_should_validate()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "https://server/api");
        var at = await GetAccessTokenAsync();
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", at);

        var response = await _pipeline.BackChannelClient.SendAsync(req);

        response.IsSuccessStatusCode.ShouldBeTrue();
        ApiWasCalled.ShouldBeTrue();
        ApiPrincipal.Identity.IsAuthenticated.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task bearer_ref_token_should_validate()
    {
        _client.AccessTokenType = AccessTokenType.Reference;

        var req = new HttpRequestMessage(HttpMethod.Get, "https://server/api");
        var at = await GetAccessTokenAsync();
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", at);

        var response = await _pipeline.BackChannelClient.SendAsync(req);

        response.IsSuccessStatusCode.ShouldBeTrue();
        ApiWasCalled.ShouldBeTrue();
        ApiPrincipal.Identity.IsAuthenticated.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_token_should_validate()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "https://server/api");
        var at = await GetAccessTokenAsync(true);
        req.Headers.Authorization = new AuthenticationHeaderValue("DPoP", at);
        req.Headers.Add("DPoP", CreateProofToken("GET", "https://server/api", at));

        var response = await _pipeline.BackChannelClient.SendAsync(req);

        response.IsSuccessStatusCode.ShouldBeTrue();
        ApiWasCalled.ShouldBeTrue();
        ApiPrincipal.Identity.IsAuthenticated.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_token_should_not_validate_if_cnf_from_jwt_access_token_does_not_match_proof_token()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "https://server/api");
        var at = await GetAccessTokenAsync(true);
        req.Headers.Authorization = new AuthenticationHeaderValue("DPoP", at);

        // Use a new key to make the proof token that we present when we make the API request.
        // This doesn't prove that we have possession of the key that the access token is bound to,
        // so it should fail.
        var newKey = GenerateJwk();
        var newJwk = new Microsoft.IdentityModel.Tokens.JsonWebKey(newKey);
        var newJkt = Base64Url.Encode(newJwk.ComputeJwkThumbprint());
        var proofToken = CreateProofToken("GET", "https://server/api", at, jwkString: newKey);
        req.Headers.Add("DPoP", proofToken);

        // Double check that the thumbprint in the access token's cnf claim doesn't match
        // the thumbprint of the new key we just used.
        var handler = new JwtSecurityTokenHandler();
        var parsedAt = handler.ReadJwtToken(at);
        var parsedProof = handler.ReadJwtToken(proofToken);
        var cnf = parsedAt.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Confirmation);
        var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cnf.Value);
        if (json.TryGetValue(JwtClaimTypes.ConfirmationMethods.JwkThumbprint, out var jktJson))
        {
            var accessTokenJkt = jktJson.ToString();
            accessTokenJkt.ShouldNotBe(newJkt);
        }

        var response = await _pipeline.BackChannelClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_token_should_not_validate_if_cnf_from_introspection_does_not_match_proof_token()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "https://server/api");
        var at = await GetAccessTokenAsync(dpop: true, reference: true);
        req.Headers.Authorization = new AuthenticationHeaderValue("DPoP", at);

        // Use a new key to make the proof token that we present when we make the API request.
        // This doesn't prove that we have possession of the key that the access token is bound to,
        // so it should fail.
        var newKey = GenerateJwk();
        var newJwk = new Microsoft.IdentityModel.Tokens.JsonWebKey(newKey);
        var newJkt = Base64Url.Encode(newJwk.ComputeJwkThumbprint());
        var proofToken = CreateProofToken("GET", "https://server/api", at, jwkString: newKey);
        req.Headers.Add("DPoP", proofToken);

        var introspectionRequest = new TokenIntrospectionRequest
        {
            Address = "https://server/connect/introspect",
            ClientId = "introspection",
            ClientSecret = "secret",
            Token = at
        };
        var introspectionResponse = await _pipeline.BackChannelClient.IntrospectTokenAsync(introspectionRequest);
        introspectionResponse.IsError.ShouldBeFalse();

        var cnf = introspectionResponse.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Confirmation);
        var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cnf.Value);
        if (json.TryGetValue(JwtClaimTypes.ConfirmationMethods.JwkThumbprint, out var jktJson))
        {
            var accessTokenJkt = jktJson.ToString();
            accessTokenJkt.ShouldNotBe(newJkt);
        }

        var response = await _pipeline.BackChannelClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_nonce_required_should_require_nonce()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "https://server/api");
        var at = await GetAccessTokenAsync(true);
        req.Headers.Authorization = new AuthenticationHeaderValue("DPoP", at);
        req.Headers.Add("DPoP", CreateProofToken("GET", "https://server/api", at));

        _client.DPoPValidationMode = DPoPTokenExpirationValidationMode.Nonce;
        var response = await _pipeline.BackChannelClient.SendAsync(req);

        response.IsSuccessStatusCode.ShouldBeFalse();
        response.Headers.Contains("DPoP-Nonce").ShouldBeTrue();
    }
    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_nonce_should_validate()
    {
        var at = await GetAccessTokenAsync(true);

        var req = new HttpRequestMessage(HttpMethod.Get, "https://server/api");
        req.Headers.Authorization = new AuthenticationHeaderValue("DPoP", at);
        req.Headers.Add("DPoP", CreateProofToken("GET", "https://server/api", at));

        _client.DPoPValidationMode = DPoPTokenExpirationValidationMode.Nonce;
        var response = await _pipeline.BackChannelClient.SendAsync(req);
        var nonce = response.Headers.GetValues("DPoP-Nonce").FirstOrDefault();

        var req2 = new HttpRequestMessage(HttpMethod.Get, "https://server/api");
        req2.Headers.Authorization = new AuthenticationHeaderValue("DPoP", at);
        req2.Headers.Add("DPoP", CreateProofToken("GET", "https://server/api", at, nonce));

        var response2 = await _pipeline.BackChannelClient.SendAsync(req2);
        response2.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task bearer_only_dpop_token_should_fail()
    {
        Init(LocalApiTokenMode.BearerOnly);
        var req = new HttpRequestMessage(HttpMethod.Get, "https://server/api");
        var at = await GetAccessTokenAsync(true);
        req.Headers.Authorization = new AuthenticationHeaderValue("DPoP", at);
        req.Headers.Add("DPoP", CreateProofToken("GET", "https://server/api", at));

        var response = await _pipeline.BackChannelClient.SendAsync(req);

        response.IsSuccessStatusCode.ShouldBeFalse();
        response.Headers.WwwAuthenticate.Select(x => x.Scheme).ShouldBe(["Bearer"]);

    }

    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_only_bearer_should_fail()
    {
        Init(LocalApiTokenMode.DPoPOnly);
        var req = new HttpRequestMessage(HttpMethod.Get, "https://server/api");
        var at = await GetAccessTokenAsync();
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", at);

        var response = await _pipeline.BackChannelClient.SendAsync(req);

        response.IsSuccessStatusCode.ShouldBeFalse();
        response.Headers.WwwAuthenticate.Select(x => x.Scheme).ShouldBe(["DPoP"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_authz_should_fail()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "https://server/api");

        var response = await _pipeline.BackChannelClient.SendAsync(req);

        response.IsSuccessStatusCode.ShouldBeFalse();
        response.Headers.WwwAuthenticate.Select(x => x.Scheme).ShouldBe(["Bearer", "DPoP"]);
    }
    [Fact]
    [Trait("Category", Category)]
    public async Task missing_token_should_fail()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "https://server/api");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "");

        var response = await _pipeline.BackChannelClient.SendAsync(req);

        response.IsSuccessStatusCode.ShouldBeFalse();
    }
    [Fact]
    [Trait("Category", Category)]
    public async Task malformed_token_should_fail()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "https://server/api");
        var at = await GetAccessTokenAsync();
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", at.Substring(at.Length / 2));

        var response = await _pipeline.BackChannelClient.SendAsync(req);

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_token_for_disabled_client_should_fail()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "https://server/api");
        var at = await GetAccessTokenAsync(true);
        req.Headers.Authorization = new AuthenticationHeaderValue("DPoP", at);
        req.Headers.Add("DPoP", CreateProofToken("GET", "https://server/api", at));

        _client.Enabled = false;

        var response = await _pipeline.BackChannelClient.SendAsync(req);

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_validation_failure_should_fail()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "https://server/api");
        var at = await GetAccessTokenAsync(true);
        req.Headers.Authorization = new AuthenticationHeaderValue("DPoP", at);

        var response = await _pipeline.BackChannelClient.SendAsync(req);

        response.IsSuccessStatusCode.ShouldBeFalse();
    }
    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_token_using_bearer_scheme_should_fail()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "https://server/api");
        var at = await GetAccessTokenAsync(true);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", at);

        var response = await _pipeline.BackChannelClient.SendAsync(req);

        response.IsSuccessStatusCode.ShouldBeFalse();
    }
}
