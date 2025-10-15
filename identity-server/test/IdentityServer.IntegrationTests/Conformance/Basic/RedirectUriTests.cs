// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using System.Security.Claims;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;

namespace Duende.IdentityServer.IntegrationTests.Conformance.Basic;

public class RedirectUriTests
{
    private const string Category = "Conformance.Basic.RedirectUriTests";

    private IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();

    public RedirectUriTests()
    {
        _mockPipeline.Initialize();

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
                "https://code_client/callback",
                "https://code_client/callback?foo=bar&baz=quux"
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
    public async Task Reject_redirect_uri_not_matching_registered_redirect_uri()
    {
        await _mockPipeline.LoginAsync("bob");

        var nonce = Guid.NewGuid().ToString();
        var state = Guid.NewGuid().ToString();

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "code_client",
            responseType: "code",
            scope: "openid",
            redirectUri: "https://bad",
            state: state,
            nonce: nonce);
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Reject_request_without_redirect_uri_when_multiple_registered()
    {
        await _mockPipeline.LoginAsync("bob");

        var nonce = Guid.NewGuid().ToString();
        var state = Guid.NewGuid().ToString();

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "code_client",
            responseType: "code",
            scope: "openid",
            // redirectUri deliberately absent 
            redirectUri: null,
            state: state,
            nonce: nonce);
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe("invalid_request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Preserves_query_parameters_in_redirect_uri()
    {
        await _mockPipeline.LoginAsync("bob");

        var nonce = Guid.NewGuid().ToString();
        var state = Guid.NewGuid().ToString();

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;
        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "code_client",
            responseType: "code",
            scope: "openid",
            redirectUri: "https://code_client/callback?foo=bar&baz=quux",
            state: state,
            nonce: nonce);
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://code_client/callback?");
        var authorization = _mockPipeline.ParseAuthorizationResponseUrl(response.Headers.Location.ToString());
        authorization.Code.ShouldNotBeNull();
        authorization.State.ShouldBe(state);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(response.Headers.Location.Query);
        query["foo"].ToString().ShouldBe("bar");
        query["baz"].ToString().ShouldBe("quux");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Rejects_redirect_uri_when_query_parameter_does_not_match()
    {
        await _mockPipeline.LoginAsync("bob");

        var nonce = Guid.NewGuid().ToString();
        var state = Guid.NewGuid().ToString();

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "code_client",
            responseType: "code",
            scope: "openid",
            redirectUri: "https://code_client/callback?baz=quux&foo=bar",
            state: state,
            nonce: nonce);
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.ErrorWasCalled.ShouldBeTrue();
        _mockPipeline.ErrorMessage.Error.ShouldBe("invalid_request");
    }
}
