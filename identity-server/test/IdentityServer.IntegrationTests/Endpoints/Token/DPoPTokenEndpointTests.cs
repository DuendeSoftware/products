// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Test;
using Duende.IdentityServer.Validation;
using IntegrationTests.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IntegrationTests.Endpoints.Token;

public class DPoPTokenEndpointTests
{
    private const string Category = "DPoP Token endpoint";

    private IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();

    private Client _dpopConfidentialClient;

    private Client _dpopPublicClient;

    private DateTime _now = new DateTime(2020, 3, 10, 9, 0, 0, DateTimeKind.Utc);
    public DateTime UtcNow
    {
        get
        {
            if (_now > DateTime.MinValue) return _now;
            return DateTime.UtcNow;
        }
    }

    private Dictionary<string, object> _header;
    private Dictionary<string, object> _payload;
    private string _privateJWK = "{\"Crv\":null,\"D\":\"QeBWodq0hSYjfAxxo0VZleXLqwwZZeNWvvFfES4WyItao_-OJv1wKA7zfkZxbWkpK5iRbKrl2AMJ52AtUo5JJ6QZ7IjAQlgM0lBg3ltjb1aA0gBsK5XbiXcsV8DiAnRuy6-XgjAKPR8Lo-wZl_fdPbVoAmpSdmfn_6QXXPBai5i7FiyDbQa16pI6DL-5SCj7F78QDTRiJOqn5ElNvtoJEfJBm13giRdqeriFi3pCWo7H3QBgTEWtDNk509z4w4t64B2HTXnM0xj9zLnS42l7YplJC7MRibD4nVBMtzfwtGRKLj8beuDgtW9pDlQqf7RVWX5pHQgiHAZmUi85TEbYdQ\",\"DP\":\"h2F54OMaC9qq1yqR2b55QNNaChyGtvmTHSdqZJ8lJFqvUorlz-Uocj2BTowWQnaMd8zRKMdKlSeUuSv4Z6WmjSxSsNbonI6_II5XlZLWYqFdmqDS-xCmJY32voT5Wn7OwB9xj1msDqrFPg-PqSBOh5OppjCqXqDFcNvSkQSajXc\",\"DQ\":\"VABdS20Nxkmq6JWLQj7OjRxVJuYsHrfmWJmDA7_SYtlXaPUcg-GiHGQtzdDWEeEi0dlJjv9I3FdjKGC7CGwqtVygW38DzVYJsV2EmRNJc1-j-1dRs_pK9GWR4NYm0mVz_IhS8etIf9cfRJk90xU3AL3_J6p5WNF7I5ctkLpnt8M\",\"E\":\"AQAB\",\"K\":null,\"KeyOps\":[],\"Kty\":\"RSA\",\"N\":\"yWWAOSV3Z_BW9rJEFvbZyeU-q2mJWC0l8WiHNqwVVf7qXYgm9hJC0j1aPHku_Wpl38DpK3Xu3LjWOFG9OrCqga5Pzce3DDJKI903GNqz5wphJFqweoBFKOjj1wegymvySsLoPqqDNVYTKp4nVnECZS4axZJoNt2l1S1bC8JryaNze2stjW60QT-mIAGq9konKKN3URQ12dr478m0Oh-4WWOiY4HrXoSOklFmzK-aQx1JV_SZ04eIGfSw1pZZyqTaB1BwBotiy-QA03IRxwIXQ7BSx5EaxC5uMCMbzmbvJqjt-q8Y1wyl-UQjRucgp7hkfHSE1QT3zEex2Q3NFux7SQ\",\"Oth\":null,\"P\":\"_T7MTkeOh5QyqlYCtLQ2RWf2dAJ9i3wrCx4nEDm1c1biijhtVTL7uJTLxwQIM9O2PvOi5Dq-UiGy6rhHZqf5akWTeHtaNyI-2XslQfaS3ctRgmGtRQL_VihK-R9AQtDx4eWL4h-bDJxPaxby_cVo_j2MX5AeoC1kNmcCdDf_X0M\",\"Q\":\"y5ZSThaGLjaPj8Mk2nuD8TiC-sb4aAZVh9K-W4kwaWKfDNoPcNb_dephBNMnOp9M1br6rDbyG7P-Sy_LOOsKg3Q0wHqv4hnzGaOQFeMJH4HkXYdENC7B5JG9PefbC6zwcgZWiBnsxgKpScNWuzGF8x2CC-MdsQ1bkQeTPbJklIM\",\"QI\":\"i716Vt9II_Rt6qnjsEhfE4bej52QFG9a1hSnx5PDNvRrNqR_RpTA0lO9qeXSZYGHTW_b6ZXdh_0EUwRDEDHmaxjkIcTADq6JLuDltOhZuhLUSc5NCKLAVCZlPcaSzv8-bZm57mVcIpx0KyFHxvk50___Jgx1qyzwLX03mPGUbDQ\",\"Use\":null,\"X\":null,\"X5c\":[],\"X5t\":null,\"X5tS256\":null,\"X5u\":null,\"Y\":null,\"KeySize\":2048,\"HasPrivateKey\":true,\"CryptoProviderFactory\":{\"CryptoProviderCache\":{},\"CustomCryptoProvider\":null,\"CacheSignatureProviders\":true,\"SignatureProviderObjectPoolCacheSize\":80}}";
    private string _publicJWK = "{\"kty\":\"RSA\",\"use\":\"sig\",\"x5t\":null,\"e\":\"AQAB\",\"n\":\"yWWAOSV3Z_BW9rJEFvbZyeU-q2mJWC0l8WiHNqwVVf7qXYgm9hJC0j1aPHku_Wpl38DpK3Xu3LjWOFG9OrCqga5Pzce3DDJKI903GNqz5wphJFqweoBFKOjj1wegymvySsLoPqqDNVYTKp4nVnECZS4axZJoNt2l1S1bC8JryaNze2stjW60QT-mIAGq9konKKN3URQ12dr478m0Oh-4WWOiY4HrXoSOklFmzK-aQx1JV_SZ04eIGfSw1pZZyqTaB1BwBotiy-QA03IRxwIXQ7BSx5EaxC5uMCMbzmbvJqjt-q8Y1wyl-UQjRucgp7hkfHSE1QT3zEex2Q3NFux7SQ\",\"x5c\":null,\"x\":null,\"y\":null,\"crv\":null}";
    private string _JKT = "JGSVlE73oKtQQI1dypYg8_JNat0xJjsQNyOI5oxaZf4";

