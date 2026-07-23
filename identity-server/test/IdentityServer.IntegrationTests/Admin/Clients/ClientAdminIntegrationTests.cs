// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using System.Text.Json;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using SecretHashAlgorithm = Duende.IdentityServer.Admin.SecretHashAlgorithm;

namespace Duende.IdentityServer.IntegrationTests.Admin.Clients;

/// <summary>
/// End-to-end token flow tests that verify clients created via <see cref="IClientAdmin"/>
/// are found at runtime by <c>IClientStore</c>, validated, and accepted by the token endpoint.
/// </summary>
public sealed class ClientAdminIntegrationTests(WebServerFixture webApp)
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Can_perform_client_credentials_flow()
    {
        await using var fixture = new StorageBasedIdentityServerFixture(webApp);
        await fixture.InitializeAsync();

        var clientId = $"cc_{Guid.NewGuid():N}";
        const string plaintext = "super-secret";

        // Create the client via admin API with an initial secret
        var createResult = await fixture.ClientAdmin.CreateAsync(
            new CreateClient
            {
                ClientId = clientId,
                RequireClientSecret = true,
                AllowedGrantTypes = [GrantType.ClientCredentials],
                AllowedScopes = ["scope1"],
                ClientSecrets =
                [
                    new CreateClientSecret
                    {
                        PlaintextValue = plaintext,
                        HashAlgorithm = SecretHashAlgorithm.Sha256
                    }
                ]
            },
            _ct);
        createResult.IsSuccess.ShouldBeTrue($"CreateAsync failed: {createResult}");

        // POST to /connect/token
        var response = await fixture.HttpClient.PostAsync("/connect/token",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", "scope1"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", plaintext)
            ]),
            _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));
        json.RootElement.TryGetProperty("access_token", out var tokenElement).ShouldBeTrue(
            "Response JSON should contain access_token");
        tokenElement.GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Cors_policy_allows_configured_origin()
    {
        await using var fixture = new StorageBasedIdentityServerFixture(webApp);
        await fixture.InitializeAsync();

        const string allowedOrigin = "https://spa.example.com";

        var createResult = await fixture.ClientAdmin.CreateAsync(
            new CreateClient
            {
                ClientId = $"cors_{Guid.NewGuid():N}",
                RequireClientSecret = false,
                AllowedGrantTypes = [GrantType.AuthorizationCode],
                AllowedCorsOrigins = [allowedOrigin],
                RedirectUris = ["https://spa.example.com"],
            },
            _ct);
        createResult.IsSuccess.ShouldBeTrue($"CreateAsync failed: {createResult}");

        // Send a preflight OPTIONS request to the token endpoint with the configured origin
        var request = new HttpRequestMessage(HttpMethod.Options, "/connect/token");
        request.Headers.Add("Origin", allowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await fixture.HttpClient.SendAsync(request, _ct);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins).ShouldBeTrue(
            "Expected Access-Control-Allow-Origin header in response");
        origins.ShouldContain(allowedOrigin);
    }

    [Fact]
    public async Task Cors_policy_rejects_unconfigured_origin()
    {
        await using var fixture = new StorageBasedIdentityServerFixture(webApp);
        await fixture.InitializeAsync();

        var createResult = await fixture.ClientAdmin.CreateAsync(
            new CreateClient
            {
                ClientId = $"cors_{Guid.NewGuid():N}",
                RequireClientSecret = false,
                AllowedGrantTypes = [GrantType.AuthorizationCode],
                AllowedCorsOrigins = ["https://allowed.example.com"],
                RedirectUris = ["https://allowed.example.com"],
            },
            _ct);
        createResult.IsSuccess.ShouldBeTrue($"CreateAsync failed: {createResult}");

        // Send a preflight request with an origin that is NOT configured
        var request = new HttpRequestMessage(HttpMethod.Options, "/connect/token");
        request.Headers.Add("Origin", "https://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await fixture.HttpClient.SendAsync(request, _ct);

        var hasOriginHeader = response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins);
        if (hasOriginHeader && origins is not null)
        {
            origins!.ShouldNotContain("https://evil.example.com");
        }
    }
}
