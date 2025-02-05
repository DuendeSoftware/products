// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;
using Shouldly;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using IntegrationTests.Common;
using Xunit;

namespace IntegrationTests.Conformance.Basic;

public class ResponseTypeResponseModeTests
{
    private const string Category = "Conformance.Basic.ResponseTypeResponseModeTests";

    private IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();

    public ResponseTypeResponseModeTests()
    {
        _mockPipeline.Initialize();
        _mockPipeline.BrowserClient.AllowAutoRedirect = false;
        _mockPipeline.Clients.Add(new Client
        {
            Enabled = true,
            ClientId = "code_client",
            ClientSecrets = new List<Secret>
            {
                new Secret("secret".Sha512())
            },

            AllowedGrantTypes = GrantTypes.Code,
            AllowedScopes = { "openid" },

            RequireConsent = false,
            RequirePkce = false,
            RedirectUris = new List<string>
            {
                "https://code_client/callback"
            }
        });

        _mockPipeline.IdentityScopes.Add(new IdentityResources.OpenId());

        _mockPipeline.Users.Add(new TestUser
        {
            SubjectId = "bob",
            Username = "bob",
            Claims = new Claim[]
            {
                new Claim("name", "Bob Loblaw"),
                new Claim("email", "bob@loblaw.com"),
                new Claim("role", "Attorney")
            }
        });
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Request_with_response_type_code_supported()
    {
        await _mockPipeline.LoginAsync("bob");

        var metadata = await _mockPipeline.BackChannelClient.GetAsync(IdentityServerPipeline.DiscoveryEndpoint);
        metadata.StatusCode.ShouldBe(HttpStatusCode.OK);

        var state = Guid.NewGuid().ToString();
        var nonce = Guid.NewGuid().ToString();

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "code_client",
            responseType: "code",
            scope: "openid",
            redirectUri: "https://code_client/callback",
            state: state,
            nonce: nonce);
        var response = await _mockPipeline.BrowserClient.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.Found);

        var authorization = new AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();
        authorization.Code.ShouldNotBeNull();
        authorization.State.ShouldBe(state);
    }

    // this might not be in sync with the actual conformance tests
    // since we dead-end on the error page due to changes 
    // to follow the RFC to address open redirect in original OAuth RFC
    [Fact]
    [Trait("Category", Category)]
    public async Task Request_missing_response_type_rejected()
    {
        await _mockPipeline.LoginAsync("bob");

        var state = Guid.NewGuid().ToString();
        var nonce = Guid.NewGuid().ToString();
        var values = new Parameters
        {
            { OidcConstants.AuthorizeRequest.ClientId, "code_client" },
            { OidcConstants.AuthorizeRequest.ResponseType, null }, // missing
            { OidcConstants.AuthorizeRequest.RedirectUri, "https://code_client/callback" },
            { OidcConstants.AuthorizeRequest.Scope, "openid" },
            { OidcConstants.AuthorizeRequest.State, state },
            { OidcConstants.AuthorizeRequest.Nonce, nonce }
        };
        var request = new RequestUrl(IdentityServerPipeline.AuthorizeEndpoint);
        var url = request.Create(values);
        
        _mockPipeline.BrowserClient.AllowAutoRedirect = true;
        var _ = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorMessage.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }
}