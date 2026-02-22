// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net.Http.Json;
using Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;
using Duende.IdentityServer.IntegrationTests.TestHosts;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.IntegrationTests.Configuration;

public class DynamicClientRegistrationTests : ConfigurationIntegrationTestBase
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    [Fact]
    public async Task valid_request_creates_new_client()
    {
        IdentityServerHost.ApiScopes.Add(new ApiScope("api1"));

        var request = new DynamicClientRegistrationRequest
        {
            RedirectUris = new[] { new Uri("https://example.com/callback") },
            GrantTypes = new[] { "authorization_code" },
            ClientName = "test",
            ClientUri = new Uri("https://example.com"),
            DefaultMaxAge = 10000,
            Scope = "api1 openid profile"
        };
        var httpResponse = await ConfigurationHost.HttpClient!.PostAsJsonAsync("/connect/dcr", request);

        var response = await httpResponse.Content.ReadFromJsonAsync<DynamicClientRegistrationResponse>();
        response.ShouldNotBeNull();
        var newClient = await IdentityServerHost.GetClientAsync(response!.ClientId, _ct); // Not null already asserted
        newClient.ShouldNotBeNull();
        newClient.ClientId.ShouldBe(response.ClientId);
        newClient.AllowedGrantTypes.ShouldBe(request.GrantTypes);
        newClient.ClientName.ShouldBe(request.ClientName);
        newClient.ClientUri.ShouldBe(request.ClientUri.ToString());
        newClient.UserSsoLifetime.ShouldBe(request.DefaultMaxAge);
        newClient.ClientSecrets.Count.ShouldBe(1);
        newClient.ClientSecrets.Single().Value.ShouldBe(response.ClientSecret.Sha256());
    }

    [Fact]
    public async Task request_for_public_client_does_not_require_client_secret()
    {
        IdentityServerHost.ApiScopes.Add(new ApiScope("api1"));

        var request = new DynamicClientRegistrationRequest
        {
            RedirectUris = new[] { new Uri("https://example.com/callback") },
            GrantTypes = new[] { "authorization_code" },
            ClientName = "test",
            ClientUri = new Uri("https://example.com"),
            DefaultMaxAge = 10000,
            Scope = "api1 openid profile",
            TokenEndpointAuthenticationMethod = "none"
        };
        var httpResponse = await ConfigurationHost.HttpClient!.PostAsJsonAsync("/connect/dcr", request);

        var response = await httpResponse.Content.ReadFromJsonAsync<DynamicClientRegistrationResponse>();
        response.ShouldNotBeNull();
        response.ClientSecret.ShouldBeNull();
        response.RequireClientSecret.ShouldNotBeNull();
        response.RequireClientSecret.Value.ShouldBeFalse();
    }
}
