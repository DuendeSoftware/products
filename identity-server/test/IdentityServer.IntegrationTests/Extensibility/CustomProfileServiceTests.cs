// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Buffers.Text;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Extensibility;

public class CustomProfileServiceTests
{
    private const string Category = "Authorize endpoint";

    private IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();

    public CustomProfileServiceTests()
    {
        _mockPipeline.OnPostConfigureServices += svcs =>
        {
            svcs.AddTransient<IProfileService, CustomProfileService>();
        };

        _mockPipeline.Clients.Add(new Client
        {
            ClientId = "implicit",
            AllowedGrantTypes = GrantTypes.Implicit,
            RedirectUris = { "https://client/callback" },
            RequireConsent = false,
            AllowedScopes = { "openid", "custom_identity" }
        });

        _mockPipeline.IdentityScopes.Add(new IdentityResources.OpenId());
        _mockPipeline.IdentityScopes.Add(new IdentityResource("custom_identity", new string[] { "foo" }));

        _mockPipeline.Users.Add(new Duende.IdentityServer.Test.TestUser
        {
            SubjectId = "bob",
            Username = "bob",
            Password = "password",
        });

        _mockPipeline.Initialize();
    }

    [Fact]
    public async Task custom_profile_should_return_claims_for_implicit_client()
    {
        await _mockPipeline.LoginAsync("bob");

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "implicit",
            responseType: "id_token",
            scope: "openid custom_identity",
            redirectUri: "https://client/callback",
            state: "state",
            nonce: "nonce");

        _mockPipeline.BrowserClient.AllowAutoRedirect = false;
        var response = await _mockPipeline.BrowserClient.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        response.Headers.Location.ToString().ShouldStartWith("https://client/callback");

        var authorization = new Duende.IdentityModel.Client.AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();
        authorization.IdentityToken.ShouldNotBeNull();

        var payload = authorization.IdentityToken.Split('.')[1];
        var json = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(payload));
        var obj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        obj["foo"].GetString().ShouldBe("bar");
    }
}

public class CustomProfileService : IProfileService
{
    public Task GetProfileDataAsync(ProfileDataRequestContext context, CT ct)
    {
        var claims = new Claim[]
        {
            new Claim("foo", "bar")
        };
        context.AddRequestedClaims(claims);
        return Task.CompletedTask;
    }

    public Task IsActiveAsync(IsActiveContext context, CT ct)
    {
        context.IsActive = true;
        return Task.CompletedTask;
    }
}
