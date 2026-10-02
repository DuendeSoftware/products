// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.IntegrationTests.Admin.Clients;

/// <summary>
/// End-to-end JAR (JWT-secured Authorization Request) tests verifying signed request object
/// validation at the <c>/connect/authorize</c> endpoint for storage-backed admin-created
/// clients with JWK secrets.
/// </summary>
public sealed class ClientAdminJarTests(WebServerFixture webApp)
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task jwk_secret_created_with_client_validates_signed_request_object()
    {
        await using var fixture = new StorageBasedIdentityServerFixture(webApp);
        await fixture.InitializeAsync();

        var clientId = $"jar_jwk_{Guid.NewGuid():N}";
        const string redirectUri = "https://client/callback";

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
                RequireClientSecret = false,
                RequireRequestObject = true,
                AllowedGrantTypes = [GrantType.Implicit],
                RedirectUris = [redirectUri],
                AllowedScopes = ["openid"],
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
        var requestJwt = CreateRequestJwt(clientId, fixture.BaseAddress.ToString().TrimEnd('/'), redirectUri, signingCredentials);

        var url = BuildAuthorizeUrl(clientId, requestJwt);

        var response = await fixture.HttpClient.GetAsync(url, _ct);

        AssertRequestObjectAccepted(response);
    }

    [Fact]
    public async Task jwk_secret_added_to_client_validates_signed_request_object()
    {
        await using var fixture = new StorageBasedIdentityServerFixture(webApp);
        await fixture.InitializeAsync();

        var clientId = $"jar_jwk_added_{Guid.NewGuid():N}";
        const string redirectUri = "https://client/callback";

        var createResult = await fixture.ClientAdmin.CreateAsync(
            new CreateClient
            {
                ClientId = clientId,
                RequireClientSecret = false,
                RequireRequestObject = true,
                AllowedGrantTypes = [GrantType.Implicit],
                RedirectUris = [redirectUri],
                AllowedScopes = ["openid"]
            },
            _ct);
        createResult.IsSuccess.ShouldBeTrue($"CreateAsync failed: {createResult}");

        using var rsa = RSA.Create(2048);
        using var publicRsa = RSA.Create();
        publicRsa.ImportParameters(rsa.ExportParameters(false));
        var rsaSecurityKey = new RsaSecurityKey(publicRsa) { KeyId = Guid.NewGuid().ToString("N") };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaSecurityKey);
        var jwkJson = JsonSerializer.Serialize(jwk);

        var createSecretResult = await fixture.ClientAdmin.CreateSecretAsync(
            createResult.Id,
            new CreateClientSecret
            {
                PlaintextValue = jwkJson,
                Type = IdentityServerConstants.SecretTypes.JsonWebKey
            },
            _ct);
        createSecretResult.IsSuccess.ShouldBeTrue($"CreateSecretAsync failed: {createSecretResult}");

        var signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        var requestJwt = CreateRequestJwt(clientId, fixture.BaseAddress.ToString().TrimEnd('/'), redirectUri, signingCredentials);

        var url = BuildAuthorizeUrl(clientId, requestJwt);

        var response = await fixture.HttpClient.GetAsync(url, _ct);

        AssertRequestObjectAccepted(response);
    }

    [Fact]
    public async Task x509_base64_secret_created_with_client_validates_signed_request_object()
    {
        await using var fixture = new StorageBasedIdentityServerFixture(webApp);
        await fixture.InitializeAsync();

        var clientId = $"jar_x509_created_{Guid.NewGuid():N}";
        const string redirectUri = "https://client/callback";

        using var cert = TestCert.Load();
        var certBase64 = Convert.ToBase64String(cert.Export(X509ContentType.Cert));

        var createResult = await fixture.ClientAdmin.CreateAsync(
            new CreateClient
            {
                ClientId = clientId,
                RequireClientSecret = false,
                RequireRequestObject = true,
                AllowedGrantTypes = [GrantType.Implicit],
                RedirectUris = [redirectUri],
                AllowedScopes = ["openid"],
                ClientSecrets =
                [
                    new CreateClientSecret
                    {
                        PlaintextValue = certBase64,
                        Type = IdentityServerConstants.SecretTypes.X509CertificateBase64
                    }
                ]
            },
            _ct);
        createResult.IsSuccess.ShouldBeTrue($"CreateAsync failed: {createResult}");

        var signingCredentials = new X509SigningCredentials(cert);
        var requestJwt = CreateRequestJwt(clientId, fixture.BaseAddress.ToString().TrimEnd('/'), redirectUri, signingCredentials);

        var url = BuildAuthorizeUrl(clientId, requestJwt);

        var response = await fixture.HttpClient.GetAsync(url, _ct);

        AssertRequestObjectAccepted(response);
    }

    [Fact]
    public async Task x509_base64_secret_added_to_client_validates_signed_request_object()
    {
        await using var fixture = new StorageBasedIdentityServerFixture(webApp);
        await fixture.InitializeAsync();

        var clientId = $"jar_x509_added_{Guid.NewGuid():N}";
        const string redirectUri = "https://client/callback";

        var createResult = await fixture.ClientAdmin.CreateAsync(
            new CreateClient
            {
                ClientId = clientId,
                RequireClientSecret = false,
                RequireRequestObject = true,
                AllowedGrantTypes = [GrantType.Implicit],
                RedirectUris = [redirectUri],
                AllowedScopes = ["openid"]
            },
            _ct);
        createResult.IsSuccess.ShouldBeTrue($"CreateAsync failed: {createResult}");

        using var cert = TestCert.Load();
        var certBase64 = Convert.ToBase64String(cert.Export(X509ContentType.Cert));

        var createSecretResult = await fixture.ClientAdmin.CreateSecretAsync(
            createResult.Id,
            new CreateClientSecret
            {
                PlaintextValue = certBase64,
                Type = IdentityServerConstants.SecretTypes.X509CertificateBase64
            },
            _ct);
        createSecretResult.IsSuccess.ShouldBeTrue($"CreateSecretAsync failed: {createSecretResult}");

        var signingCredentials = new X509SigningCredentials(cert);
        var requestJwt = CreateRequestJwt(clientId, fixture.BaseAddress.ToString().TrimEnd('/'), redirectUri, signingCredentials);

        var url = BuildAuthorizeUrl(clientId, requestJwt);

        var response = await fixture.HttpClient.GetAsync(url, _ct);

        AssertRequestObjectAccepted(response);
    }

    [Fact]
    public async Task signed_request_object_with_wrong_key_is_rejected()
    {
        await using var fixture = new StorageBasedIdentityServerFixture(webApp);
        await fixture.InitializeAsync();

        var clientId = $"jar_jwk_wrong_{Guid.NewGuid():N}";
        const string redirectUri = "https://client/callback";

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
                RequireClientSecret = false,
                RequireRequestObject = true,
                AllowedGrantTypes = [GrantType.Implicit],
                RedirectUris = [redirectUri],
                AllowedScopes = ["openid"],
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

        // Sign with a different key that was never registered as a client secret.
        using var wrongRsa = RSA.Create(2048);
        var wrongSigningCredentials = new SigningCredentials(new RsaSecurityKey(wrongRsa), SecurityAlgorithms.RsaSha256);
        var requestJwt = CreateRequestJwt(clientId, fixture.BaseAddress.ToString().TrimEnd('/'), redirectUri, wrongSigningCredentials);

        var url = BuildAuthorizeUrl(clientId, requestJwt);

        var response = await fixture.HttpClient.GetAsync(url, _ct);

        AssertRequestObjectRejected(response);
    }

    private static void AssertRequestObjectAccepted(HttpResponseMessage response)
    {
        // A successfully validated request object results in a redirect toward the login
        // page (no user is authenticated yet), never an "invalid_request" error response.
        var (isErrorRedirect, description) = InspectAuthorizeResponse(response);
        isErrorRedirect.ShouldBeFalse($"Expected request object to be accepted, but got: {description}");
    }

    private static void AssertRequestObjectRejected(HttpResponseMessage response)
    {
        var (isErrorRedirect, description) = InspectAuthorizeResponse(response);
        isErrorRedirect.ShouldBeTrue($"Expected request object to be rejected, but got: {description}");
    }

    private static (bool IsErrorRedirect, string Description) InspectAuthorizeResponse(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther)
        {
            var location = response.Headers.Location?.ToString() ?? string.Empty;
            // A rejected/invalid request object results in a redirect to the generic error
            // page (e.g. "/home/error?errorId=..."), whereas an accepted request object
            // redirects toward the login page (e.g. "/Account/Login?ReturnUrl=...").
            var isError = location.Contains("error=invalid_request", StringComparison.OrdinalIgnoreCase)
                || location.Contains("/home/error", StringComparison.OrdinalIgnoreCase)
                || location.Contains("errorId=", StringComparison.OrdinalIgnoreCase);
            return (isError, $"{response.StatusCode} -> {location}");
        }

        return (true, $"Unexpected status code: {response.StatusCode}");
    }

    private static string BuildAuthorizeUrl(string clientId, string requestJwt) =>
        new RequestUrl("/connect/authorize").CreateAuthorizeUrl(
            clientId: clientId,
            responseType: "id_token",
            extra: Parameters.FromObject(new { request = requestJwt }));

    private static string CreateRequestJwt(string issuer, string audience, string redirectUri, SigningCredentials credentials)
    {
        var handler = new JwtSecurityTokenHandler();
        handler.OutboundClaimTypeMap.Clear();

        var claims = new[]
        {
            new Claim("client_id", issuer),
            new Claim("response_type", "id_token"),
            new Claim("scope", "openid"),
            new Claim("state", "123state"),
            new Claim("nonce", "123nonce"),
            new Claim("redirect_uri", redirectUri)
        };

        var token = handler.CreateJwtSecurityToken(
            issuer: issuer,
            audience: audience,
            signingCredentials: credentials,
            subject: new ClaimsIdentity(claims));

        return handler.WriteToken(token);
    }
}
