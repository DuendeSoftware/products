// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Shouldly;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;
using IntegrationTests.Common;
using Xunit;
using Duende.IdentityModel.Client;
using Duende.IdentityModel;

namespace IntegrationTests.Endpoints.Token;

public class RefreshTokenTests
{
    private const string Category = "Refresh Token Tests";

    private readonly Client _client;
    private string client_id = "client";
    private string client_secret = "secret";

    private IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();

    public RefreshTokenTests()
    {
        _mockPipeline.Clients.Add(_client = new Client
        {
            ClientId = client_id,
            AllowedGrantTypes = GrantTypes.Code,
            ClientSecrets =
                {
                    new Secret(client_secret.Sha256()),
                },
            AllowOfflineAccess = true,
            RequirePkce = false,
            RedirectUris = { "https://client/callback" },
            AllowedScopes = new List<string> { "openid", "scope" },
        });


        _mockPipeline.Users.Add(new TestUser
        {
            SubjectId = "bob",
            Username = "bob",
            Password = "password",
            Claims = new Claim[]
            {
                new Claim("name", "Bob Loblaw"),
                new Claim("email", "bob@loblaw.com"),
                new Claim("role", "Attorney")
            }
        });

        _mockPipeline.IdentityScopes.AddRange(new IdentityResource[] {
            new IdentityResources.OpenId()
        });

        _mockPipeline.ApiScopes.AddRange(new[] {
            new ApiScope("scope")
        });

        _mockPipeline.Initialize();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task sid_should_be_in_access_token_after_token_is_renewed()
    {
        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var authz = await _mockPipeline.RequestAuthorizationEndpointAsync(
                    clientId: client_id,
                    responseType: "code",
                    scope: "openid scope offline_access",
                    redirectUri: "https://client/callback");
        authz.Code.ShouldNotBeNull();

        var code = authz.Code;

        var wrapper = new MessageHandlerWrapper(_mockPipeline.Handler);
        var tokenClient = new HttpClient(wrapper);
        var tokenResult1 = await tokenClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = client_id,
            ClientSecret = client_secret,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,

            Code = code,
            RedirectUri = "https://client/callback"
        });

        tokenResult1.IsError.ShouldBeFalse();
        tokenResult1.AccessToken.ShouldNotBeNull();


        var payload1 = JsonSerializer.Deserialize<JsonElement>(Base64Url.Decode(tokenResult1.AccessToken.Split('.')[1]));
        var sid1 = payload1.TryGetValue("sid").GetString();
        sid1.ShouldBe(_mockPipeline.GetSessionCookie().Value);

        var tokenResult2 = await tokenClient.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = client_id,
            ClientSecret = client_secret,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
            
            RefreshToken = tokenResult1.RefreshToken
        });

        tokenResult2.IsError.ShouldBeFalse();
        tokenResult2.AccessToken.ShouldNotBeNull();

        var payload2 = JsonSerializer.Deserialize<JsonElement>(Base64Url.Decode(tokenResult2.AccessToken.Split('.')[1]));
        var sid2 = payload2.TryGetValue("sid").GetString();
        sid1.ShouldBe(sid2);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_UpdateAccessTokenClaimsOnRefresh_is_set_sid_should_be_in_access_token_after_token_is_renewed()
    {
        _client.UpdateAccessTokenClaimsOnRefresh = true;

        await _mockPipeline.LoginAsync("bob");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;

        var authz = await _mockPipeline.RequestAuthorizationEndpointAsync(
                    clientId: client_id,
                    responseType: "code",
                    scope: "openid scope offline_access",
                    redirectUri: "https://client/callback");
        authz.Code.ShouldNotBeNull();

        var code = authz.Code;

        var wrapper = new MessageHandlerWrapper(_mockPipeline.Handler);
        var tokenClient = new HttpClient(wrapper);
        var tokenResult1 = await tokenClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = client_id,
            ClientSecret = client_secret,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,

            Code = code,
            RedirectUri = "https://client/callback"
        });

        tokenResult1.IsError.ShouldBeFalse();
        tokenResult1.AccessToken.ShouldNotBeNull();


        var payload1 = JsonSerializer.Deserialize<JsonElement>(Base64Url.Decode(tokenResult1.AccessToken.Split('.')[1]));
        var sid1 = payload1.TryGetValue("sid").GetString();
        sid1.ShouldBe(_mockPipeline.GetSessionCookie().Value);

        var tokenResult2 = await tokenClient.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = client_id,
            ClientSecret = client_secret,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,

            RefreshToken = tokenResult1.RefreshToken
        });

        tokenResult2.IsError.ShouldBeFalse();
        tokenResult2.AccessToken.ShouldNotBeNull();

        var payload2 = JsonSerializer.Deserialize<JsonElement>(Base64Url.Decode(tokenResult2.AccessToken.Split('.')[1]));
        var sid2 = payload2.TryGetValue("sid").GetString();
        sid1.ShouldBe(sid2);
    }
}