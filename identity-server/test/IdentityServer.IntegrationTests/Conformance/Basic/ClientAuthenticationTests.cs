// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;

namespace Duende.IdentityServer.IntegrationTests.Conformance.Basic;

public class ClientAuthenticationTests
{
    private const string Category = "Conformance.Basic.ClientAuthenticationTests";
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly IdentityServerPipeline _pipeline = new IdentityServerPipeline();

    public ClientAuthenticationTests()
    {
        _pipeline.IdentityScopes.Add(new IdentityResources.OpenId());
        _pipeline.Clients.Add(new Client
        {
            Enabled = true,
            ClientId = "code_pipeline.Client",
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
                "https://code_pipeline.Client/callback",
                "https://code_pipeline.Client/callback?foo=bar&baz=quux"
            }
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
    [Trait("Category", Category)]
    public async Task Token_endpoint_supports_client_authentication_with_basic_authentication_with_POST()
    {
        await _pipeline.LoginAsync("bob");

        var nonce = Guid.NewGuid().ToString();

        _pipeline.BrowserClient.AllowAutoRedirect = false;
        var url = _pipeline.CreateAuthorizeUrl(
            clientId: "code_pipeline.Client",
            responseType: "code",
            scope: "openid",
            redirectUri: "https://code_pipeline.Client/callback?foo=bar&baz=quux",
            nonce: nonce);
        var response = await _pipeline.BrowserClient.GetAsync(url, _ct);

        var authorization = _pipeline.ParseAuthorizationResponseUrl(response.Headers.Location.ToString());
        authorization.Code.ShouldNotBeNull();

        var code = authorization.Code;

        // backchannel client
        var wrapper = new MessageHandlerWrapper(_pipeline.Handler);
        var tokenClient = new HttpClient(wrapper);
        var tokenResult = await tokenClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "code_pipeline.Client",
            ClientSecret = "secret",

            Code = code,
            RedirectUri = "https://code_pipeline.Client/callback?foo=bar&baz=quux"
        }, _ct);

        tokenResult.IsError.ShouldBeFalse();
        tokenResult.HttpErrorReason.ShouldBe("OK");
        tokenResult.TokenType.ShouldBe("Bearer");
        tokenResult.AccessToken.ShouldNotBeNull();
        tokenResult.ExpiresIn.ShouldBeGreaterThan(0);
        tokenResult.IdentityToken.ShouldNotBeNull();

        wrapper.Response.Headers.CacheControl.NoCache.ShouldBeTrue();
        wrapper.Response.Headers.CacheControl.NoStore.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Token_endpoint_supports_client_authentication_with_form_encoded_authentication_in_POST_body()
    {
        await _pipeline.LoginAsync("bob");

        var nonce = Guid.NewGuid().ToString();

        _pipeline.BrowserClient.AllowAutoRedirect = false;
        var url = _pipeline.CreateAuthorizeUrl(
            clientId: "code_pipeline.Client",
            responseType: "code",
            scope: "openid",
            redirectUri: "https://code_pipeline.Client/callback?foo=bar&baz=quux",
            nonce: nonce);
        var response = await _pipeline.BrowserClient.GetAsync(url, _ct);

        var authorization = _pipeline.ParseAuthorizationResponseUrl(response.Headers.Location.ToString());
        authorization.Code.ShouldNotBeNull();

        var code = authorization.Code;

        // backchannel client
        var wrapper = new MessageHandlerWrapper(_pipeline.Handler);
        var tokenClient = new HttpClient(wrapper);
        var tokenResult = await tokenClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "code_pipeline.Client",
            ClientSecret = "secret",
            ClientCredentialStyle = ClientCredentialStyle.PostBody,

            Code = code,
            RedirectUri = "https://code_pipeline.Client/callback?foo=bar&baz=quux"
        }, _ct);

        tokenResult.IsError.ShouldBeFalse();
        tokenResult.HttpErrorReason.ShouldBe("OK");
        tokenResult.TokenType.ShouldBe("Bearer");
        tokenResult.AccessToken.ShouldNotBeNull();
        tokenResult.ExpiresIn.ShouldBeGreaterThan(0);
        tokenResult.IdentityToken.ShouldNotBeNull();
    }
}