    public DPoPTokenEndpointTests()
    {
        _mockPipeline.OnPostConfigureServices += services =>
        {
        };

        _mockPipeline.Clients.AddRange(new Client[] {
            _dpopConfidentialClient = new Client
            {
                ClientId = "client1",
                AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
                ClientSecrets =
                {
                    new Secret("secret".Sha256()),
                },
                RedirectUris = { "https://client1/callback" },
                RequirePkce = false,
                AllowOfflineAccess = true,
                RefreshTokenUsage = TokenUsage.ReUse,
                AllowedScopes = new List<string> { "openid", "profile", "scope1" },
            },
            _dpopPublicClient = new Client
            {
                ClientId = "client2",
                AllowedGrantTypes = GrantTypes.Code,
                RequireClientSecret = false,
                RequirePkce = false,
                RedirectUris = { "https://client2/callback" },
                AllowOfflineAccess = true,
                RefreshTokenUsage = TokenUsage.ReUse,
                AllowedScopes = new List<string> { "openid", "profile", "scope2" },
            }
        });

        _mockPipeline.Users.Add(new TestUser
        {
            SubjectId = "bob",
            Username = "bob",
            Password = "bob",
            Claims = new Claim[]
            {
                new Claim("name", "Bob Loblaw"),
                new Claim("email", "bob@loblaw.com"),
                new Claim("role", "Attorney")
            }
        });

        _mockPipeline.IdentityScopes.AddRange(new IdentityResource[] {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email()
        });
        _mockPipeline.ApiResources.AddRange(new[]
        {
            new ApiResource("api1")
            {
                Scopes = { "scope1" },
                ApiSecrets =
                {
                    new Secret("secret".Sha256())
                }
            },
            new ApiResource("api2")
            {
                Scopes = { "scope2" },
                ApiSecrets =
                {
                    new Secret("secret".Sha256())
                }
            }
        });
        _mockPipeline.ApiScopes.AddRange(new ApiScope[] {
            new ApiScope
            {
                Name = "scope1"
            },
            new ApiScope
            {
                Name = "scope2"
            }
        });

        _mockPipeline.Initialize();

        _payload = new Dictionary<string, object>
        {
            //{ "jti", CryptoRandom.CreateUniqueId() }, // added dynamically below in CreateDPoPProofToken
            //{ "iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }, // added dynamically below in CreateDPoPProofToken
            { "htm", "POST" },
            { "htu", IdentityServerPipeline.TokenEndpoint },
        };

        CreateHeaderValuesFromPublicKey();
    }

