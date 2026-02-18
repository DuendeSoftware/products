// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Authorize;

public class Http303RedirectTests
{
    private const string Category = "HTTP 303 Redirect Tests";

    private readonly IdentityServerPipeline _pipeline = new();

    public Http303RedirectTests()
    {
        _pipeline.Clients.AddRange(new Client[]
        {
            new()
            {
                ClientId = "code_client",
                ClientName = "Code Flow Client",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = false,
                AllowedScopes = { "openid", "profile" },
                RedirectUris = { "https://client/callback" },
                ClientSecrets = { new Secret("secret".Sha256()) }
            },
            new()
            {
                ClientId = "implicit_client",
                ClientName = "Implicit Flow Client",
                AllowedGrantTypes = GrantTypes.Implicit,
                AllowedScopes = { "openid", "profile" },
                AllowAccessTokensViaBrowser = true,
                RedirectUris = { "https://implicit/callback" }
            }
        });

        _pipeline.IdentityScopes.AddRange(new IdentityResource[]
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile()
        });

        _pipeline.Users.Add(new TestUser
        {
            SubjectId = "bob",
            Username = "bob",
            IsActive = true
        });

        _pipeline.Initialize();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Authorize_Returns303Redirect()
    {
        await _pipeline.LoginAsync("bob");
        _pipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _pipeline.CreateAuthorizeUrl(
            clientId: "code_client",
            responseType: "code",
            scope: "openid",
            redirectUri: "https://client/callback",
            state: "123_state",
            nonce: "123_nonce");

        var response = await _pipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldStartWith("https://client/callback");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Authorize_ImplicitFlow_Returns303Redirect()
    {
        await _pipeline.LoginAsync("bob");
        _pipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _pipeline.CreateAuthorizeUrl(
            clientId: "implicit_client",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://implicit/callback",
            state: "123_state",
            nonce: "123_nonce");

        var response = await _pipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldStartWith("https://implicit/callback");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Authorize_RedirectToLogin_Returns303Redirect()
    {
        // User is not logged in, so authorize should redirect to login page
        _pipeline.BrowserClient.AllowAutoRedirect = false;

        var url = _pipeline.CreateAuthorizeUrl(
            clientId: "code_client",
            responseType: "code",
            scope: "openid",
            redirectUri: "https://client/callback",
            state: "123_state",
            nonce: "123_nonce");

        var response = await _pipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldContain("/account/login");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Authorize_ErrorRedirectToErrorPage_Returns303Redirect()
    {
        await _pipeline.LoginAsync("bob");
        _pipeline.BrowserClient.AllowAutoRedirect = false;

        // Use invalid scope to trigger an unsafe error that redirects to the error page
        var url = _pipeline.CreateAuthorizeUrl(
            clientId: "code_client",
            responseType: "code",
            scope: "openid invalid_scope",
            redirectUri: "https://client/callback",
            state: "123_state",
            nonce: "123_nonce");

        var response = await _pipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldContain("/home/error");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task EndSession_Returns303Redirect()
    {
        await _pipeline.LoginAsync("bob");
        _pipeline.BrowserClient.AllowAutoRedirect = false;

        var response = await _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.EndSessionEndpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldContain("/account/logout");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task EndSession_WithIdTokenHint_Returns303Redirect()
    {
        await _pipeline.LoginAsync("bob");
        _pipeline.BrowserClient.AllowAutoRedirect = false;

        // First get an id_token
        var url = _pipeline.CreateAuthorizeUrl(
            clientId: "implicit_client",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://implicit/callback",
            state: "123_state",
            nonce: "123_nonce");

        var authorizeResponse = await _pipeline.BrowserClient.GetAsync(url);
        var authorization = new Duende.IdentityModel.Client.AuthorizeResponse(authorizeResponse.Headers.Location!.ToString());
        var idToken = authorization.IdentityToken;

        // Now call end session with the id_token_hint
        var response = await _pipeline.BrowserClient.GetAsync(
            IdentityServerPipeline.EndSessionEndpoint + "?id_token_hint=" + idToken);

        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldContain("/account/logout");
    }
}
