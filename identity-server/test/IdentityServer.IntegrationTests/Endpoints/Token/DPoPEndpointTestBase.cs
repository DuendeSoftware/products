// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;
using IntegrationTests.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IntegrationTests.Endpoints.Token;

[ShouldlyMethods]
public static class DPoPAssertions
{
    private static string GetJKTFromAccessToken(TokenResponse tokenResponse)
    {
        var claims = ParseAccessTokenClaims(tokenResponse);
        return GetJKTFromCnfClaim(claims);
    }

    private static IEnumerable<Claim> ParseAccessTokenClaims(TokenResponse tokenResponse)
    {
        tokenResponse.IsError.ShouldBeFalse(tokenResponse.Error);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenResponse.AccessToken);
        return token.Claims;
    }

    private static string GetJKTFromCnfClaim(IEnumerable<Claim> claims)
    {
        var cnf = claims.SingleOrDefault(x => x.Type == "cnf")?.Value;
        if (cnf != null)
        {
            var json = JsonSerializer.Deserialize<JsonElement>(cnf);
            return json.GetString("jkt");
        }
        return null;
    }


    public static void ShouldHaveDPoPThumbprint(this TokenResponse token, string jkt)
    {
        token.IsError.ShouldBeFalse();
        token.TokenType.ShouldBe(OidcConstants.TokenResponse.DPoPTokenType);
        GetJKTFromAccessToken(token).ShouldBe(jkt);
    }
}

public abstract class DPoPEndpointTestBase
{
    protected IdentityServerPipeline Pipeline = new IdentityServerPipeline();

    protected Client ConfidentialClient;

    protected Client PublicClient;

    protected DateTime Now = new DateTime(2020, 3, 10, 9, 0, 0, DateTimeKind.Utc);
    protected DateTime UtcNow
    {
        get
        {
            if (Now > DateTime.MinValue) return Now;
            return DateTime.UtcNow;
        }
    }

