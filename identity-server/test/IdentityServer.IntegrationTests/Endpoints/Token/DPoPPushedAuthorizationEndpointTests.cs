// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using IntegrationTests.Common;
using IntegrationTests.Endpoints.Token;

namespace IntegrationTests.Endpoints.PushedAuthorization;

public class DPoPPushedAuthorizationEndpointTests : DPoPEndpointTestBase
{
    protected const string Category = "DPoP PAR endpoint";

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_dpop_request_should_fail()
    {
        var request = new PushedAuthorizationRequest
        {
            Address = IdentityServerPipeline.ParEndpoint,
            ClientId = "client1",
            ClientSecret = "secret",
            Scope = "scope1",
            ResponseType = OidcConstants.ResponseTypes.Code
        };
        request.Headers.Add("DPoP", "malformed");

        var response = await Pipeline.BackChannelClient.PushAuthorizationAsync(request);
        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("invalid_request");
    }

    [Fact]
    public async Task multiple_dpop_headers_sent_to_the_par_endpoint_fails()
    {
        Pipeline.BackChannelClient.DefaultRequestHeaders.Add("DPoP", "first");
        Pipeline.BackChannelClient.DefaultRequestHeaders.Add("DPoP", "second");
        var (parJson, statusCode) = await Pipeline.PushAuthorizationRequestAsync();
        statusCode.ShouldBe(HttpStatusCode.BadRequest);
        parJson.RootElement.GetProperty("error").GetString()
            .ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }
}
