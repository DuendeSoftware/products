// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Hosting.FederatedSignOut;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;
using Microsoft.AspNetCore.Authentication;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public sealed class SamlFederatedSignoutTests
{
    private const string Category = "SAML Federated Signout";

    private readonly IdentityServerPipeline _pipeline = new();
    private readonly ClaimsPrincipal _user;

    public SamlFederatedSignoutTests()
    {
        _user = new IdentityServerUser("bob")
        {
            AdditionalClaims = { new Claim(JwtClaimTypes.SessionId, "123") }
        }.CreatePrincipal();

        _pipeline.IdentityScopes.AddRange(new IdentityResource[]
        {
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
            Claims =
            [
                new Claim("name", "Bob Loblaw"),
                new Claim("email", "bob@loblaw.com")
            ]
        });

        _pipeline.Initialize();
    }

    private static SamlSpLogoutContext CreateSamlContext() => new()
    {
        IdpEntityId = "https://upstream-idp.example.com",
        LogoutRequestId = "_req-abc-123",
        RelayState = "some-relay-state",
        ResponseBinding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Post",
        ResponseDestination = "https://upstream-idp.example.com/slo"
    };

    [Fact]
    [Trait("Category", Category)]
    public async Task saml_idp_initiated_logout_renders_page_with_iframe_and_completion_url()
    {
        await _pipeline.LoginAsync(_user);

        await _pipeline.RequestAuthorizationEndpointAsync(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");

        _pipeline.OnFederatedSignout = async ctx =>
        {
            await ctx.SignOutAsync();
            ctx.Items[SamlSpLogoutContext.HttpContextItemsKey] = CreateSamlContext();
            return true;
        };

        var response = await _pipeline.BrowserClient.GetAsync(
            IdentityServerPipeline.FederatedSignOutUrl + "?sid=123");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");

        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("id=\"signout-frame\"");
        html.ShouldContain("data-completion-url=");
        html.ShouldContain("/saml/slo/sp-complete?logoutId=");
        html.ShouldContain("connect/endsession/callback?endSessionId=");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task saml_idp_initiated_logout_with_no_downstream_clients_redirects_to_completion_endpoint()
    {
        // Log in but do NOT authorize any clients — no downstream sessions
        await _pipeline.LoginAsync(_user);

        _pipeline.OnFederatedSignout = async ctx =>
        {
            await ctx.SignOutAsync();
            ctx.Items[SamlSpLogoutContext.HttpContextItemsKey] = CreateSamlContext();
            return true;
        };

        _pipeline.BrowserClient.AllowAutoRedirect = false;
        var response = await _pipeline.BrowserClient.GetAsync(
            IdentityServerPipeline.FederatedSignOutUrl + "?sid=123");

        // When no downstream clients need notification, the wrapper redirects
        // directly to the completion endpoint to send the LogoutResponse immediately.
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldContain("/saml/slo/sp-complete");
        response.Headers.Location!.ToString().ShouldContain("logoutId=");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task saml_idp_initiated_logout_sets_no_cache_headers()
    {
        await _pipeline.LoginAsync(_user);

        await _pipeline.RequestAuthorizationEndpointAsync(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");

        _pipeline.OnFederatedSignout = async ctx =>
        {
            await ctx.SignOutAsync();
            ctx.Items[SamlSpLogoutContext.HttpContextItemsKey] = CreateSamlContext();
            return true;
        };

        var response = await _pipeline.BrowserClient.GetAsync(
            IdentityServerPipeline.FederatedSignOutUrl + "?sid=123");

        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        response.Headers.CacheControl.NoCache.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task saml_idp_initiated_logout_sets_csp_headers()
    {
        await _pipeline.LoginAsync(_user);

        await _pipeline.RequestAuthorizationEndpointAsync(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");

        _pipeline.OnFederatedSignout = async ctx =>
        {
            await ctx.SignOutAsync();
            ctx.Items[SamlSpLogoutContext.HttpContextItemsKey] = CreateSamlContext();
            return true;
        };

        var response = await _pipeline.BrowserClient.GetAsync(
            IdentityServerPipeline.FederatedSignOutUrl + "?sid=123");

        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        csp.ShouldContain("default-src 'none'");
        csp.ShouldContain("script-src");
        csp.ShouldContain("frame-src 'self'");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task saml_idp_initiated_logout_renders_post_message_script()
    {
        await _pipeline.LoginAsync(_user);

        await _pipeline.RequestAuthorizationEndpointAsync(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");

        _pipeline.OnFederatedSignout = async ctx =>
        {
            await ctx.SignOutAsync();
            ctx.Items[SamlSpLogoutContext.HttpContextItemsKey] = CreateSamlContext();
            return true;
        };

        var response = await _pipeline.BrowserClient.GetAsync(
            IdentityServerPipeline.FederatedSignOutUrl + "?sid=123");

        var html = await response.Content.ReadAsStringAsync();
        // The script listens for postMessage from the iframe
        html.ShouldContain("logout-iframes-complete");
        html.ShouldContain("setTimeout(complete,5000)");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task saml_idp_initiated_logout_renders_noscript_fallback()
    {
        await _pipeline.LoginAsync(_user);

        await _pipeline.RequestAuthorizationEndpointAsync(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");

        _pipeline.OnFederatedSignout = async ctx =>
        {
            await ctx.SignOutAsync();
            ctx.Items[SamlSpLogoutContext.HttpContextItemsKey] = CreateSamlContext();
            return true;
        };

        var response = await _pipeline.BrowserClient.GetAsync(
            IdentityServerPipeline.FederatedSignOutUrl + "?sid=123");

        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("<noscript>");
        html.ShouldContain("Click here if not redirected automatically");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task oidc_federated_signout_still_works_when_saml_context_not_present()
    {
        // Verify the existing OIDC path is not broken by our changes
        await _pipeline.LoginAsync(_user);

        await _pipeline.RequestAuthorizationEndpointAsync(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");

        // Default OnFederatedSignout just calls SignOutAsync — no SAML context set
        var response = await _pipeline.BrowserClient.GetAsync(
            IdentityServerPipeline.FederatedSignOutUrl + "?sid=123");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        var html = await response.Content.ReadAsStringAsync();

        // Should render the old-style iframe (not our combined page)
        html.ShouldContain("connect/endsession/callback?endSessionId=");
        html.ShouldNotContain("signout-frame");
        html.ShouldNotContain("/saml/slo/sp-complete");
    }
}
