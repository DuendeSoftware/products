// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.IntegrationTests.Extensibility;

public class CustomTokenCreationServiceTests
{
    private const string Category = "CustomTokenCreationServiceTests";

    private IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();

    public CustomTokenCreationServiceTests()
    {
        _mockPipeline.OnPostConfigureServices += svcs =>
        {
            svcs.AddTransient<ITokenCreationService, CustomTokenCreationService>();
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
    public async Task custom_aud_should_be_in_access_token()
    {
        var result = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(
            new ClientCredentialsTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = "test",
                ClientSecret = "secret"
            });
        result.IsError.ShouldBeFalse();

        var accessToken = result.AccessToken;
        var payload = accessToken.Split('.')[1];
        var json = Encoding.UTF8.GetString(Base64Url.Decode(payload));
        var obj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        obj["aud"].ToStringList().ShouldContain("custom1");
    }
}

public class CustomTokenCreationService : DefaultTokenCreationService
{
    public CustomTokenCreationService(IClock clock, IKeyMaterialService keys, IdentityServerOptions options, ILogger<DefaultTokenCreationService> logger) : base(clock, keys, options, logger)
    {
    }

    protected override Task<string> CreatePayloadAsync(Token token)
    {
        token.Audiences.Add("custom1");
        return base.CreatePayloadAsync(token);
    }
}
