// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Microsoft.IdentityModel.Tokens;
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
    public async Task jwk_secret_created_with_client_can_authenticate_with_private_key_jwt()
    {
        await using var fixture = new StorageBasedIdentityServerFixture(webApp);
        await fixture.InitializeAsync();

        var clientId = $"jwt_jwk_create_{Guid.NewGuid():N}";

        using var rsa = RSA.Create(2048);
        using var publicRsa = RSA.Create();
        publicRsa.ImportParameters(rsa.ExportParameters(false));
        var rsaSecurityKey = new RsaSecurityKey(publicRsa) { KeyId = Guid.NewGuid().ToString("N") };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaSecurityKey);
        var jwkJson = JsonSerializer.Serialize(jwk);

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
                        PlaintextValue = jwkJson,
                        Type = IdentityServerConstants.SecretTypes.JsonWebKey
                    }
                ]
            },
            _ct);
        createResult.IsSuccess.ShouldBeTrue($"CreateAsync failed: {createResult}");

        var signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        var assertion = CreateClientAssertion(clientId, fixture.BaseAddress.ToString().TrimEnd('/'), signingCredentials);

        var response = await fixture.HttpClient.RequestClientCredentialsTokenAsync(
            new ClientCredentialsTokenRequest
            {
                Address = "/connect/token",
                ClientId = clientId,
                ClientCredentialStyle = ClientCredentialStyle.PostBody,
                Scope = "scope1",
                ClientAssertion = new ClientAssertion
                {
                    Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                    Value = assertion
                }
            },
            _ct);

        response.IsError.ShouldBeFalse($"Token request failed: {response.Error} {response.ErrorDescription}");
        response.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task jwk_secret_added_to_client_can_authenticate_with_private_key_jwt()
    {
        await using var fixture = new StorageBasedIdentityServerFixture(webApp);
        await fixture.InitializeAsync();

        var clientId = $"jwt_jwk_add_{Guid.NewGuid():N}";

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
                        PlaintextValue = "placeholder-secret-not-used-by-this-test",
                        HashAlgorithm = SecretHashAlgorithm.Sha256
                    }
                ]
            },
            _ct);
        createResult.IsSuccess.ShouldBeTrue($"CreateAsync failed: {createResult}");

        using var rsa = RSA.Create(2048);
        using var publicRsa = RSA.Create();
        publicRsa.ImportParameters(rsa.ExportParameters(false));
        var rsaSecurityKey = new RsaSecurityKey(publicRsa) { KeyId = Guid.NewGuid().ToString("N") };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaSecurityKey);
        var jwkJson = JsonSerializer.Serialize(jwk);

        var secretResult = await fixture.ClientAdmin.CreateSecretAsync(
            createResult.Id,
            new CreateClientSecret
            {
                PlaintextValue = jwkJson,
                Type = IdentityServerConstants.SecretTypes.JsonWebKey
            },
            _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecretAsync failed: {secretResult}");

        var signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        var assertion = CreateClientAssertion(clientId, fixture.BaseAddress.ToString().TrimEnd('/'), signingCredentials);

        var response = await fixture.HttpClient.RequestClientCredentialsTokenAsync(
            new ClientCredentialsTokenRequest
            {
                Address = "/connect/token",
                ClientId = clientId,
                ClientCredentialStyle = ClientCredentialStyle.PostBody,
                Scope = "scope1",
                ClientAssertion = new ClientAssertion
                {
                    Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                    Value = assertion
                }
            },
            _ct);

        response.IsError.ShouldBeFalse($"Token request failed: {response.Error} {response.ErrorDescription}");
        response.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task x509_base64_secret_created_with_client_can_authenticate_with_private_key_jwt()
    {
        await using var fixture = new StorageBasedIdentityServerFixture(webApp);
        await fixture.InitializeAsync();

        var clientId = $"jwt_x509_create_{Guid.NewGuid():N}";

        using var cert = TestCert.Load();
        var material = Convert.ToBase64String(cert.Export(X509ContentType.Cert));

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
                        PlaintextValue = material,
                        Type = IdentityServerConstants.SecretTypes.X509CertificateBase64
                    }
                ]
            },
            _ct);
        createResult.IsSuccess.ShouldBeTrue($"CreateAsync failed: {createResult}");

        var signingCredentials = new X509SigningCredentials(cert);
        var assertion = CreateClientAssertion(clientId, fixture.BaseAddress.ToString().TrimEnd('/'), signingCredentials);

        var response = await fixture.HttpClient.RequestClientCredentialsTokenAsync(
            new ClientCredentialsTokenRequest
            {
                Address = "/connect/token",
                ClientId = clientId,
                ClientCredentialStyle = ClientCredentialStyle.PostBody,
                Scope = "scope1",
                ClientAssertion = new ClientAssertion
                {
                    Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                    Value = assertion
                }
            },
            _ct);

        response.IsError.ShouldBeFalse($"Token request failed: {response.Error} {response.ErrorDescription}");
        response.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task x509_base64_secret_added_to_client_can_authenticate_with_private_key_jwt()
    {
        await using var fixture = new StorageBasedIdentityServerFixture(webApp);
        await fixture.InitializeAsync();

        var clientId = $"jwt_x509_add_{Guid.NewGuid():N}";

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
                        PlaintextValue = "placeholder-secret-not-used-by-this-test",
                        HashAlgorithm = SecretHashAlgorithm.Sha256
                    }
                ]
            },
            _ct);
        createResult.IsSuccess.ShouldBeTrue($"CreateAsync failed: {createResult}");

        using var cert = TestCert.Load();
        var material = Convert.ToBase64String(cert.Export(X509ContentType.Cert));

        var secretResult = await fixture.ClientAdmin.CreateSecretAsync(
            createResult.Id,
            new CreateClientSecret
            {
                PlaintextValue = material,
                Type = IdentityServerConstants.SecretTypes.X509CertificateBase64
            },
            _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecretAsync failed: {secretResult}");

        var signingCredentials = new X509SigningCredentials(cert);
        var assertion = CreateClientAssertion(clientId, fixture.BaseAddress.ToString().TrimEnd('/'), signingCredentials);

        var response = await fixture.HttpClient.RequestClientCredentialsTokenAsync(
            new ClientCredentialsTokenRequest
            {
                Address = "/connect/token",
                ClientId = clientId,
                ClientCredentialStyle = ClientCredentialStyle.PostBody,
                Scope = "scope1",
                ClientAssertion = new ClientAssertion
                {
                    Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                    Value = assertion
                }
            },
            _ct);

        response.IsError.ShouldBeFalse($"Token request failed: {response.Error} {response.ErrorDescription}");
        response.AccessToken.ShouldNotBeNullOrWhiteSpace();
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

    private static string CreateClientAssertion(string clientId, string audience, SigningCredentials credentials)
    {
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            clientId,
            audience,
            [
                new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString()),
                new Claim(JwtClaimTypes.Subject, clientId),
                new Claim(JwtClaimTypes.IssuedAt, ((DateTimeOffset)now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            ],
            now,
            now.AddMinutes(1),
            credentials);

        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.OutboundClaimTypeMap.Clear();
        return tokenHandler.WriteToken(token);
    }
}
