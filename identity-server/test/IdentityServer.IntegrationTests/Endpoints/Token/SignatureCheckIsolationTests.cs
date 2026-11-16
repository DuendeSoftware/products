// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Token;

public class SignatureCheckIsolationTests : DPoPEndpointTestBase
{
    private const string Category = "Signature check isolation";
    private const string ForeignClientId = "foreign_client";

    private readonly RSA _foreignRsa = RSA.Create(2048);
    private readonly X509Certificate2 _foreignCertificate;

    public SignatureCheckIsolationTests()
    {
        _foreignCertificate = new CertificateRequest("CN=foreign", _foreignRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        Pipeline.OnPostConfigureServices += services =>
            services.AddIdentityServerBuilder().AddJwtBearerClientAuthentication();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_requesting_a_token_with_a_valid_proof_after_other_code_checked_a_jwk_with_the_same_public_key_and_a_foreign_x5c_expect_a_bound_token()
    {
        CreateNewRSAKey();
        InitializePipeline();
        var victimKey = new Microsoft.IdentityModel.Tokens.JsonWebKey(PublicJWK);
        await CheckForeignSignatureLikeOtherCode(PublicKeyWithForeignX5c(victimKey), new SigningCredentials(new RsaSecurityKey(_foreignRsa), SecurityAlgorithms.RsaSha256));

        var victimResponse = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(CreateClientCredentialsTokenRequest(CreateDPoPProofToken()));

        victimResponse.ShouldHaveDPoPThumbprint(JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_requesting_a_token_with_a_proof_signed_by_another_key_after_a_client_authenticated_with_a_jwk_with_the_same_public_key_and_a_foreign_x5c_expect_invalid_dpop_proof()
    {
        CreateNewRSAKey();
        var victimKey = new Microsoft.IdentityModel.Tokens.JsonWebKey(PublicJWK);
        AddForeignClient(PublicKeyWithForeignX5c(victimKey));
        InitializePipeline();
        var foreignSignIn = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(CreateForeignClientAssertionTokenRequest(ForeignClientId));
        foreignSignIn.IsError.ShouldBeFalse();

        var forgedProof = CreateDPoPProofToken(key: new RsaSecurityKey(_foreignRsa));
        var forgedResponse = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(CreateClientCredentialsTokenRequest(forgedProof));

        forgedResponse.Error.ShouldBe(OidcConstants.TokenErrors.InvalidDPoPProof);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_requesting_a_token_with_a_client_assertion_signed_by_another_key_after_other_code_checked_a_jwk_with_the_client_public_key_and_a_foreign_x5c_expect_invalid_client()
    {
        CreateNewRSAKey();
        var victimKey = new Microsoft.IdentityModel.Tokens.JsonWebKey(PublicJWK);
        Pipeline.Clients.Add(new Client
        {
            ClientId = "victim_client",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret { Type = IdentityServerConstants.SecretTypes.JsonWebKey, Value = PublicJWK } },
            AllowedScopes = { "scope1" }
        });
        InitializePipeline();
        await CheckForeignSignatureLikeOtherCode(PublicKeyWithForeignX5c(victimKey), new SigningCredentials(new RsaSecurityKey(_foreignRsa), SecurityAlgorithms.RsaSha256));

        var forgedResponse = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(CreateForeignClientAssertionTokenRequest("victim_client"));

        forgedResponse.Error.ShouldBe(OidcConstants.TokenErrors.InvalidClient);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_requesting_a_token_with_a_proof_signed_by_another_key_after_a_client_sent_a_request_object_signed_for_a_jwk_with_the_same_public_key_and_a_foreign_x5c_expect_invalid_dpop_proof()
    {
        CreateNewRSAKey();
        var victimKey = new Microsoft.IdentityModel.Tokens.JsonWebKey(PublicJWK);
        AddForeignClient(PublicKeyWithForeignX5c(victimKey));
        InitializePipeline();
        _ = await Pipeline.BrowserClient.GetAsync(CreateAuthorizeUrlWithForeignRequestObject(ForeignClientId, "https://foreign/callback"));
        Pipeline.LoginWasCalled.ShouldBeTrue();
        Pipeline.ErrorWasCalled.ShouldBeFalse();

        var forgedProof = CreateDPoPProofToken(key: new RsaSecurityKey(_foreignRsa));
        var forgedResponse = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(CreateClientCredentialsTokenRequest(forgedProof));

        forgedResponse.Error.ShouldBe(OidcConstants.TokenErrors.InvalidDPoPProof);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_authorizing_with_a_request_object_signed_by_another_key_after_a_client_sent_a_request_object_signed_for_a_jwk_with_the_same_public_key_and_a_foreign_x5c_expect_an_error()
    {
        CreateNewRSAKey();
        var victimKey = new Microsoft.IdentityModel.Tokens.JsonWebKey(PublicJWK);
        Pipeline.Clients.Add(new Client
        {
            ClientId = "victim_client",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = false,
            ClientSecrets = { new Secret { Type = IdentityServerConstants.SecretTypes.JsonWebKey, Value = PublicJWK } },
            RedirectUris = { "https://victim/callback" },
            AllowedScopes = { "openid" }
        });
        AddForeignClient(PublicKeyWithForeignX5c(victimKey));
        InitializePipeline();
        _ = await Pipeline.BrowserClient.GetAsync(CreateAuthorizeUrlWithForeignRequestObject(ForeignClientId, "https://foreign/callback"));
        Pipeline.LoginWasCalled.ShouldBeTrue();
        Pipeline.ErrorWasCalled.ShouldBeFalse();

        _ = await Pipeline.BrowserClient.GetAsync(CreateAuthorizeUrlWithForeignRequestObject("victim_client", "https://victim/callback"));

        Pipeline.ErrorMessage.ShouldNotBeNull().Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequestObject);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_calling_userinfo_with_a_token_signed_by_another_key_after_other_code_checked_a_jwk_with_the_signing_public_key_and_a_foreign_x5c_expect_unauthorized()
    {
        var signingKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(CryptoHelper.CreateRsaSecurityKey());
        Pipeline.OnPostConfigureServices += services =>
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
        var userInfoResponse = await Pipeline.BackChannelClient.GetUserInfoAsync(new UserInfoRequest
        {
            Address = IdentityServerPipeline.UserInfoEndpoint,
            Token = forgedAccessToken
        });

        userInfoResponse.HttpStatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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

    private void AddForeignClient(Dictionary<string, object> registeredKey) => Pipeline.Clients.Add(new Client
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
        return Pipeline.CreateAuthorizeUrl(clientId: clientId, responseType: "code", extra: new { request = requestObject });
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
        otherCodeCheck.IsValid.ShouldBeTrue();
    }
}
