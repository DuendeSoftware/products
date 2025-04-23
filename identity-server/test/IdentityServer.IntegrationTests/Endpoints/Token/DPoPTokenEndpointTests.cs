// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using Duende.IdentityModel.Client;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntegrationTests.Endpoints.Token;

public class DPoPTokenEndpointTests : DPoPEndpointTestBase
{
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

    private async Task<AuthorizationCodeTokenRequest> CreateAuthCodeTokenRequestAsync(
        string clientId = "client1", bool omitDPoPProof = false, string dpopJkt = null)
    {
        await Pipeline.LoginAsync("bob");

        Pipeline.BrowserClient.AllowAutoRedirect = false;

        var scope = clientId == "client1" ? "scope1" : "scope2";

        var url = Pipeline.CreateAuthorizeUrl(
            clientId: clientId,
            responseType: "code",
            responseMode: "query",
            scope: $"openid {scope} offline_access",
            redirectUri: $"https://{clientId}/callback",
            extra: dpopJkt != null ? new
            {
                dpop_jkt = dpopJkt
            } : null);
        var response = await Pipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith($"https://{clientId}/callback");

        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();

        var codeRequest = new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = clientId,
            ClientSecret = "secret",
            Code = authorization.Code,
            RedirectUri = $"https://{clientId}/callback",
        };
        if (!omitDPoPProof)
        {
            codeRequest.Headers.Add("DPoP", CreateDPoPProofToken());
        }
        return codeRequest;
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

