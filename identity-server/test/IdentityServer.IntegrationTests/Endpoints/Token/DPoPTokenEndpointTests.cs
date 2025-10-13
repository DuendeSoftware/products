// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Token;

public class DPoPTokenEndpointTests : DPoPEndpointTestBase
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    protected const string Category = "DPoP Token endpoint";

    private ClientCredentialsTokenRequest CreateClientCredentialsTokenRequest(
        string proofToken = null, bool omitDPoPProof = false)
    {
        var request = new ClientCredentialsTokenRequest()
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "scope1",
        };
        if (!omitDPoPProof)
        {
            proofToken ??= CreateDPoPProofToken();
            request.Headers.Add("DPoP", proofToken);
        }
        return request;
    }

    private RefreshTokenRequest CreateRefreshTokenRequest(
        TokenResponse codeResponse, string clientId = "client1", bool omitDPoPProof = false)
    {
        var rtRequest = new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = clientId,
            ClientSecret = "secret",
            RefreshToken = codeResponse.RefreshToken
        };
        if (!omitDPoPProof)
        {
            rtRequest.Headers.Add("DPoP", CreateDPoPProofToken());
        }
        return rtRequest;
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_dpop_request_should_return_bound_access_token()
    {
        var request = CreateClientCredentialsTokenRequest();

        var response = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request, _ct);

        response.IsError.ShouldBeFalse();
        response.TokenType.ShouldBe("DPoP");
        var jkt = GetJKTFromAccessToken(response);
        jkt.ShouldBe(JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_dpop_request_with_unusual_but_valid_proof_token_should_return_bound_access_token()
    {
        // The point here is to have an array in the payload, to exercise
        // the json serialization
        Payload.Add("key_ops", new string[] { "sign", "verify" });
        var request = CreateClientCredentialsTokenRequest();

        var response = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request, _ct);

        response.IsError.ShouldBeFalse();
        response.TokenType.ShouldBe("DPoP");
        var jkt = GetJKTFromAccessToken(response);
        jkt.ShouldBe(JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_proof_token_too_long_should_fail()
    {
        Payload.Add("foo", new string('x', 3000));
        var request = CreateClientCredentialsTokenRequest();

        var response = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request, _ct);

        response.IsError.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task replayed_dpop_token_should_fail()
    {
        // Shared proof used throughout
        var dpopToken = CreateDPoPProofToken();

        // Initial request succeeds
        var firstRequest = CreateClientCredentialsTokenRequest(dpopToken);
        var firstResponse = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(firstRequest, _ct);
        firstResponse.IsError.ShouldBeFalse();
        firstResponse.TokenType.ShouldBe("DPoP");
        var jkt = GetJKTFromAccessToken(firstResponse);
        jkt.ShouldBe(JKT);

        // Second request fails
        var secondRequest = CreateClientCredentialsTokenRequest(dpopToken);
        secondRequest.Headers.Add("DPoP", dpopToken);
        var secondResponse = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(secondRequest, _ct);
        secondResponse.IsError.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_dpop_request_should_fail()
    {
        var request = CreateClientCredentialsTokenRequest(proofToken: "malformed");

        var response = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request, _ct);

        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("invalid_dpop_proof");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_dpop_token_when_required_should_fail()
    {
        ConfidentialClient.RequireDPoP = true;
        var request = CreateClientCredentialsTokenRequest(omitDPoPProof: true);

        var response = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request, _ct);

        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task multiple_dpop_tokens_should_fail()
    {
        var request = CreateClientCredentialsTokenRequest(omitDPoPProof: true);
        var dpopToken = CreateDPoPProofToken();
        request.Headers.Add("DPoP", dpopToken);
        request.Headers.Add("DPoP", dpopToken);

        var response = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request, _ct);

        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("invalid_request");
    }

    [Theory]
    [ClassData(typeof(ParModes))]
    [Trait("Category", Category)]
    public async Task valid_dpop_request_should_return_bound_refresh_token(ParMode parMode)
    {
        var codeRequest = await CreateAuthCodeTokenRequestAsync(parMode: parMode);
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest, _ct);
        codeResponse.ShouldHaveDPoPThumbprint(JKT);

        var rtRequest = CreateRefreshTokenRequest(codeResponse);
        var rtResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest, _ct);
        rtResponse.ShouldHaveDPoPThumbprint(JKT);
    }

    [Theory]
    [ClassData(typeof(ParModes))]
    [Trait("Category", Category)]
    public async Task confidential_client_dpop_proof_should_be_required_on_renewal(ParMode parMode)
    {
        var codeRequest = await CreateAuthCodeTokenRequestAsync(parMode: parMode);
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest, _ct);
        codeResponse.ShouldHaveDPoPThumbprint(JKT);

        var rtRequest = CreateRefreshTokenRequest(codeResponse, omitDPoPProof: true);
        var rtResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest, _ct);
        rtResponse.IsError.ShouldBeTrue();
        rtResponse.Error.ShouldBe("invalid_request");
    }

    [Theory]
    [ClassData(typeof(ParModes))]
    [Trait("Category", Category)]
    public async Task public_client_dpop_proof_should_be_required_on_renewal(ParMode parMode)
    {
        var codeRequest = await CreateAuthCodeTokenRequestAsync(clientId: "client2", parMode: parMode);
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest, _ct);
        codeResponse.ShouldHaveDPoPThumbprint(JKT);

        var rtRequest = CreateRefreshTokenRequest(codeResponse, clientId: "client2", omitDPoPProof: true);
        var rtResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest, _ct);
        rtResponse.IsError.ShouldBeTrue();
        rtResponse.Error.ShouldBe("invalid_request");
    }

    [Theory]
    [InlineData(ParMode.Unused)]
    [InlineData(ParMode.NoBinding)]
    [Trait("Category", Category)]
    public async Task dpop_should_not_be_able_to_start_on_renewal(ParMode parMode)
    {
        // Initial code flow doesn't use dpop
        var codeRequest = await CreateAuthCodeTokenRequestAsync(omitDPoPProofAtTokenEndpoint: true, parMode: parMode);
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest, _ct);
        codeResponse.IsError.ShouldBeFalse();

        // Subsequent refresh token request tries to use dpop
        var rtRequest = CreateRefreshTokenRequest(codeResponse, omitDPoPProof: false);

        var rtResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest, _ct);
        rtResponse.IsError.ShouldBeTrue();
    }

    [Theory]
    [ClassData(typeof(ParModes))]
    [Trait("Category", Category)]
    public async Task confidential_client_should_be_able_to_use_different_dpop_key_for_refresh_token_request(ParMode parMode)
    {
        var codeRequest = await CreateAuthCodeTokenRequestAsync(parMode: parMode);
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest, _ct);
        codeResponse.ShouldHaveDPoPThumbprint(JKT);

        CreateNewRSAKey();
        var rtRequest = CreateRefreshTokenRequest(codeResponse);

        var rtResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest, _ct);
        rtResponse.ShouldHaveDPoPThumbprint(JKT);
    }

    [Theory]
    [ClassData(typeof(ParModes))]
    [Trait("Category", Category)]
    public async Task public_client_should_not_be_able_to_use_different_dpop_key_for_refresh_token_request(ParMode parMode)
    {
        var codeRequest = await CreateAuthCodeTokenRequestAsync(clientId: "client2", parMode: parMode);
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest, _ct);
        codeResponse.ShouldHaveDPoPThumbprint(JKT);

        CreateNewRSAKey();
        var rtRequest = CreateRefreshTokenRequest(codeResponse, clientId: "client2");

        var rtResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest, _ct);
        rtResponse.IsError.ShouldBeTrue();
        rtResponse.Error.ShouldBe("invalid_dpop_proof");
    }

    [Theory]
    [ClassData(typeof(ParModes))]
    [Trait("Category", Category)]
    public async Task public_client_using_same_dpop_key_for_refresh_token_request_should_succeed(ParMode parMode)
    {
        var codeRequest = await CreateAuthCodeTokenRequestAsync(clientId: "client2", parMode: parMode);
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest, _ct);
        codeResponse.ShouldHaveDPoPThumbprint(JKT);

        var firstRefreshRequest = CreateRefreshTokenRequest(codeResponse, clientId: "client2");
        var firstRefreshResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(firstRefreshRequest, _ct);
        firstRefreshResponse.ShouldHaveDPoPThumbprint(JKT);

        var secondRefreshRequest = CreateRefreshTokenRequest(codeResponse, clientId: "client2");
        var secondRefreshResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(secondRefreshRequest, _ct);
        secondRefreshResponse.ShouldHaveDPoPThumbprint(JKT);
    }


    [Theory]
    [ClassData(typeof(ParModes))]
    [Trait("Category", Category)]
    public async Task missing_proof_token_when_required_on_refresh_token_request_should_fail(ParMode parMode)
    {
        ConfidentialClient.RequireDPoP = true;

        var codeRequest = await CreateAuthCodeTokenRequestAsync(parMode: parMode);
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest, _ct);
        codeResponse.ShouldHaveDPoPThumbprint(JKT);

        var rtRequest = CreateRefreshTokenRequest(codeResponse, omitDPoPProof: true);
        var rtResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest, _ct);
        rtResponse.IsError.ShouldBeTrue();
        rtResponse.Error.ShouldBe("invalid_request");
    }

    [Theory]
    [InlineData(AccessTokenType.Reference)]
    [InlineData(AccessTokenType.Jwt)]
    [Trait("Category", Category)]
    public async Task valid_dpop_request_at_introspection_should_return_binding_information(AccessTokenType accessTokenType)
    {
        ConfidentialClient.AccessTokenType = accessTokenType;
        var codeRequest = await CreateAuthCodeTokenRequestAsync();
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest, _ct);

        var introspectionRequest = new TokenIntrospectionRequest
        {
            Address = IdentityServerPipeline.IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",
            Token = codeResponse.AccessToken,
        };
        var introspectionResponse = await Pipeline.BackChannelClient.IntrospectTokenAsync(introspectionRequest, _ct);
        introspectionResponse.IsError.ShouldBeFalse();
        GetJKTFromCnfClaim(introspectionResponse.Claims).ShouldBe(JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task matching_dpop_key_thumbprint_on_authorize_endpoint_and_token_endpoint_should_succeed()
    {
        var codeRequest = await CreateAuthCodeTokenRequestAsync(dpopJkt: JKT);

        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest, _ct);
        codeResponse.ShouldHaveDPoPThumbprint(JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_key_thumbprint_too_long_should_fail()
    {
        var url = Pipeline.CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "code",
            responseMode: "query",
            scope: "openid scope1 offline_access",
            redirectUri: "https://client1/callback",
            extra: new
            {
                dpop_jkt = new string('x', 101)
            });
        await Pipeline.BrowserClient.GetAsync(url, _ct);

        Pipeline.ErrorWasCalled.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ParMode.Unused)]
    [InlineData(ParMode.DpopJktParameter)]
    [InlineData(ParMode.DpopHeader)]
    [InlineData(ParMode.Both)]
    [Trait("Category", Category)]
    public async Task mismatched_dpop_key_thumbprint_on_authorize_endpoint_and_token_endpoint_should_fail(ParMode parMode)
    {
        var oldJkt = JKT;
        var oldProof = CreateDPoPProofToken(htu: IdentityServerPipeline.ParEndpoint);
        CreateNewRSAKey();
        JKT.ShouldNotBe(oldJkt);
        var codeRequest = await CreateAuthCodeTokenRequestAsync(parMode: parMode, dpopJkt: oldJkt, dpopProof: oldProof);

        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest, _ct);
        codeResponse.IsError.ShouldBeTrue();
        codeResponse.Error.ShouldBe("invalid_dpop_proof");
    }

    [Theory]
    [ClassData(typeof(ParModes))]
    [Trait("Category", Category)]
    public async Task server_issued_nonce_should_be_emitted(ParMode parMode)
    {
        var expectedNonce = "nonce";

        Pipeline.OnPostConfigureServices += services =>
        {
            services.AddSingleton<MockDPoPProofValidator>();
            services.AddSingleton<IDPoPProofValidator>(sp =>
            {
                var mockValidator = sp.GetRequiredService<MockDPoPProofValidator>();
                mockValidator.ServerIssuedNonce = expectedNonce;
                return mockValidator;
            });
        };
        Pipeline.Initialize();

        var codeRequest = await CreateAuthCodeTokenRequestAsync(parMode: parMode, expectedDpopNonce: expectedNonce);

        if (parMode is ParMode.DpopHeader or ParMode.Both)
        {
            return;
        }
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest, _ct);
        codeResponse.IsError.ShouldBeTrue();
        codeResponse.Error.ShouldBe(OidcConstants.TokenErrors.UseDPoPNonce);
        codeResponse.DPoPNonce.ShouldBe(expectedNonce);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task token_request_when_using_mtls_for_client_authentication_should_succeed()
    {
        var clientId = "mtls_client";
        var clientCert = TestCert.Load();
        var client = new Client
        {
            ClientId = clientId,
            ClientSecrets =
            {
                new Secret
                {
                    Type = IdentityServerConstants.SecretTypes.X509CertificateThumbprint,
                    Value = clientCert.Thumbprint
                }
            },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = { "scope1" },
            RequireDPoP = true
        };

        Pipeline.Clients.Add(client);
        Pipeline.Initialize();
        Pipeline.SetClientCertificate(clientCert);

        var tokenClient = Pipeline.GetMtlsClient();
        tokenClient.DefaultRequestHeaders.Add("DPoP", CreateDPoPProofToken(htu: IdentityServerPipeline.TokenMtlsEndpoint));
        var formParams = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", clientId },
            { "scope", "scope1" }
        };

        var form = new FormUrlEncodedContent(formParams);
        var response = await tokenClient.PostAsync(IdentityServerPipeline.TokenMtlsEndpoint, form, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync(_ct);
        json.ShouldContain("access_token");
        json.ShouldContain("\"token_type\":\"DPoP\"");
    }

    internal class MockDPoPProofValidator : DefaultDPoPProofValidator
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
                result.Error = OidcConstants.TokenErrors.UseDPoPNonce;
                return;
            }

            await base.ValidateFreshnessAsync(context, result);
        }
    }

    public enum KeyType { RSA, EC }

    [Theory]
    [InlineData("RS256", KeyType.RSA)]
    [InlineData("RS384", KeyType.RSA)]
    [InlineData("RS512", KeyType.RSA)]
    [InlineData("PS256", KeyType.RSA)]
    [InlineData("PS384", KeyType.RSA)]
    [InlineData("PS512", KeyType.RSA)]
    [InlineData("ES256", KeyType.EC)]
    [InlineData("ES384", KeyType.EC)]
    [InlineData("ES512", KeyType.EC)]
    [Trait("Category", Category)]
    public async Task all_supported_signing_algorithms_should_work(string alg, KeyType keyType)
    {
        if (keyType == KeyType.RSA)
        {
            CreateNewRSAKey();
        }
        else
        {
            CreateNewECKey();
        }

        var proofToken = CreateDPoPProofToken(alg);
        var request = CreateClientCredentialsTokenRequest(proofToken);

        var response = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request, _ct);

        response.IsError.ShouldBeFalse();
        response.TokenType.ShouldBe("DPoP");
        var jkt = GetJKTFromAccessToken(response);
        jkt.ShouldBe(JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task mtls_and_dpop_request_should_succeed()
    {
        var clientId = "mtls_dpop_client";
        var clientCert = TestCert.Load();

        // Add a client that requires mTLS and supports DPoP
        var client = new Client
        {
            ClientId = clientId,
            ClientSecrets =
            {
                new Secret
                {
                    Type = IdentityServerConstants.SecretTypes.X509CertificateThumbprint,
                    Value = clientCert.Thumbprint
                }
            },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = { "scope1" },
            RequireDPoP = true
        };

        Pipeline.Clients.Add(client);
        Pipeline.Initialize();

        // Set the client certificate in the pipeline
        Pipeline.SetClientCertificate(clientCert);

        var tokenClient = Pipeline.GetMtlsClient();
        var formParams = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", clientId },
            { "scope", "scope1" }
        };
        var form = new FormUrlEncodedContent(formParams);
        tokenClient.DefaultRequestHeaders.Add("DPoP", CreateDPoPProofToken(htu: IdentityServerPipeline.TokenMtlsEndpoint));

        var response = await tokenClient.PostAsync(IdentityServerPipeline.TokenMtlsEndpoint, form, _ct);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync(_ct);
        json.ShouldContain("access_token");
        json.ShouldContain("\"token_type\":\"DPoP\"");
    }
}

public class ParModes : TheoryData<ParMode>
{
    public ParModes()
    {
        Add(ParMode.Unused);
        Add(ParMode.NoBinding);
        Add(ParMode.DpopHeader);
        Add(ParMode.DpopJktParameter);
        Add(ParMode.Both);
    }
}
