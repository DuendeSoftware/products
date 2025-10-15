// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;
using Microsoft.AspNetCore.Authentication;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public class FederatedSignoutTests
{
    private const string Category = "Federated Signout";
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly IdentityServerPipeline _pipeline = new IdentityServerPipeline();
    private readonly ClaimsPrincipal _user;

    public FederatedSignoutTests()
    {
        _user = new IdentityServerUser("bob")
        {
            AdditionalClaims = { new Claim(JwtClaimTypes.SessionId, "123") }
        }.CreatePrincipal();

        _pipeline = new IdentityServerPipeline();

        _pipeline.IdentityScopes.AddRange(new IdentityResource[] {
            new IdentityResources.OpenId()
        });

        _pipeline.Clients.Add(new Client
        {
            ClientId = "client1",
            AllowedGrantTypes = GrantTypes.Implicit,
            RequireConsent = false,
            AllowedScopes = new List<string> { "openid" },
            RedirectUris = new List<string> { "https://client1/callback" },
            FrontChannelLogoutUri = "https://client1/signout",
            PostLogoutRedirectUris = new List<string> { "https://client1/signout-callback" },
            AllowAccessTokensViaBrowser = true
        });

        _pipeline.Users.Add(new TestUser
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

        _pipeline.Initialize();
    }

    [Fact]
    public async Task valid_request_to_federated_signout_endpoint_should_render_page_with_iframe()
    {
        await _pipeline.LoginAsync(_user);

        await _pipeline.RequestAuthorizationEndpointAsync(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");

        var response = await _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.FederatedSignOutUrl + "?sid=123", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.ShouldBe("text/html");
        var html = await response.Content.ReadAsStringAsync(_ct);
        html.ShouldContain("https://server/connect/endsession/callback?endSessionId=");
    }

    [Fact]
    public async Task valid_POST_request_to_federated_signout_endpoint_should_render_page_with_iframe()
    {
        await _pipeline.LoginAsync(_user);

        await _pipeline.RequestAuthorizationEndpointAsync(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");

        var response = await _pipeline.BrowserClient.PostAsync(IdentityServerPipeline.FederatedSignOutUrl, new FormUrlEncodedContent(new Dictionary<string, string> { { "sid", "123" } }), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.ShouldBe("text/html");
        var html = await response.Content.ReadAsStringAsync(_ct);
        html.ShouldContain("https://server/connect/endsession/callback?endSessionId=");
    }

    [Fact]
    public async Task no_clients_signed_into_should_not_render_page_with_iframe()
    {
        await _pipeline.LoginAsync(_user);

        var response = await _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.FederatedSignOutUrl + "?sid=123", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.ShouldBeNull();
        var html = await response.Content.ReadAsStringAsync(_ct);
        html.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task no_authenticated_user_should_not_render_page_with_iframe()
    {
        var response = await _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.FederatedSignOutUrl + "?sid=123", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.ShouldBeNull();
        var html = await response.Content.ReadAsStringAsync(_ct);
        html.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task user_not_signed_out_should_not_render_page_with_iframe()
    {
        _pipeline.OnFederatedSignout = ctx =>
        {
            return Task.FromResult(true);
        };

        await _pipeline.LoginAsync(_user);

        await _pipeline.RequestAuthorizationEndpointAsync(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");

        var response = await _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.FederatedSignOutUrl + "?sid=123", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.ShouldBeNull();
        var html = await response.Content.ReadAsStringAsync(_ct);
        html.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task non_200_should_not_render_page_with_iframe()
    {
        _pipeline.OnFederatedSignout = async ctx =>
        {
            await ctx.SignOutAsync(); // even if we signout, we should not see iframes
            ctx.Response.Redirect("http://foo");
            return true;
        };

        await _pipeline.LoginAsync(_user);

        await _pipeline.RequestAuthorizationEndpointAsync(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");

        _pipeline.BrowserClient.AllowAutoRedirect = false;
        var response = await _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.FederatedSignOutUrl + "?sid=123", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Content.Headers.ContentType.ShouldBeNull();
        var html = await response.Content.ReadAsStringAsync(_ct);
        html.ShouldBe(string.Empty);
    }
}
