// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Shouldly;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IntegrationTests.Extensibility;

public class CustomClaimsServiceTests
{
    private const string Category = "CustomClaimsServiceTests";

    private IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();

    public CustomClaimsServiceTests()
    {
        _mockPipeline.OnPostConfigureServices += svcs =>
        {
            svcs.AddTransient<IClaimsService, CustomClaimsService>();
        };

        _mockPipeline.Clients.Add(new Client
        {
            ClientId = "test",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = { "scope1" }
        });

        _mockPipeline.ApiScopes.Add(new ApiScope("scope1"));
        _mockPipeline.ApiResources.Add(new ApiResource("urn:res1") { Scopes = { "scope1" } });

        _mockPipeline.Users.Add(new Duende.IdentityServer.Test.TestUser
        {
            SubjectId = "bob",
            Username = "bob",
            Password = "password",
        });

        _mockPipeline.Initialize();
    }

    [Fact]
    public async Task custom_claims_should_be_in_access_token()
    {
        var result = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(
            new ClientCredentialsTokenRequest { 
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = "test",
                ClientSecret = "secret"
            });
        result.IsError.ShouldBeFalse();

        var accessToken = result.AccessToken;
        var payload = accessToken.Split('.')[1];
        var json = Encoding.UTF8.GetString(Base64Url.Decode(payload));
        var obj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        obj["foo"].GetString().ShouldBe("foo1");
    }
}

public class CustomClaimsService : DefaultClaimsService
{
    public CustomClaimsService(IProfileService profile, ILogger<DefaultClaimsService> logger) : base(profile, logger)
    {
    }

    public override async Task<IEnumerable<Claim>> GetAccessTokenClaimsAsync(ClaimsPrincipal subject, ResourceValidationResult resourceResult, ValidatedRequest request)
    {
        var result = (await base.GetAccessTokenClaimsAsync(subject, resourceResult, request)).ToList();

        result.Add(new Claim("foo", "foo1"));

        return result;
    }
}