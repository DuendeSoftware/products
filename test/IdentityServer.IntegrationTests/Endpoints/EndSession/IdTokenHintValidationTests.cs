// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Test;
using FluentAssertions;
using IdentityModel;
using IdentityModel.Client;
using IntegrationTests.Common;
using Xunit;

namespace IntegrationTests.Endpoints.EndSession;

/// <summary>
/// Only identity tokens are accepted as an id_token_hint at the end session endpoint. Other
/// tokens, and tokens that don't identify a user, are ignored.
/// </summary>
public sealed class IdTokenHintValidationTests
{
    private const string Category = "End session endpoint";

    private readonly IdentityServerPipeline _mockPipeline = new();

    public IdTokenHintValidationTests()
    {
        _mockPipeline.Clients.Add(new Client
        {
            ClientId = "rp",
            AllowedGrantTypes = GrantTypes.Implicit,
            RequireConsent = false,
            AllowedScopes = new List<string> { "openid" },
            RedirectUris = new List<string> { "https://rp/callback" },
            PostLogoutRedirectUris = new List<string> { "https://rp/signout-callback" },
            AllowAccessTokensViaBrowser = true,
            AlwaysIncludeUserClaimsInIdToken = true
        });

        _mockPipeline.Clients.Add(new Client
        {
            ClientId = "client_a",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = new List<string> { "api2" },
            PostLogoutRedirectUris = new List<string> { "https://client_a/signout-callback" },
        });

        _mockPipeline.Clients.Add(new Client
        {
            ClientId = "m2m",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = new List<string> { "api1" },
        });

        _mockPipeline.Clients.Add(new Client
        {
            ClientId = "ro",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
            AllowedScopes = new List<string> { "api1" },
        });

        // An API resource whose name happens to equal the client id "rp": its access tokens carry aud=rp.
        _mockPipeline.ApiResources.Add(new ApiResource("rp") { Scopes = { "api1" } });
        _mockPipeline.ApiScopes.Add(new ApiScope("api1"));
        _mockPipeline.ApiScopes.Add(new ApiScope("api2"));

        _mockPipeline.Users.Add(new TestUser
        {
            SubjectId = "bob",
            Username = "bob",
            Password = "bob",
            Claims = new Claim[]
            {
                new Claim("name", "Bob"),
                new Claim("events", "calendar")
            }
        });

        _mockPipeline.IdentityScopes.Add(new IdentityResources.OpenId());
        _mockPipeline.IdentityScopes.Add(new IdentityResource(
            name: "custom_events",
            userClaims: new[] { "events" }));

        _mockPipeline.Clients.Single(c => c.ClientId == "rp").AllowedScopes.Add("custom_events");

        _mockPipeline.Initialize();
    }

    private async Task EndSessionAsync(string hint, string postLogoutRedirectUri = "https://rp/signout-callback")
    {
        await _mockPipeline.LoginAsync("bob");
        _mockPipeline.LogoutRequest = null;

        var url = IdentityServerPipeline.EndSessionEndpoint +
                  "?id_token_hint=" + hint +
                  "&post_logout_redirect_uri=" + postLogoutRedirectUri;
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.LogoutWasCalled.Should().BeTrue();
        _mockPipeline.LogoutRequest.Should().NotBeNull();
    }

    // Does not sign a user in first: unlike EndSessionAsync, this exercises the end session
    // endpoint for an anonymous caller, so the id_token_hint's sub/sid can't be matched against
    // any current session.
    private async Task EndSessionAnonymouslyAsync(string hint, string postLogoutRedirectUri = "https://rp/signout-callback")
    {
        _mockPipeline.LogoutRequest = null;

        var url = IdentityServerPipeline.EndSessionEndpoint +
                  "?id_token_hint=" + hint +
                  "&post_logout_redirect_uri=" + postLogoutRedirectUri;
        await _mockPipeline.BrowserClient.GetAsync(url);

        _mockPipeline.LogoutWasCalled.Should().BeTrue();
        _mockPipeline.LogoutRequest.Should().NotBeNull();
    }

