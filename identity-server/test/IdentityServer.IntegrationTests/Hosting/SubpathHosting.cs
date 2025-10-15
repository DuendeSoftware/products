// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public class SubpathHosting
{
    private const string Category = "Subpath endpoint";
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();
    private Client _client1;

    public SubpathHosting()
    {
        _mockPipeline.Clients.AddRange(new Client[] {
            _client1 = new Client
            {
                ClientId = "client1",
                AllowedGrantTypes = GrantTypes.Implicit,
                RequireConsent = false,
                AllowedScopes = new List<string> { "openid", "profile" },
                RedirectUris = new List<string> { "https://client1/callback" },
                AllowAccessTokensViaBrowser = true
            }
        });

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

        _mockPipeline.IdentityScopes.AddRange(new IdentityResource[] {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email()
        });

        _mockPipeline.Initialize("/subpath");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task anonymous_user_should_be_redirected_to_login_page()
    {
        var url = new RequestUrl("https://server/subpath/connect/authorize").CreateAuthorizeUrl(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");
        var response = await _mockPipeline.BrowserClient.GetAsync(url, _ct);

        _mockPipeline.LoginWasCalled.ShouldBeTrue();
    }
}
