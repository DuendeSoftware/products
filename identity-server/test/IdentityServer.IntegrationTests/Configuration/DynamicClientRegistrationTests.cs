// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;
using Duende.IdentityServer.IntegrationTests.TestHosts;
using Duende.IdentityServer.Models;
using Microsoft.IdentityModel.Tokens;

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

    [Fact]
    public async Task zero_absolute_refresh_token_lifetime_allows_unlimited_lifetime()
    {
        // A value of 0 for AbsoluteRefreshTokenLifetime means unlimited lifetime
        // See: https://docs.duendesoftware.com/identityserver/reference/v8/models/client/#refresh-token
        var request = new DynamicClientRegistrationRequest
        {
            RedirectUris = new[] { new Uri("https://example.com/callback") },
            GrantTypes = new[] { "authorization_code", "refresh_token" },
            AbsoluteRefreshTokenLifetime = 0
        };
        var httpResponse = await ConfigurationHost.HttpClient!.PostAsJsonAsync("/connect/dcr", request);

        var response = await httpResponse.Content.ReadFromJsonAsync<DynamicClientRegistrationResponse>();
        response.ShouldNotBeNull();
        var newClient = await IdentityServerHost.GetClientAsync(response!.ClientId, _ct);
        newClient.ShouldNotBeNull();
        newClient.AbsoluteRefreshTokenLifetime.ShouldBe(0);
    }

    [Fact]
    public async Task jwks_with_client_secret_basic_creates_both_jwk_and_shared_secrets()
    {
        using var rsaKey = RSA.Create();
        var securityKey = new RsaSecurityKey(rsaKey);
        var publicJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(securityKey);
        publicJwk.Alg = SecurityAlgorithms.RsaSha256;
        var jwkJson = JsonSerializer.Serialize(publicJwk);
        var jwkElement = JsonSerializer.Deserialize<JsonElement>(jwkJson);

        var request = new DynamicClientRegistrationRequest
        {
            RedirectUris = new[] { new Uri("https://example.com/callback") },
            GrantTypes = new[] { "authorization_code" },
            TokenEndpointAuthenticationMethod = "client_secret_basic",
            Jwks = new KeySet(new object[] { jwkElement })
        };
        var httpResponse = await ConfigurationHost.HttpClient!.PostAsJsonAsync("/connect/dcr", request);

        var response = await httpResponse.Content.ReadFromJsonAsync<DynamicClientRegistrationResponse>();
        response.ShouldNotBeNull();
        response!.ClientSecret.ShouldNotBeNull();

        var newClient = await IdentityServerHost.GetClientAsync(response.ClientId, _ct);
        newClient.ShouldNotBeNull();

        // Should have both a JWK secret and a shared secret
        newClient!.ClientSecrets.Count.ShouldBe(2);
        newClient.ClientSecrets.ShouldContain(s => s.Type == "JWK");
        newClient.ClientSecrets.ShouldContain(s => s.Type == "SharedSecret");
    }

    [Fact]
    public async Task jwks_with_private_key_jwt_does_not_create_shared_secret()
    {
        var rsaKey = RSA.Create();
        var securityKey = new RsaSecurityKey(rsaKey);
        var publicJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(securityKey);
        publicJwk.Alg = SecurityAlgorithms.RsaSha256;
        var jwkJson = JsonSerializer.Serialize(publicJwk);
        var jwkElement = JsonSerializer.Deserialize<JsonElement>(jwkJson);

        var request = new DynamicClientRegistrationRequest
        {
            RedirectUris = new[] { new Uri("https://example.com/callback") },
            GrantTypes = new[] { "authorization_code" },
            TokenEndpointAuthenticationMethod = "private_key_jwt",
            Jwks = new KeySet(new object[] { jwkElement })
        };
        var httpResponse = await ConfigurationHost.HttpClient!.PostAsJsonAsync("/connect/dcr", request);

        var response = await httpResponse.Content.ReadFromJsonAsync<DynamicClientRegistrationResponse>();
        response.ShouldNotBeNull();
        response!.ClientSecret.ShouldBeNull();

        var newClient = await IdentityServerHost.GetClientAsync(response.ClientId, _ct);
        newClient.ShouldNotBeNull();

        // Should only have JWK secret, no shared secret
        newClient!.ClientSecrets.Count.ShouldBe(1);
        newClient.ClientSecrets.Single().Type.ShouldBe("JWK");
    }

    [Fact]
    public async Task jwks_without_explicit_auth_method_defaults_to_client_secret_basic()
    {
        using var rsaKey = RSA.Create();
        var securityKey = new RsaSecurityKey(rsaKey);
        var publicJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(securityKey);
        publicJwk.Alg = SecurityAlgorithms.RsaSha256;
        var jwkJson = JsonSerializer.Serialize(publicJwk);
        var jwkElement = JsonSerializer.Deserialize<JsonElement>(jwkJson);

        var request = new DynamicClientRegistrationRequest
        {
            RedirectUris = new[] { new Uri("https://example.com/callback") },
            GrantTypes = new[] { "authorization_code" },
            Jwks = new KeySet(new object[] { jwkElement })
        };
        var httpResponse = await ConfigurationHost.HttpClient!.PostAsJsonAsync("/connect/dcr", request);

        var response = await httpResponse.Content.ReadFromJsonAsync<DynamicClientRegistrationResponse>();
        response.ShouldNotBeNull();
        response!.ClientSecret.ShouldNotBeNull();
        response.TokenEndpointAuthenticationMethod.ShouldBe("client_secret_basic");

        var newClient = await IdentityServerHost.GetClientAsync(response.ClientId, _ct);
        newClient.ShouldNotBeNull();

        // Should have both a JWK secret and a shared secret since auth method is client_secret_basic
        newClient!.ClientSecrets.Count.ShouldBe(2);
        newClient.ClientSecrets.ShouldContain(s => s.Type == "JWK");
        newClient.ClientSecrets.ShouldContain(s => s.Type == "SharedSecret");
    }

    [Fact]
    public async Task no_jwks_without_explicit_auth_method_defaults_to_client_secret_basic()
    {
        var request = new DynamicClientRegistrationRequest
        {
            RedirectUris = new[] { new Uri("https://example.com/callback") },
            GrantTypes = new[] { "authorization_code" }
        };
        var httpResponse = await ConfigurationHost.HttpClient!.PostAsJsonAsync("/connect/dcr", request);

        var response = await httpResponse.Content.ReadFromJsonAsync<DynamicClientRegistrationResponse>();
        response.ShouldNotBeNull();
        response!.ClientSecret.ShouldNotBeNull();
        response.TokenEndpointAuthenticationMethod.ShouldBe("client_secret_basic");
    }
}
