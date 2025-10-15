// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using System.Security.Claims;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Test;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public class CorsTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private const string Category = "CORS Integration";

    private IdentityServerPipeline _pipeline = new IdentityServerPipeline();

    public CorsTests()
    {
        _pipeline.Clients.AddRange(new Client[] {
            new Client
            {
                ClientId = "client",
                AllowedGrantTypes = GrantTypes.Implicit,
                RequireConsent = true,
                AllowedScopes = new List<string> { "openid", "profile", "api1", "api2" },
                RedirectUris = new List<string> { "https://client/callback" },
                AllowedCorsOrigins = new List<string> { "https://client" }
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

        _pipeline.IdentityScopes.AddRange(new IdentityResource[] {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email()
        });
        _pipeline.ApiResources.AddRange(new ApiResource[] {
            new ApiResource
            {
                Name = "api",
                Scopes = { "api1", "api2" }
            }
        });
        _pipeline.ApiScopes.AddRange(new[] {
            new ApiScope
            {
                Name = "api1"
            },
            new ApiScope
            {
                Name = "api2"
            }
        });

        _pipeline.Initialize();
    }

    [Theory]
    [InlineData(IdentityServerPipeline.DiscoveryEndpoint)]
    [InlineData(IdentityServerPipeline.DiscoveryKeysEndpoint)]
    [InlineData(IdentityServerPipeline.TokenEndpoint)]
    [InlineData(IdentityServerPipeline.UserInfoEndpoint)]
    [InlineData(IdentityServerPipeline.RevocationEndpoint)]
    [Trait("Category", Category)]
    public async Task cors_request_to_allowed_endpoints_should_succeed(string url)
    {
        _pipeline.BackChannelClient.DefaultRequestHeaders.Add("Origin", "https://client");
        _pipeline.BackChannelClient.DefaultRequestHeaders.Add("Access-Control-Request-Method", "GET");

        var message = new HttpRequestMessage(HttpMethod.Options, url);
        var response = await _pipeline.BackChannelClient.SendAsync(message, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeTrue();
    }

    [Theory]
    [InlineData(IdentityServerPipeline.AuthorizeEndpoint)]
    [InlineData(IdentityServerPipeline.EndSessionEndpoint)]
    [InlineData(IdentityServerPipeline.CheckSessionEndpoint)]
    [InlineData(IdentityServerPipeline.LoginPage)]
    [InlineData(IdentityServerPipeline.ConsentPage)]
    [InlineData(IdentityServerPipeline.ErrorPage)]
    [Trait("Category", Category)]
    public async Task cors_request_to_restricted_endpoints_should_not_succeed(string url)
    {
        _pipeline.BackChannelClient.DefaultRequestHeaders.Add("Origin", "https://client");
        _pipeline.BackChannelClient.DefaultRequestHeaders.Add("Access-Control-Request-Method", "GET");

        var message = new HttpRequestMessage(HttpMethod.Options, url);
        var response = await _pipeline.BackChannelClient.SendAsync(message, _ct);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task custom_cors_policy_provider_should_be_used()
    {
        var policy = new StubCorePolicyProvider();
        _pipeline.OnPreConfigureServices += services =>
        {
            services.AddSingleton<ICorsPolicyService>(policy);
        };
        _pipeline.Initialize();

        _pipeline.BackChannelClient.DefaultRequestHeaders.Add("Origin", "https://client");
        _pipeline.BackChannelClient.DefaultRequestHeaders.Add("Access-Control-Request-Method", "GET");

        var message = new HttpRequestMessage(HttpMethod.Options, IdentityServerPipeline.DiscoveryEndpoint);
        var response = await _pipeline.BackChannelClient.SendAsync(message, _ct);

        policy.WasCalled.ShouldBeTrue();
    }
}

public class StubCorePolicyProvider : ICorsPolicyService
{
    public bool Result;
    public bool WasCalled;

    public Task<bool> IsOriginAllowedAsync(string origin)
    {
        WasCalled = true;
        return Task.FromResult(Result);
    }
}
