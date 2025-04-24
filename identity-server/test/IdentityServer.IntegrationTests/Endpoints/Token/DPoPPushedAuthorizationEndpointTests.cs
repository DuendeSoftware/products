// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using IntegrationTests.Common;
using IntegrationTests.Endpoints.Token;

namespace IntegrationTests.Endpoints.PushedAuthorization;

/// <summary>
/// This class contains tests of the DPoP support at the PAR endpoint, exercising behavior that is specific to PAR.
/// Many more tests in DPoPTokenEndpointTests exercise the code flow and are parameterized to test both with and without
/// pushed authorization.
/// </summary>
public class DPoPPushedAuthorizationEndpointTests : DPoPEndpointTestBase
{
    protected const string Category = "DPoP PAR endpoint";

    private PushedAuthorizationRequest CreatePushedAuthorizationRequest(
        string proofToken = null, bool omitDPoPProof = false, string dpopKeyThumprint = null
    )
    {
        var request = new PushedAuthorizationRequest
        {
            Address = IdentityServerPipeline.ParEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "scope1",
            ResponseType = OidcConstants.ResponseTypes.Code,
            RedirectUri = "https://client1/callback",
            DPoPKeyThumbprint = dpopKeyThumprint
        };
        if (!omitDPoPProof)
        {
            proofToken ??= CreateDPoPProofToken(htu: IdentityServerPipeline.ParEndpoint);
            request.Headers.Add("DPoP", proofToken);
        }
        return request;
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_proof_token_too_long_should_fail()
    {
        Payload.Add("foo", new string('x', 3000));
        var request = CreatePushedAuthorizationRequest();
        var response = await Pipeline.BackChannelClient.PushAuthorizationAsync(request);
        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("invalid_dpop_proof");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task replayed_dpop_token_should_fail()
    {
        // Shared proof used throughout
        var dpopToken = CreateDPoPProofToken(htu: IdentityServerPipeline.ParEndpoint);

        // Initial request succeeds
        var firstRequest = CreatePushedAuthorizationRequest(dpopToken);
        var firstResponse = await Pipeline.BackChannelClient.PushAuthorizationAsync(firstRequest);
        firstResponse.IsError.ShouldBeFalse();

        // Second request fails
        var secondRequest = CreatePushedAuthorizationRequest(dpopToken);
        var secondResponse = await Pipeline.BackChannelClient.PushAuthorizationAsync(secondRequest);
        secondResponse.IsError.ShouldBeTrue();
        secondResponse.Error.ShouldBe("invalid_dpop_proof");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_dpop_request_should_fail()
    {
        var request = CreatePushedAuthorizationRequest(proofToken: "malformed");
        var response = await Pipeline.BackChannelClient.PushAuthorizationAsync(request);
        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("invalid_dpop_proof");
    }

    [Fact]
    public async Task multiple_dpop_headers_should_fail()
    {
        var request = CreatePushedAuthorizationRequest(omitDPoPProof: true);
        var dpopToken = CreateDPoPProofToken();
        request.Headers.Add("DPoP", dpopToken);
        request.Headers.Add("DPoP", dpopToken);

        var response = await Pipeline.BackChannelClient.PushAuthorizationAsync(request);

        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    public async Task mismatch_between_header_and_thumbprint_should_fail()
    {
        var oldThumbprint = JKT;
        CreateNewRSAKey();
        oldThumbprint.ShouldNotBe(JKT);
        var request = CreatePushedAuthorizationRequest(dpopKeyThumprint: oldThumbprint);

        var response = await Pipeline.BackChannelClient.PushAuthorizationAsync(request);

        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }
}
