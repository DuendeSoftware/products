// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;
using IdentityModel;
using IdentityModel.Client;
using IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace IntegrationTests.Endpoints.Token;

public class SignatureCheckIsolationTests
{
    private const string Category = "Signature check isolation";
    private const string ForeignClientId = "foreign_client";

    private readonly IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();
    private readonly RSA _foreignRsa = RSA.Create(2048);
    private readonly X509Certificate2 _foreignCertificate;

    private Dictionary<string, object> _header;
    private Dictionary<string, object> _payload;
    private string _privateJWK;
    private string _publicJWK;
    private string _JKT;

    public SignatureCheckIsolationTests()
    {
        _foreignCertificate = new CertificateRequest("CN=foreign", _foreignRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        _mockPipeline.Clients.AddRange(new Client[]
        {
            new Client
            {
                ClientId = "client1",
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets = { new Secret("secret".Sha256()) },
                AllowedScopes = new List<string> { "scope1" },
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
            }
        });

        _mockPipeline.IdentityScopes.Add(new IdentityResources.OpenId());
        _mockPipeline.ApiResources.Add(new ApiResource("api1")
        {
            Scopes = { "scope1" },
            ApiSecrets = { new Secret("secret".Sha256()) }
        });
        _mockPipeline.ApiScopes.Add(new ApiScope { Name = "scope1" });

        _mockPipeline.OnPostConfigureServices += services =>
            services.AddIdentityServerBuilder().AddJwtBearerClientAuthentication();

        _payload = new Dictionary<string, object>
        {
            { "htm", "POST" },
            { "htu", IdentityServerPipeline.TokenEndpoint },
        };

        CreateNewRSAKey();
    }

    private void InitializePipeline() => _mockPipeline.Initialize();

    [Fact]
    [Trait("Category", Category)]
    public async Task when_requesting_a_token_with_a_valid_proof_after_other_code_checked_a_jwk_with_the_same_public_key_and_a_foreign_x5c_expect_a_bound_token()
    {
        InitializePipeline();
        var victimKey = new Microsoft.IdentityModel.Tokens.JsonWebKey(_publicJWK);
        await CheckForeignSignatureLikeOtherCode(PublicKeyWithForeignX5c(victimKey), new SigningCredentials(new RsaSecurityKey(_foreignRsa), SecurityAlgorithms.RsaSha256));

        var victimRequest = CreateClientCredentialsTokenRequest(CreateDPoPProofToken());
        var victimResponse = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(victimRequest);

        victimResponse.IsError.Should().BeFalse(victimResponse.Error);
        GetJKTFromAccessToken(victimResponse).Should().Be(_JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_requesting_a_token_with_a_proof_signed_by_another_key_after_a_client_authenticated_with_a_jwk_with_the_same_public_key_and_a_foreign_x5c_expect_invalid_dpop_proof()
    {
        var victimKey = new Microsoft.IdentityModel.Tokens.JsonWebKey(_publicJWK);
        AddForeignClient(PublicKeyWithForeignX5c(victimKey));
        InitializePipeline();
        var foreignSignIn = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(CreateForeignClientAssertionTokenRequest(ForeignClientId));
        foreignSignIn.IsError.Should().BeFalse(foreignSignIn.Error);

        var forgedProof = CreateDPoPProofToken(key: new RsaSecurityKey(_foreignRsa));
        var forgedResponse = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(CreateClientCredentialsTokenRequest(forgedProof));

        forgedResponse.Error.Should().Be(OidcConstants.TokenErrors.InvalidDPoPProof);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_requesting_a_token_with_a_client_assertion_signed_by_another_key_after_other_code_checked_a_jwk_with_the_client_public_key_and_a_foreign_x5c_expect_invalid_client()
    {
        var victimKey = new Microsoft.IdentityModel.Tokens.JsonWebKey(_publicJWK);
        _mockPipeline.Clients.Add(new Client
        {
            ClientId = "victim_client",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret { Type = IdentityServerConstants.SecretTypes.JsonWebKey, Value = _publicJWK } },
            AllowedScopes = { "scope1" }
        });
        InitializePipeline();
        await CheckForeignSignatureLikeOtherCode(PublicKeyWithForeignX5c(victimKey), new SigningCredentials(new RsaSecurityKey(_foreignRsa), SecurityAlgorithms.RsaSha256));

        var forgedResponse = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(CreateForeignClientAssertionTokenRequest("victim_client"));

        forgedResponse.Error.Should().Be(OidcConstants.TokenErrors.InvalidClient);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_requesting_a_token_with_a_proof_signed_by_another_key_after_a_client_sent_a_request_object_signed_for_a_jwk_with_the_same_public_key_and_a_foreign_x5c_expect_invalid_dpop_proof()
    {
        var victimKey = new Microsoft.IdentityModel.Tokens.JsonWebKey(_publicJWK);
        AddForeignClient(PublicKeyWithForeignX5c(victimKey));
        InitializePipeline();
        _ = await _mockPipeline.BrowserClient.GetAsync(CreateAuthorizeUrlWithForeignRequestObject(ForeignClientId, "https://foreign/callback"));
        _mockPipeline.LoginWasCalled.Should().BeTrue();
        _mockPipeline.ErrorWasCalled.Should().BeFalse();

        var forgedProof = CreateDPoPProofToken(key: new RsaSecurityKey(_foreignRsa));
        var forgedResponse = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(CreateClientCredentialsTokenRequest(forgedProof));

        forgedResponse.Error.Should().Be(OidcConstants.TokenErrors.InvalidDPoPProof);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_authorizing_with_a_request_object_signed_by_another_key_after_a_client_sent_a_request_object_signed_for_a_jwk_with_the_same_public_key_and_a_foreign_x5c_expect_an_error()
    {
        var victimKey = new Microsoft.IdentityModel.Tokens.JsonWebKey(_publicJWK);
        _mockPipeline.Clients.Add(new Client
        {
            ClientId = "victim_client",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = false,
            ClientSecrets = { new Secret { Type = IdentityServerConstants.SecretTypes.JsonWebKey, Value = _publicJWK } },
            RedirectUris = { "https://victim/callback" },
            AllowedScopes = { "openid" }
        });
        AddForeignClient(PublicKeyWithForeignX5c(victimKey));
        InitializePipeline();
        _ = await _mockPipeline.BrowserClient.GetAsync(CreateAuthorizeUrlWithForeignRequestObject(ForeignClientId, "https://foreign/callback"));
        _mockPipeline.LoginWasCalled.Should().BeTrue();
        _mockPipeline.ErrorWasCalled.Should().BeFalse();

        _ = await _mockPipeline.BrowserClient.GetAsync(CreateAuthorizeUrlWithForeignRequestObject("victim_client", "https://victim/callback"));

        _mockPipeline.ErrorMessage.Should().NotBeNull();
        _mockPipeline.ErrorMessage.Error.Should().Be(OidcConstants.AuthorizeErrors.InvalidRequestObject);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_calling_userinfo_with_a_token_signed_by_another_key_after_other_code_checked_a_jwk_with_the_signing_public_key_and_a_foreign_x5c_expect_unauthorized()
    {
        var signingKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(CryptoHelper.CreateRsaSecurityKey());
        _mockPipeline.OnPostConfigureServices += services =>
            services.AddIdentityServerBuilder().AddSigningCredential(signingKey, SecurityAlgorithms.RsaSsaPssSha256);
        InitializePipeline();
        var foreignSigningCredentials = new SigningCredentials(new RsaSecurityKey(_foreignRsa) { KeyId = signingKey.Kid }, SecurityAlgorithms.RsaSsaPssSha256);
        await CheckForeignSignatureLikeOtherCode(PublicKeyWithForeignX5c(signingKey), foreignSigningCredentials);

        var forgedAccessToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = IdentityServerPipeline.BaseUrl,
            TokenType = "at+jwt",
            Claims = new Dictionary<string, object>
            {
                ["sub"] = "bob",
                ["client_id"] = "client1",
                ["scope"] = "openid"
            },
            SigningCredentials = foreignSigningCredentials
        });
        var userInfoResponse = await _mockPipeline.BackChannelClient.GetUserInfoAsync(new UserInfoRequest
        {
            Address = IdentityServerPipeline.UserInfoEndpoint,
            Token = forgedAccessToken
        });

        userInfoResponse.HttpStatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private ClientCredentialsTokenRequest CreateClientCredentialsTokenRequest(string proofToken)
    {
        var request = new ClientCredentialsTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "scope1",
        };
        request.Headers.Add(OidcConstants.HttpHeaders.DPoP, proofToken);
        return request;
    }

    private Dictionary<string, object> PublicKeyWithForeignX5c(Microsoft.IdentityModel.Tokens.JsonWebKey publicKey) => new()
    {
        ["kty"] = publicKey.Kty,
        ["n"] = publicKey.N,
        ["e"] = publicKey.E,
        ["x5c"] = new[] { Convert.ToBase64String(_foreignCertificate.RawData) }
    };

    private void AddForeignClient(Dictionary<string, object> registeredKey) => _mockPipeline.Clients.Add(new Client
    {
        ClientId = ForeignClientId,
        AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
        ClientSecrets =
        {
            new Secret
            {
                Type = IdentityServerConstants.SecretTypes.JsonWebKey,
                Value = JsonSerializer.Serialize(registeredKey)
            }
        },
        RedirectUris = { "https://foreign/callback" },
        RequirePkce = false,
        AllowedScopes = { "openid", "scope1" }
    });

    private ClientCredentialsTokenRequest CreateForeignClientAssertionTokenRequest(string clientId) => new()
    {
        Address = IdentityServerPipeline.TokenEndpoint,
        ClientId = clientId,
        ClientCredentialStyle = ClientCredentialStyle.PostBody,
        ClientAssertion =
        {
            Type = OidcConstants.ClientAssertionTypes.JwtBearer,
            Value = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = clientId,
                Audience = IdentityServerPipeline.TokenEndpoint,
                Claims = new Dictionary<string, object>
                {
                    [JwtClaimTypes.Subject] = clientId,
                    [JwtClaimTypes.JwtId] = CryptoRandom.CreateUniqueId()
                },
                SigningCredentials = new SigningCredentials(new RsaSecurityKey(_foreignRsa), SecurityAlgorithms.RsaSha256)
            })
        },
        Scope = "scope1"
    };

    private string CreateAuthorizeUrlWithForeignRequestObject(string clientId, string redirectUri)
    {
        var requestObject = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = clientId,
            Audience = IdentityServerPipeline.BaseUrl,
            Claims = new Dictionary<string, object>
            {
                ["client_id"] = clientId,
                ["response_type"] = "code",
                ["scope"] = "openid",
                ["redirect_uri"] = redirectUri
            },
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_foreignRsa), SecurityAlgorithms.RsaSha256)
        });
        return _mockPipeline.CreateAuthorizeUrl(clientId: clientId, responseType: "code", extra: new { request = requestObject });
    }

    private static async Task CheckForeignSignatureLikeOtherCode(Dictionary<string, object> key, SigningCredentials foreignSigningCredentials)
    {
        var tokenHandler = new JsonWebTokenHandler();
        var otherCodeCheck = await tokenHandler.ValidateTokenAsync(
            tokenHandler.CreateToken(new SecurityTokenDescriptor { SigningCredentials = foreignSigningCredentials }),
            new TokenValidationParameters
            {
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.JsonWebKey(JsonSerializer.Serialize(key)),
                ValidateIssuer = false,
                ValidateAudience = false
            });
        otherCodeCheck.IsValid.Should().BeTrue();
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

    private void CreateHeaderValuesFromPublicKey()
    {
        var jwk = JsonSerializer.Deserialize<JsonElement>(_publicJWK);
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
        }
        _header = new Dictionary<string, object>()
        {
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

    private string GetJKTFromAccessToken(TokenResponse tokenResponse)
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenResponse.AccessToken);
        var cnf = token.Claims.SingleOrDefaultClaimValue("cnf");
        if (cnf == null)
        {
            return null;
        }
        var cnfJson = JsonSerializer.Deserialize<JsonElement>(cnf);
        return cnfJson.GetProperty("jkt").GetString();
    }
}

internal static class ClaimsExtensions
{
    public static string SingleOrDefaultClaimValue(this IEnumerable<Claim> claims, string type)
    {
        foreach (var claim in claims)
        {
            if (claim.Type == type)
            {
                return claim.Value;
            }
        }
        return null;
    }
}