    protected Dictionary<string, object> Header;
    protected Dictionary<string, object> Payload;
    protected string PrivateJWK = "{\"Crv\":null,\"D\":\"QeBWodq0hSYjfAxxo0VZleXLqwwZZeNWvvFfES4WyItao_-OJv1wKA7zfkZxbWkpK5iRbKrl2AMJ52AtUo5JJ6QZ7IjAQlgM0lBg3ltjb1aA0gBsK5XbiXcsV8DiAnRuy6-XgjAKPR8Lo-wZl_fdPbVoAmpSdmfn_6QXXPBai5i7FiyDbQa16pI6DL-5SCj7F78QDTRiJOqn5ElNvtoJEfJBm13giRdqeriFi3pCWo7H3QBgTEWtDNk509z4w4t64B2HTXnM0xj9zLnS42l7YplJC7MRibD4nVBMtzfwtGRKLj8beuDgtW9pDlQqf7RVWX5pHQgiHAZmUi85TEbYdQ\",\"DP\":\"h2F54OMaC9qq1yqR2b55QNNaChyGtvmTHSdqZJ8lJFqvUorlz-Uocj2BTowWQnaMd8zRKMdKlSeUuSv4Z6WmjSxSsNbonI6_II5XlZLWYqFdmqDS-xCmJY32voT5Wn7OwB9xj1msDqrFPg-PqSBOh5OppjCqXqDFcNvSkQSajXc\",\"DQ\":\"VABdS20Nxkmq6JWLQj7OjRxVJuYsHrfmWJmDA7_SYtlXaPUcg-GiHGQtzdDWEeEi0dlJjv9I3FdjKGC7CGwqtVygW38DzVYJsV2EmRNJc1-j-1dRs_pK9GWR4NYm0mVz_IhS8etIf9cfRJk90xU3AL3_J6p5WNF7I5ctkLpnt8M\",\"E\":\"AQAB\",\"K\":null,\"KeyOps\":[],\"Kty\":\"RSA\",\"N\":\"yWWAOSV3Z_BW9rJEFvbZyeU-q2mJWC0l8WiHNqwVVf7qXYgm9hJC0j1aPHku_Wpl38DpK3Xu3LjWOFG9OrCqga5Pzce3DDJKI903GNqz5wphJFqweoBFKOjj1wegymvySsLoPqqDNVYTKp4nVnECZS4axZJoNt2l1S1bC8JryaNze2stjW60QT-mIAGq9konKKN3URQ12dr478m0Oh-4WWOiY4HrXoSOklFmzK-aQx1JV_SZ04eIGfSw1pZZyqTaB1BwBotiy-QA03IRxwIXQ7BSx5EaxC5uMCMbzmbvJqjt-q8Y1wyl-UQjRucgp7hkfHSE1QT3zEex2Q3NFux7SQ\",\"Oth\":null,\"P\":\"_T7MTkeOh5QyqlYCtLQ2RWf2dAJ9i3wrCx4nEDm1c1biijhtVTL7uJTLxwQIM9O2PvOi5Dq-UiGy6rhHZqf5akWTeHtaNyI-2XslQfaS3ctRgmGtRQL_VihK-R9AQtDx4eWL4h-bDJxPaxby_cVo_j2MX5AeoC1kNmcCdDf_X0M\",\"Q\":\"y5ZSThaGLjaPj8Mk2nuD8TiC-sb4aAZVh9K-W4kwaWKfDNoPcNb_dephBNMnOp9M1br6rDbyG7P-Sy_LOOsKg3Q0wHqv4hnzGaOQFeMJH4HkXYdENC7B5JG9PefbC6zwcgZWiBnsxgKpScNWuzGF8x2CC-MdsQ1bkQeTPbJklIM\",\"QI\":\"i716Vt9II_Rt6qnjsEhfE4bej52QFG9a1hSnx5PDNvRrNqR_RpTA0lO9qeXSZYGHTW_b6ZXdh_0EUwRDEDHmaxjkIcTADq6JLuDltOhZuhLUSc5NCKLAVCZlPcaSzv8-bZm57mVcIpx0KyFHxvk50___Jgx1qyzwLX03mPGUbDQ\",\"Use\":null,\"X\":null,\"X5c\":[],\"X5t\":null,\"X5tS256\":null,\"X5u\":null,\"Y\":null,\"KeySize\":2048,\"HasPrivateKey\":true,\"CryptoProviderFactory\":{\"CryptoProviderCache\":{},\"CustomCryptoProvider\":null,\"CacheSignatureProviders\":true,\"SignatureProviderObjectPoolCacheSize\":80}}";
    protected string PublicJWK = "{\"kty\":\"RSA\",\"use\":\"sig\",\"x5t\":null,\"e\":\"AQAB\",\"n\":\"yWWAOSV3Z_BW9rJEFvbZyeU-q2mJWC0l8WiHNqwVVf7qXYgm9hJC0j1aPHku_Wpl38DpK3Xu3LjWOFG9OrCqga5Pzce3DDJKI903GNqz5wphJFqweoBFKOjj1wegymvySsLoPqqDNVYTKp4nVnECZS4axZJoNt2l1S1bC8JryaNze2stjW60QT-mIAGq9konKKN3URQ12dr478m0Oh-4WWOiY4HrXoSOklFmzK-aQx1JV_SZ04eIGfSw1pZZyqTaB1BwBotiy-QA03IRxwIXQ7BSx5EaxC5uMCMbzmbvJqjt-q8Y1wyl-UQjRucgp7hkfHSE1QT3zEex2Q3NFux7SQ\",\"x5c\":null,\"x\":null,\"y\":null,\"crv\":null}";
    protected string JKT = "JGSVlE73oKtQQI1dypYg8_JNat0xJjsQNyOI5oxaZf4";