    private void CreateNewRSAKey()
    {
        var key = CryptoHelper.CreateRsaSecurityKey();
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        _JKT = Base64UrlEncoder.Encode(key.ComputeJwkThumbprint());
        _privateJWK = JsonSerializer.Serialize(jwk);
        _publicJWK = JsonSerializer.Serialize(new
        {
            kty = jwk.Kty,
            e = jwk.E,
            n = jwk.N,
        });

        CreateHeaderValuesFromPublicKey();
    }

    private void CreateNewECKey()
    {
        var key = CryptoHelper.CreateECDsaSecurityKey();
        var jwk = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(key);
        _JKT = Base64UrlEncoder.Encode(key.ComputeJwkThumbprint());
        _privateJWK = JsonSerializer.Serialize(jwk);
        _publicJWK = JsonSerializer.Serialize(new
        {
            kty = jwk.Kty,
            x = jwk.X,
            y = jwk.Y,
            crv = jwk.Crv
        });

        CreateHeaderValuesFromPublicKey();
    }

    private void CreateHeaderValuesFromPublicKey(string publicJwk = null)
    {
        var jwk = JsonSerializer.Deserialize<JsonElement>(publicJwk ?? _publicJWK);
        var jwkValues = new Dictionary<string, object>();
        foreach (var item in jwk.EnumerateObject())
        {
            if (item.Value.ValueKind == JsonValueKind.String)
            {
                var val = item.Value.GetString();
                if (!string.IsNullOrEmpty(val))
                {
                    jwkValues.Add(item.Name, val);
                }
            }
            if (item.Value.ValueKind == JsonValueKind.False)
            {
                jwkValues.Add(item.Name, false);
            }
            if (item.Value.ValueKind == JsonValueKind.True)
            {
                jwkValues.Add(item.Name, true);
            }
            if (item.Value.ValueKind == JsonValueKind.Number)
            {
                jwkValues.Add(item.Name, item.Value.GetInt64());
            }
        }
        _header = new Dictionary<string, object>()
        {
            //{ "alg", "RS265" }, // JsonWebTokenHandler requires adding this itself
            { "typ", "dpop+jwt" },
            { "jwk", jwkValues },
        };
    }

    private string CreateDPoPProofToken(string alg = "RS256", SecurityKey key = null)
    {
        var payload = new Dictionary<string, object>(_payload);
        if (!payload.ContainsKey("jti"))
        {
            payload.Add("jti", CryptoRandom.CreateUniqueId());
        }
        if (!payload.ContainsKey("iat"))
        {
            payload.Add("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        key ??= new Microsoft.IdentityModel.Tokens.JsonWebKey(_privateJWK);
        var handler = new JsonWebTokenHandler() { SetDefaultTimesOnTokenCreation = false };
        var token = handler.CreateToken(JsonSerializer.Serialize(payload), new SigningCredentials(key, alg), _header);
        return token;
    }

    private IEnumerable<Claim> ParseAccessTokenClaims(TokenResponse tokenResponse)
    {
        tokenResponse.IsError.ShouldBeFalse(tokenResponse.Error);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenResponse.AccessToken);
        return token.Claims;
    }
    private string GetJKTFromAccessToken(TokenResponse tokenResponse)
    {
        var claims = ParseAccessTokenClaims(tokenResponse);
        return GetJKTFromCnfClaim(claims);
    }
    private string GetJKTFromCnfClaim(IEnumerable<Claim> claims)
    {
        var cnf = claims.SingleOrDefault(x => x.Type == "cnf")?.Value;
        if (cnf != null)
        {
            var json = JsonSerializer.Deserialize<JsonElement>(cnf);
            return json.GetString("jkt");
        }
        return null;
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_dpop_request_should_return_bound_access_token()
    {
        var dpopToken = CreateDPoPProofToken();
        var request = new ClientCredentialsTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "scope1",
        };
        request.Headers.Add("DPoP", dpopToken);

        var response = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);
        response.IsError.ShouldBeFalse();
        response.TokenType.ShouldBe("DPoP");
        var jkt = GetJKTFromAccessToken(response);
        jkt.ShouldBe(_JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_dpop_request_with_unusual_but_valid_proof_token_should_return_bound_access_token()
    {
        // The point here is to have an array in the payload, to exercise 
        // the json serialization
        _payload.Add("key_ops", new string[] { "sign", "verify" });

        var dpopToken = CreateDPoPProofToken();
        var request = new ClientCredentialsTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "scope1",
        };
        request.Headers.Add("DPoP", dpopToken);

        var response = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);
        response.IsError.ShouldBeFalse();
        response.TokenType.ShouldBe("DPoP");
        var jkt = GetJKTFromAccessToken(response);
        jkt.ShouldBe(_JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_proof_token_too_long_should_fail()
    {
        _payload.Add("foo", new string('x', 3000));

        var dpopToken = CreateDPoPProofToken();
        var request = new ClientCredentialsTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "scope1",
        };
        request.Headers.Add("DPoP", dpopToken);

        var response = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);
        response.IsError.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task replayed_dpop_token_should_fail()
    {
        var dpopToken = CreateDPoPProofToken();

        {
            var request = new ClientCredentialsTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = "client1",
                ClientSecret = "secret",
                Scope = "scope1",
            };
            request.Headers.Add("DPoP", dpopToken);

            var response = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);
            response.IsError.ShouldBeFalse();
            response.TokenType.ShouldBe("DPoP");
            var jkt = GetJKTFromAccessToken(response);
            jkt.ShouldBe(_JKT);
        }