    private async Task<string> GetBobsAccessTokenWithAudRpAsync()
    {
        var response = await _mockPipeline.BackChannelClient.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "ro",
            ClientSecret = "secret",
            UserName = "bob",
            Password = "bob",
            Scope = "api1",
        });
        response.IsError.Should().BeFalse(response.Error);
        return response.AccessToken;
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_id_token_hint_is_honoured()
    {
        await _mockPipeline.LoginAsync("bob");

        var authorization = await _mockPipeline.RequestAuthorizationEndpointAsync(
            clientId: "rp",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://rp/callback",
            state: "s",
            nonce: "n");

        await EndSessionAsync(authorization.IdentityToken);

        _mockPipeline.LogoutRequest.ClientId.Should().Be("rp");
        _mockPipeline.LogoutRequest.PostLogoutRedirectUri.Should().Be("https://rp/signout-callback");
        _mockPipeline.LogoutRequest.ShowSignoutPrompt.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task malformed_id_token_hint_is_ignored()
    {
        await EndSessionAsync("not.a.jwt");

        _mockPipeline.LogoutRequest.ClientId.Should().BeNull();
        _mockPipeline.LogoutRequest.PostLogoutRedirectUri.Should().BeNull();
        _mockPipeline.LogoutRequest.ShowSignoutPrompt.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task expired_id_token_hint_is_honoured()
    {
        // End session validates the id_token_hint without checking its lifetime, so an expired
        // real ID token for the client must still be honoured. A negative lifetime produces an
        // already-expired token without needing to advance the pipeline's clock.
        var tools = _mockPipeline.Resolve<IIdentityServerTools>();
        var expiredIdToken = await tools.IssueJwtAsync(
            lifetime: -600,
            issuer: IdentityServerPipeline.BaseUrl,
            tokenType: IdentityServerConstants.TokenTypes.IdentityToken,
            claims: new[]
            {
                new Claim(JwtClaimTypes.Audience, "rp"),
                new Claim(JwtClaimTypes.Subject, "bob"),
            });

        await EndSessionAsync(expiredIdToken);

        _mockPipeline.LogoutRequest.ClientId.Should().Be("rp");
        _mockPipeline.LogoutRequest.PostLogoutRedirectUri.Should().Be("https://rp/signout-callback");
        _mockPipeline.LogoutRequest.ShowSignoutPrompt.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task id_token_hint_with_access_token_type_is_ignored()
    {
        var accessToken = await GetBobsAccessTokenWithAudRpAsync();

        await EndSessionAsync(accessToken);

        _mockPipeline.LogoutRequest.ClientId.Should().BeNull();
        _mockPipeline.LogoutRequest.PostLogoutRedirectUri.Should().BeNull();
        _mockPipeline.LogoutRequest.ShowSignoutPrompt.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task id_token_hint_with_logout_token_type_is_ignored()
    {
        var tools = _mockPipeline.Resolve<IIdentityServerTools>();
        var logoutToken = await tools.IssueJwtAsync(
            lifetime: 300,
            issuer: IdentityServerPipeline.BaseUrl,
            tokenType: IdentityServerConstants.TokenTypes.LogoutToken,
            claims: new[]
            {
                new Claim(JwtClaimTypes.Audience, "rp"),
                new Claim(JwtClaimTypes.Subject, "bob"),
                new Claim(JwtClaimTypes.Events, "{\"http://schemas.openid.net/event/backchannel-logout\":{}}", IdentityServerConstants.ClaimValueTypes.Json),
            });
        logoutToken.Should().NotBeNullOrEmpty();

        await EndSessionAsync(logoutToken);

        _mockPipeline.LogoutRequest.ClientId.Should().BeNull();
        _mockPipeline.LogoutRequest.PostLogoutRedirectUri.Should().BeNull();
        _mockPipeline.LogoutRequest.ShowSignoutPrompt.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task id_token_hint_with_client_token_is_ignored()
    {
        _mockPipeline.Options.IssuerUri = IdentityServerPipeline.BaseUrl;

        var tools = _mockPipeline.Resolve<IIdentityServerTools>();
        var clientJwt = await tools.IssueClientJwtAsync(
            clientId: "m2m",
            lifetime: 300,
            scopes: new[] { "api1" },
            audiences: new[] { "rp" });
        clientJwt.Should().NotBeNullOrEmpty();

        await EndSessionAsync(clientJwt);

        _mockPipeline.LogoutRequest.ClientId.Should().BeNull();
        _mockPipeline.LogoutRequest.PostLogoutRedirectUri.Should().BeNull();
        _mockPipeline.LogoutRequest.ShowSignoutPrompt.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task id_token_hint_without_subject_is_ignored()
    {
        var tools = _mockPipeline.Resolve<IIdentityServerTools>();
        var idTokenWithoutSubject = await tools.IssueJwtAsync(
            lifetime: 300,
            issuer: IdentityServerPipeline.BaseUrl,
            tokenType: IdentityServerConstants.TokenTypes.IdentityToken,
            claims: new[]
            {
                new Claim(JwtClaimTypes.Audience, "rp"),
            });
        idTokenWithoutSubject.Should().NotBeNullOrEmpty();

        await EndSessionAsync(idTokenWithoutSubject);

        _mockPipeline.LogoutRequest.ClientId.Should().BeNull();
        _mockPipeline.LogoutRequest.PostLogoutRedirectUri.Should().BeNull();
        _mockPipeline.LogoutRequest.ShowSignoutPrompt.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task id_token_hint_with_custom_identity_token_type_is_honoured_when_configured()
    {
        _mockPipeline.Options.IssuerUri = IdentityServerPipeline.BaseUrl;
        _mockPipeline.Options.IdentityTokenJwtTypes.Add("id_token+jwt");

        var keyMaterialService = _mockPipeline.Resolve<IKeyMaterialService>();
        var signingCredentials = await keyMaterialService.GetSigningCredentialsAsync(null);

        var header = new JwtHeader(signingCredentials);
        header["typ"] = "id_token+jwt";

        var now = DateTime.UtcNow;
        var payload = new JwtPayload(
            issuer: IdentityServerPipeline.BaseUrl,
            audience: "rp",
            claims: new[] { new Claim(JwtClaimTypes.Subject, "bob") },
            notBefore: now,
            expires: now.AddMinutes(10),
            issuedAt: now);

        var token = new JwtSecurityToken(header, payload);
        var idToken = new JwtSecurityTokenHandler().WriteToken(token);

        await EndSessionAsync(idToken);

        _mockPipeline.LogoutRequest.ClientId.Should().Be("rp");
        _mockPipeline.LogoutRequest.PostLogoutRedirectUri.Should().Be("https://rp/signout-callback");
        _mockPipeline.LogoutRequest.ShowSignoutPrompt.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task id_token_hint_with_access_token_shape_is_ignored_when_access_token_jwt_type_is_empty()
    {
        _mockPipeline.Options.IssuerUri = IdentityServerPipeline.BaseUrl;
        _mockPipeline.Options.AccessTokenJwtType = "";

        var accessToken = await GetBobsAccessTokenWithAudRpAsync();

        await EndSessionAsync(accessToken);

        _mockPipeline.LogoutRequest.ClientId.Should().BeNull();
        _mockPipeline.LogoutRequest.PostLogoutRedirectUri.Should().BeNull();
        _mockPipeline.LogoutRequest.ShowSignoutPrompt.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task id_token_hint_with_logout_token_shape_typed_JWT_is_ignored()
    {
        _mockPipeline.Options.IssuerUri = IdentityServerPipeline.BaseUrl;
        _mockPipeline.Options.LogoutTokenJwtType = "";

        var keyMaterialService = _mockPipeline.Resolve<IKeyMaterialService>();
        var signingCredentials = await keyMaterialService.GetSigningCredentialsAsync(null);

        var header = new JwtHeader(signingCredentials);
        header["typ"] = "JWT";

        var now2 = DateTime.UtcNow;
        var payload2 = new JwtPayload(
            issuer: IdentityServerPipeline.BaseUrl,
            audience: "rp",
            claims: new[]
            {
                new Claim(JwtClaimTypes.Subject, "bob"),
                new Claim(JwtClaimTypes.Events, "{\"http://schemas.openid.net/event/backchannel-logout\":{}}", IdentityServerConstants.ClaimValueTypes.Json),
            },
            notBefore: now2,
            expires: now2.AddMinutes(10),
            issuedAt: now2);

        var token2 = new JwtSecurityToken(header, payload2);
        var logoutShapedToken = new JwtSecurityTokenHandler().WriteToken(token2);

        await EndSessionAsync(logoutShapedToken);

        _mockPipeline.LogoutRequest.ClientId.Should().BeNull();
        _mockPipeline.LogoutRequest.PostLogoutRedirectUri.Should().BeNull();
        _mockPipeline.LogoutRequest.ShowSignoutPrompt.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task id_token_with_events_user_claim_is_honoured()
    {
        await _mockPipeline.LoginAsync("bob");

        var authorization = await _mockPipeline.RequestAuthorizationEndpointAsync(
            clientId: "rp",
            responseType: "id_token",
            scope: "openid custom_events",
            redirectUri: "https://rp/callback",
            state: "s",
            nonce: "n");

        authorization.IdentityToken.Should().NotBeNullOrEmpty();
        var payload = new JwtSecurityTokenHandler().ReadJwtToken(authorization.IdentityToken).Payload;
        payload.ContainsKey("events").Should().BeTrue();

        await EndSessionAsync(authorization.IdentityToken);

        _mockPipeline.LogoutRequest.ClientId.Should().Be("rp");
        _mockPipeline.LogoutRequest.PostLogoutRedirectUri.Should().Be("https://rp/signout-callback");
        _mockPipeline.LogoutRequest.ShowSignoutPrompt.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task anonymous_caller_with_valid_id_token_hint_is_honoured()
    {
        // Obtain a real ID token for rp while bob is signed in, then sign bob out before calling
        // the end session endpoint, so the hint is presented with nobody signed in. Options are
        // default here, so RequireAuthenticatedUserForSignOutMessage is false and the endpoint
        // still processes the request.
        await _mockPipeline.LoginAsync("bob");

        var authorization = await _mockPipeline.RequestAuthorizationEndpointAsync(
            clientId: "rp",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://rp/callback",
            state: "s",
            nonce: "n");

        _mockPipeline.RemoveLoginCookie();

        await EndSessionAnonymouslyAsync(authorization.IdentityToken);

        _mockPipeline.LogoutRequest.ClientId.Should().Be("rp");
        _mockPipeline.LogoutRequest.PostLogoutRedirectUri.Should().Be("https://rp/signout-callback");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task anonymous_caller_with_access_token_hint_is_ignored()
    {
        var accessToken = await GetBobsAccessTokenWithAudRpAsync();

        await EndSessionAnonymouslyAsync(accessToken);

        _mockPipeline.LogoutRequest.ClientId.Should().BeNull();
        _mockPipeline.LogoutRequest.PostLogoutRedirectUri.Should().BeNull();
    }
}