        var response = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);

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

        var response = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);

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

        var response = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);

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
        var firstResponse = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(firstRequest);
        firstResponse.IsError.ShouldBeFalse();
        firstResponse.TokenType.ShouldBe("DPoP");
        var jkt = GetJKTFromAccessToken(firstResponse);
        jkt.ShouldBe(JKT);

        // Second request fails
        var secondRequest = CreateClientCredentialsTokenRequest(dpopToken);
        secondRequest.Headers.Add("DPoP", dpopToken);
        var secondResponse = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(secondRequest);
        secondResponse.IsError.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_dpop_request_should_fail()
    {
        var request = CreateClientCredentialsTokenRequest(proofToken: "malformed");

        var response = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);

        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("invalid_dpop_proof");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_dpop_token_when_required_should_fail()
    {
        ConfidentialClient.RequireDPoP = true;
        var request = CreateClientCredentialsTokenRequest(omitDPoPProof: true);

        var response = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);

        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("invalid_dpop_proof");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task multiple_dpop_tokens_should_fail()
    {
        var request = CreateClientCredentialsTokenRequest(omitDPoPProof: true);
        var dpopToken = CreateDPoPProofToken();
        request.Headers.Add("DPoP", dpopToken);
        request.Headers.Add("DPoP", dpopToken);

        var response = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);

        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("invalid_dpop_proof");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_dpop_request_should_return_bound_refresh_token()
    {
        var codeRequest = await CreateAuthCodeTokenRequestAsync();
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.ShouldHaveDPoPThumbprint(JKT);

        var rtRequest = CreateRefreshTokenRequest(codeResponse);
        var rtResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest);
        rtResponse.ShouldHaveDPoPThumbprint(JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task confidential_client_dpop_proof_should_be_required_on_renewal()
    {
        var codeRequest = await CreateAuthCodeTokenRequestAsync();
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.ShouldHaveDPoPThumbprint(JKT);

        var rtRequest = CreateRefreshTokenRequest(codeResponse, omitDPoPProof: true);
        var rtResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest);
        rtResponse.IsError.ShouldBeTrue();
        rtResponse.Error.ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task public_client_dpop_proof_should_be_required_on_renewal()
    {
        var codeRequest = await CreateAuthCodeTokenRequestAsync(clientId: "client2");
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.ShouldHaveDPoPThumbprint(JKT);

        var rtRequest = CreateRefreshTokenRequest(codeResponse, clientId: "client2", omitDPoPProof: true);
        var rtResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest);
        rtResponse.IsError.ShouldBeTrue();
        rtResponse.Error.ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_should_not_be_able_to_start_on_renewal()
    {
        // Initial code flow doesn't use dpop
        var codeRequest = await CreateAuthCodeTokenRequestAsync(omitDPoPProof: true);
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.IsError.ShouldBeFalse();

        // Subsequent refresh token request tries to use dpop
        var rtRequest = CreateRefreshTokenRequest(codeResponse, omitDPoPProof: false);

        var rtResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest);
        rtResponse.IsError.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task confidential_client_should_be_able_to_use_different_dpop_key_for_refresh_token_request()
    {
        var codeRequest = await CreateAuthCodeTokenRequestAsync();
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.ShouldHaveDPoPThumbprint(JKT);

        CreateNewRSAKey();
        var rtRequest = CreateRefreshTokenRequest(codeResponse);

        var rtResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest);
        rtResponse.ShouldHaveDPoPThumbprint(JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task public_client_should_not_be_able_to_use_different_dpop_key_for_refresh_token_request()
    {
        var codeRequest = await CreateAuthCodeTokenRequestAsync(clientId: "client2");
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.ShouldHaveDPoPThumbprint(JKT);

        CreateNewRSAKey();
        var rtRequest = CreateRefreshTokenRequest(codeResponse, clientId: "client2");

        var rtResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest);
        rtResponse.IsError.ShouldBeTrue();
        rtResponse.Error.ShouldBe("invalid_dpop_proof");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task public_client_using_same_dpop_key_for_refresh_token_request_should_succeed()
    {
        var codeRequest = await CreateAuthCodeTokenRequestAsync(clientId: "client2");
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.ShouldHaveDPoPThumbprint(JKT);

        var firstRefreshRequest = CreateRefreshTokenRequest(codeResponse, clientId: "client2");
        var firstRefreshResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(firstRefreshRequest);
        firstRefreshResponse.ShouldHaveDPoPThumbprint(JKT);

        var secondRefreshRequest = CreateRefreshTokenRequest(codeResponse, clientId: "client2");
        var secondRefreshResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(secondRefreshRequest);
        secondRefreshResponse.ShouldHaveDPoPThumbprint(JKT);
    }


    [Fact]
    [Trait("Category", Category)]
    public async Task missing_proof_token_when_required_on_refresh_token_request_should_fail()
    {
        ConfidentialClient.RequireDPoP = true;

        var codeRequest = await CreateAuthCodeTokenRequestAsync();
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.ShouldHaveDPoPThumbprint(JKT);

        var rtRequest = CreateRefreshTokenRequest(codeResponse, omitDPoPProof: true);
        var rtResponse = await Pipeline.BackChannelClient.RequestRefreshTokenAsync(rtRequest);
        rtResponse.IsError.ShouldBeTrue();
        rtResponse.Error.ShouldBe("invalid_dpop_proof");
    }


    [Theory]
    [InlineData(AccessTokenType.Reference)]
    [InlineData(AccessTokenType.Jwt)]
    [Trait("Category", Category)]
    public async Task valid_dpop_request_at_introspection_should_return_binding_information(AccessTokenType accessTokenType)
    {
        ConfidentialClient.AccessTokenType = accessTokenType;
        var codeRequest = await CreateAuthCodeTokenRequestAsync();
        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);

        var introspectionRequest = new TokenIntrospectionRequest
        {
            Address = IdentityServerPipeline.IntrospectionEndpoint,
            ClientId = "api1",
            ClientSecret = "secret",
            Token = codeResponse.AccessToken,
        };
        var introspectionResponse = await Pipeline.BackChannelClient.IntrospectTokenAsync(introspectionRequest);
        introspectionResponse.IsError.ShouldBeFalse();
        GetJKTFromCnfClaim(introspectionResponse.Claims).ShouldBe(JKT);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task matching_dpop_key_thumbprint_on_authorize_endpoint_and_token_endpoint_should_succeed()
    {
        var codeRequest = await CreateAuthCodeTokenRequestAsync(dpopJkt: JKT);

        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
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
        await Pipeline.BrowserClient.GetAsync(url);

        Pipeline.ErrorWasCalled.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task mismatched_dpop_key_thumbprint_on_authorize_endpoint_and_token_endpoint_should_fail()
    {
        var codeRequest = await CreateAuthCodeTokenRequestAsync(dpopJkt: "invalid");

        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.IsError.ShouldBeTrue();
        codeResponse.Error.ShouldBe("invalid_dpop_proof");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task server_issued_nonce_should_be_emitted()
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

        var codeRequest = await CreateAuthCodeTokenRequestAsync();

        var codeResponse = await Pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(codeRequest);
        codeResponse.IsError.ShouldBeTrue();
        codeResponse.Error.ShouldBe("invalid_dpop_proof");
        codeResponse.DPoPNonce.ShouldBe(expectedNonce);
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

        var response = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(request);

        response.IsError.ShouldBeFalse();
        response.TokenType.ShouldBe("DPoP");
        var jkt = GetJKTFromAccessToken(response);
        jkt.ShouldBe(JKT);
    }
}