        {
            var request = new ClientCredentialsTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = "client1",
                ClientSecret = "secret",
                Scope = "scope1",
            };
            request.Headers.Add("DPoP", dpopToken);

            var response = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);
            response.IsError.ShouldBeTrue();
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_dpop_request_should_fail()
    {
        var request = new ClientCredentialsTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "scope1",
        };
        request.Headers.Add("DPoP", "malformed");

        var response = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);
        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("invalid_dpop_proof");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_dpop_token_when_required_should_fail()
    {
        _dpopConfidentialClient.RequireDPoP = true;

        var request = new ClientCredentialsTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "scope1",
        };

        var response = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);
        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("invalid_dpop_proof");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task multiple_dpop_tokens_should_fail()
    {
        var dpopToken = CreateDPoPProofToken();
        var request = new ClientCredentialsTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "scope1",
        };
        request.Headers.Add("DPoP", dpopToken);
        request.Headers.Add("DPoP", dpopToken);

        var response = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);
        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("invalid_dpop_proof");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_dpop_request_should_return_bound_refresh_token()
    {
        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "code",
            responseMode: "query",
            scope: "openid scope1 offline_access",
            redirectUri: "https://client1/callback");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client1/callback");

        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();

        var codeRequest = new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Code = authorization.Code,
            RedirectUri = "https://client1/callback",
        };
        codeRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        var codeResponse = await _mockPipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.IsError.ShouldBeFalse();
        codeResponse.TokenType.ShouldBe("DPoP");
        GetJKTFromAccessToken(codeResponse).ShouldBe(_JKT);

        var rtRequest = new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            RefreshToken = codeResponse.RefreshToken
        };
        rtRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        var rtResponse = await _mockPipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest);
        rtResponse.IsError.ShouldBeFalse();
        rtResponse.TokenType.ShouldBe("DPoP");
        GetJKTFromAccessToken(rtResponse).ShouldBe(_JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task confidential_client_dpop_proof_should_be_required_on_renewal()
    {
        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "code",
            responseMode: "query",
            scope: "openid scope1 offline_access",
            redirectUri: "https://client1/callback");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client1/callback");

        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();

        var codeRequest = new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Code = authorization.Code,
            RedirectUri = "https://client1/callback",
        };
        codeRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        var codeResponse = await _mockPipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.IsError.ShouldBeFalse();
        codeResponse.TokenType.ShouldBe("DPoP");
        GetJKTFromAccessToken(codeResponse).ShouldBe(_JKT);

        var rtRequest = new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            RefreshToken = codeResponse.RefreshToken
        };
        // no DPoP header passed here

        var rtResponse = await _mockPipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest);
        rtResponse.IsError.ShouldBeTrue();
        rtResponse.Error.ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task public_client_dpop_proof_should_be_required_on_renewal()
    {
        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client2",
            responseType: "code",
            responseMode: "query",
            scope: "openid scope2 offline_access",
            redirectUri: "https://client2/callback");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client2/callback");

        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();

        var codeRequest = new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client2",
            ClientSecret = "secret",
            Code = authorization.Code,
            RedirectUri = "https://client2/callback",
        };
        codeRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        var codeResponse = await _mockPipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.IsError.ShouldBeFalse();
        codeResponse.TokenType.ShouldBe("DPoP");
        GetJKTFromAccessToken(codeResponse).ShouldBe(_JKT);

        var rtRequest = new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client2",
            ClientSecret = "secret",
            RefreshToken = codeResponse.RefreshToken
        };
        // no DPoP header passed here

        var rtResponse = await _mockPipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest);
        rtResponse.IsError.ShouldBeTrue();
        rtResponse.Error.ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task no_dpop_should_not_be_able_to_start_on_renewal()
    {
        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "code",
            responseMode: "query",
            scope: "openid scope1 offline_access",
            redirectUri: "https://client1/callback");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client1/callback");

        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();

        var codeRequest = new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Code = authorization.Code,
            RedirectUri = "https://client1/callback",
        };

        // no dpop here

        var codeResponse = await _mockPipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.IsError.ShouldBeFalse();

        var rtRequest = new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            RefreshToken = codeResponse.RefreshToken
        };

        CreateNewRSAKey();
        // now start dpop here
        rtRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        var rtResponse = await _mockPipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest);
        rtResponse.IsError.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task confidential_client_should_be_able_to_use_different_dpop_key_for_refresh_token_request()
    {
        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "code",
            responseMode: "query",
            scope: "openid scope1 offline_access",
            redirectUri: "https://client1/callback");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client1/callback");

        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();

        var codeRequest = new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Code = authorization.Code,
            RedirectUri = "https://client1/callback",
        };
        codeRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        var codeResponse = await _mockPipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.IsError.ShouldBeFalse();
        codeResponse.TokenType.ShouldBe("DPoP");
        GetJKTFromAccessToken(codeResponse).ShouldBe(_JKT);

        var rtRequest = new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            RefreshToken = codeResponse.RefreshToken
        };

        CreateNewRSAKey();
        rtRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        var rtResponse = await _mockPipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest);
        rtResponse.IsError.ShouldBeFalse();
        rtResponse.TokenType.ShouldBe("DPoP");
        GetJKTFromAccessToken(rtResponse).ShouldBe(_JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task public_client_should_not_be_able_to_use_different_dpop_key_for_refresh_token_request()
    {
        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client2",
            responseType: "code",
            responseMode: "query",
            scope: "openid scope2 offline_access",
            redirectUri: "https://client2/callback");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client2/callback");

        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();

        var codeRequest = new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client2",
            Code = authorization.Code,
            RedirectUri = "https://client2/callback",
        };
        codeRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        var codeResponse = await _mockPipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.IsError.ShouldBeFalse();
        codeResponse.TokenType.ShouldBe("DPoP");
        GetJKTFromAccessToken(codeResponse).ShouldBe(_JKT);

        var rtRequest = new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client2",
            RefreshToken = codeResponse.RefreshToken
        };

        var key = CryptoHelper.CreateRsaSecurityKey();
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        _privateJWK = JsonSerializer.Serialize(jwk);

        CreateNewRSAKey();
        rtRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        var rtResponse = await _mockPipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest);
        rtResponse.IsError.ShouldBeTrue();
        rtResponse.Error.ShouldBe("invalid_dpop_proof");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task public_client_using_same_dpop_key_for_refresh_token_request_should_succeed()
    {
        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client2",
            responseType: "code",
            responseMode: "query",
            scope: "openid scope2 offline_access",
            redirectUri: "https://client2/callback");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client2/callback");

        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();

        var codeRequest = new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client2",
            Code = authorization.Code,
            RedirectUri = "https://client2/callback",
        };
        codeRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        var codeResponse = await _mockPipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.IsError.ShouldBeFalse();
        codeResponse.TokenType.ShouldBe("DPoP");
        GetJKTFromAccessToken(codeResponse).ShouldBe(_JKT);

        var firstRefreshRequest = new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client2",
            RefreshToken = codeResponse.RefreshToken
        };
        firstRefreshRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        var firstRefreshResponse = await _mockPipeline.BackChannelClient.RequestRefreshTokenAsync(firstRefreshRequest);
        firstRefreshResponse.IsError.ShouldBeFalse();
        firstRefreshResponse.TokenType.ShouldBe("DPoP");
        GetJKTFromAccessToken(firstRefreshResponse).ShouldBe(_JKT);

        var secondRefreshRequest = new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client2",
            RefreshToken = codeResponse.RefreshToken
        };
        secondRefreshRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        firstRefreshRequest.Headers.GetValues("DPoP").FirstOrDefault().ShouldNotBe(
            secondRefreshRequest.Headers.GetValues("DPoP").FirstOrDefault());

        var secondRefreshResponse = await _mockPipeline.BackChannelClient.RequestRefreshTokenAsync(secondRefreshRequest);
        secondRefreshResponse.IsError.ShouldBeFalse(secondRefreshResponse.Error);
        secondRefreshResponse.TokenType.ShouldBe("DPoP");
        GetJKTFromAccessToken(secondRefreshResponse).ShouldBe(_JKT);
    }


    [Fact]
    [Trait("Category", Category)]
    public async Task missing_proof_token_when_required_on_refresh_token_request_should_fail()
    {
        _dpopConfidentialClient.RequireDPoP = true;

        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "code",
            responseMode: "query",
            scope: "openid scope1 offline_access",
            redirectUri: "https://client1/callback");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client1/callback");

        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();

        var codeRequest = new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Code = authorization.Code,
            RedirectUri = "https://client1/callback",
        };
        codeRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        var codeResponse = await _mockPipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.IsError.ShouldBeFalse();
        codeResponse.TokenType.ShouldBe("DPoP");
        GetJKTFromAccessToken(codeResponse).ShouldBe(_JKT);

        var rtRequest = new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            RefreshToken = codeResponse.RefreshToken
        };

        var rtResponse = await _mockPipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest);
        rtResponse.IsError.ShouldBeTrue();
        rtResponse.Error.ShouldBe("invalid_dpop_proof");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_dpop_request_using_reference_token_at_introspection_should_return_binding_information()
    {
        _dpopConfidentialClient.AccessTokenType = AccessTokenType.Reference;

        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "code",
            responseMode: "query",
            scope: "openid scope1 offline_access",
            redirectUri: "https://client1/callback");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client1/callback");

        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();

        var codeRequest = new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Code = authorization.Code,
            RedirectUri = "https://client1/callback",
        };
        codeRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        var codeResponse = await _mockPipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);

        var introspectionRequest = new TokenIntrospectionRequest
        {
            Address = IdentityServerPipeline.IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",
            Token = codeResponse.AccessToken,
        };
        var introspectionResponse = await _mockPipeline.BackChannelClient.IntrospectTokenAsync(introspectionRequest);
        introspectionResponse.IsError.ShouldBeFalse();
        GetJKTFromCnfClaim(introspectionResponse.Claims).ShouldBe(_JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_dpop_request_using_jwt_at_introspection_should_return_binding_information()
    {
        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "code",
            responseMode: "query",
            scope: "openid scope1 offline_access",
            redirectUri: "https://client1/callback");
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client1/callback");

        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();

        var codeRequest = new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Code = authorization.Code,
            RedirectUri = "https://client1/callback",
        };
        codeRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        var codeResponse = await _mockPipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);

        var introspectionRequest = new TokenIntrospectionRequest
        {
            Address = IdentityServerPipeline.IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",
            Token = codeResponse.AccessToken,
        };
        var introspectionResponse = await _mockPipeline.BackChannelClient.IntrospectTokenAsync(introspectionRequest);
        introspectionResponse.IsError.ShouldBeFalse();
        GetJKTFromCnfClaim(introspectionResponse.Claims).ShouldBe(_JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task matching_dpop_key_thumbprint_on_authorize_endpoint_and_token_endpoint_should_succeed()
    {
        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "code",
            responseMode: "query",
            scope: "openid scope1 offline_access",
            redirectUri: "https://client1/callback",
            extra: new
            {
                dpop_jkt = _JKT
            });
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client1/callback");

        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();

        var codeRequest = new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Code = authorization.Code,
            RedirectUri = "https://client1/callback",
        };
        codeRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        var codeResponse = await _mockPipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.IsError.ShouldBeFalse();
        GetJKTFromAccessToken(codeResponse).ShouldBe(_JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_key_thumbprint_too_long_should_fail()
    {
        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = true;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "code",
            responseMode: "query",
            scope: "openid scope1 offline_access",
            redirectUri: "https://client1/callback",
            extra: new
            {
                dpop_jkt = new string('x', 101)
            });
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task mismatched_dpop_key_thumbprint_on_authorize_endpoint_and_token_endpoint_should_fail()
    {
        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "code",
            responseMode: "query",
            scope: "openid scope1 offline_access",
            redirectUri: "https://client1/callback",
            extra: new
            {
                dpop_jkt = "invalid"
            });
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client1/callback");

        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();

        var codeRequest = new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Code = authorization.Code,
            RedirectUri = "https://client1/callback",
        };
        codeRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        var codeResponse = await _mockPipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.IsError.ShouldBeTrue();
        codeResponse.Error.ShouldBe("invalid_dpop_proof");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task server_issued_nonce_should_be_emitted()
    {
        string nonce = default;

        _mockPipeline.OnPostConfigureServices += services =>
        {
            services.AddSingleton<MockDPoPProofValidator>();
            services.AddSingleton<IDPoPProofValidator>(sp =>
            {
                var mockValidator = sp.GetRequiredService<MockDPoPProofValidator>();
                mockValidator.ServerIssuedNonce = nonce;
                return mockValidator;
            });
        };
        _mockPipeline.Initialize();

        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "code",
            responseMode: "query",
            scope: "openid scope1 offline_access",
            redirectUri: "https://client1/callback",
            extra: new
            {
                dpop_jkt = "invalid"
            });
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://client1/callback");

        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();

        var codeRequest = new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Code = authorization.Code,
            RedirectUri = "https://client1/callback",
        };
        codeRequest.Headers.Add("DPoP", CreateDPoPProofToken());

        nonce = "nonce";

        var codeResponse = await _mockPipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.IsError.ShouldBeTrue();
        codeResponse.Error.ShouldBe("invalid_dpop_proof");
        codeResponse.HttpResponse.Headers.GetValues("DPoP-Nonce").Single().ShouldBe("nonce");
        // TODO: make IdentityModel expose the response headers?
    }

    public class MockDPoPProofValidator : DefaultDPoPProofValidator
    {
        public MockDPoPProofValidator(IdentityServerOptions options, IReplayCache replayCache, IClock clock, Microsoft.AspNetCore.DataProtection.IDataProtectionProvider dataProtectionProvider, ILogger<DefaultDPoPProofValidator> logger) : base(options, replayCache, clock, dataProtectionProvider, logger)
        {
        }

        public string ServerIssuedNonce { get; set; }

        protected override async Task ValidateFreshnessAsync(DPoPProofValidatonContext context, DPoPProofValidatonResult result)
        {
            if (ServerIssuedNonce.IsPresent())
            {
                result.ServerIssuedNonce = ServerIssuedNonce;
                result.IsError = true;
                return;
            }

            await base.ValidateFreshnessAsync(context, result);
        }
    }


    [Theory]
    [InlineData("RS256")]
    [InlineData("RS384")]
    [InlineData("RS512")]
    [InlineData("PS256")]
    [InlineData("PS384")]
    [InlineData("PS512")]
    [Trait("Category", Category)]
    public async Task different_rsa_proof_tokens_should_work(string alg)
    {
        CreateNewRSAKey();
        var proofToken = CreateDPoPProofToken(alg);

        var request = new ClientCredentialsTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "scope1",
        };
        request.Headers.Add("DPoP", proofToken);

        var response = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);
        response.IsError.ShouldBeFalse();
        response.TokenType.ShouldBe("DPoP");
        var jkt = GetJKTFromAccessToken(response);
        jkt.ShouldBe(_JKT);
    }

    [Theory]
    [InlineData("ES256")]
    [InlineData("ES384")]
    [InlineData("ES512")]
    [Trait("Category", Category)]
    public async Task different_ps_proof_tokens_should_work(string alg)
    {
        CreateNewECKey();
        var proofToken = CreateDPoPProofToken(alg);

        var request = new ClientCredentialsTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "scope1",
        };
        request.Headers.Add("DPoP", proofToken);

        var response = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);
        response.IsError.ShouldBeFalse();
        response.TokenType.ShouldBe("DPoP");
        var jkt = GetJKTFromAccessToken(response);
        jkt.ShouldBe(_JKT);
    }

}