    public DPoPEndpointTestBase()
    {
        Pipeline.OnPostConfigureServices += services =>
        {
        };

        Pipeline.Clients.AddRange([
            ConfidentialClient = new Client
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
            PublicClient = new Client
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
        ]);

        Pipeline.Users.Add(new TestUser
        {
            SubjectId = "bob",
            Username = "bob",
            Password = "bob",
            Claims =
            [
                new Claim("name", "Bob Loblaw"),
                new Claim("email", "bob@loblaw.com"),
                new Claim("role", "Attorney")
            ]
        });

        Pipeline.IdentityScopes.AddRange([
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email()
        ]);
        Pipeline.ApiResources.AddRange(
        [
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
        ]);
        Pipeline.ApiScopes.AddRange([
            new ApiScope
            {
                Name = "scope1"
            },
            new ApiScope
            {
                Name = "scope2"
            }
        ]);

        Pipeline.Initialize();

        Payload = new Dictionary<string, object>
        {
            //{ "jti", CryptoRandom.CreateUniqueId() }, // added dynamically below in CreateDPoPProofToken
            //{ "iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }, // added dynamically below in CreateDPoPProofToken
            { "htm", "POST" },
            { "htu", IdentityServerPipeline.TokenEndpoint },
        };

        CreateHeaderValuesFromPublicKey();
    }

    protected void CreateNewRSAKey()
    {
        var key = CryptoHelper.CreateRsaSecurityKey();
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        JKT = Base64UrlEncoder.Encode(key.ComputeJwkThumbprint());
        PrivateJWK = JsonSerializer.Serialize(jwk);
        PublicJWK = JsonSerializer.Serialize(new
        {
            kty = jwk.Kty,
            e = jwk.E,
            n = jwk.N,
        });

        CreateHeaderValuesFromPublicKey();
    }

    protected void CreateNewECKey()
    {
        var key = CryptoHelper.CreateECDsaSecurityKey();
        var jwk = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(key);
        JKT = Base64UrlEncoder.Encode(key.ComputeJwkThumbprint());
        PrivateJWK = JsonSerializer.Serialize(jwk);
        PublicJWK = JsonSerializer.Serialize(new
        {
            kty = jwk.Kty,
            x = jwk.X,
            y = jwk.Y,
            crv = jwk.Crv
        });

        CreateHeaderValuesFromPublicKey();
    }

    protected void CreateHeaderValuesFromPublicKey(string publicJwk = null)
    {
        var jwk = JsonSerializer.Deserialize<JsonElement>(publicJwk ?? PublicJWK);
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
        Header = new Dictionary<string, object>()
        {
            //{ "alg", "RS265" }, // JsonWebTokenHandler requires adding this itself
            { "typ", "dpop+jwt" },
            { "jwk", jwkValues },
        };
    }

    protected string CreateDPoPProofToken(string alg = "RS256", SecurityKey key = null)
    {
        var payload = new Dictionary<string, object>(Payload);
        if (!payload.ContainsKey("jti"))
        {
            payload.Add("jti", CryptoRandom.CreateUniqueId());
        }
        if (!payload.ContainsKey("iat"))
        {
            payload.Add("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        key ??= new Microsoft.IdentityModel.Tokens.JsonWebKey(PrivateJWK);
        var handler = new JsonWebTokenHandler() { SetDefaultTimesOnTokenCreation = false };
        var token = handler.CreateToken(JsonSerializer.Serialize(payload), new SigningCredentials(key, alg), Header);
        return token;
    }

    protected IEnumerable<Claim> ParseAccessTokenClaims(TokenResponse tokenResponse)
    {
        tokenResponse.IsError.ShouldBeFalse(tokenResponse.Error);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenResponse.AccessToken);
        return token.Claims;
    }
    protected string GetJKTFromAccessToken(TokenResponse tokenResponse)
    {
        var claims = ParseAccessTokenClaims(tokenResponse);
        return GetJKTFromCnfClaim(claims);
    }
    protected string GetJKTFromCnfClaim(IEnumerable<Claim> claims)
    {
        var cnf = claims.SingleOrDefault(x => x.Type == "cnf")?.Value;
        if (cnf != null)
        {
            var json = JsonSerializer.Deserialize<JsonElement>(cnf);
            return json.GetString("jkt");
        }
        return null;
    }
}
