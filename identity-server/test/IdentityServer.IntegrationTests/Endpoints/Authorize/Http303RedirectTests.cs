// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Authorize;

/// <summary>
/// Tests for HTTP 303 redirect behavior per FAPI 2.0 Security Profile.
/// When UseHttp303Redirects is enabled, the server should use HTTP 303 (See Other)
/// instead of HTTP 302 (Found) for authorization redirects.
/// </summary>
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
    }

    private void InitializeWithHttp303(bool enableHttp303)
    {
        _pipeline.OnPostConfigureServices += services => { };
        _pipeline.OnPreConfigure += app => { };
        _pipeline.OnPostConfigure += app => { };
        _pipeline.Initialize();
        _pipeline.Options.UserInteraction.UseHttp303Redirects = enableHttp303;
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Authorize_WithHttp303Disabled_Returns302Redirect()
    {
        InitializeWithHttp303(enableHttp303: false);

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

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldStartWith("https://client/callback");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Authorize_WithHttp303Enabled_Returns303Redirect()
    {
        InitializeWithHttp303(enableHttp303: true);

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
    public async Task Authorize_WithHttp303Enabled_ImplicitFlow_Returns303Redirect()
    {
        InitializeWithHttp303(enableHttp303: true);

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
    public async Task AuthorizeRedirectToLogin_WithHttp303Disabled_Returns302Redirect()
    {
        InitializeWithHttp303(enableHttp303: false);

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

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldContain("/account/login");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task AuthorizeRedirectToLogin_WithHttp303Enabled_Returns303Redirect()
    {
        InitializeWithHttp303(enableHttp303: true);

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
    public async Task Authorize_ErrorRedirectToErrorPage_WithHttp303Enabled_Returns303Redirect()
    {
        InitializeWithHttp303(enableHttp303: true);

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
        var location = response.Headers.Location!.ToString();
        location.ShouldContain("/home/error");
    }
}
