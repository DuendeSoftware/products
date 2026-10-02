// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.ApiResources;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.IntegrationTests.Admin.ApiResources;

/// <summary>
/// End-to-end protocol tests verifying that API resource secrets created via
/// <see cref="IApiResourceAdmin.CreateSecretAsync"/> authenticate correctly against the live
/// introspection endpoint, using <see cref="ApiResourceAdminAuthenticationFixture"/>.
/// </summary>
public sealed class ApiResourceAdminProtocolTests(WebServerFixture webApp)
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task x509_thumbprint_secret_created_via_admin_authenticates_to_introspection()
    {
        await using var fixture = new ApiResourceAdminAuthenticationFixture(webApp);
        await fixture.InitializeAsync();

        using var cert = TestCert.Load();
        var thumbprint = cert.Thumbprint;
        thumbprint.ShouldNotBeNullOrEmpty();

        var scopeName = $"scope_{Guid.NewGuid():N}";
        var apiScopeAdmin = fixture.GetRequiredService<IApiScopeAdmin>();
        var scopeResult = await apiScopeAdmin.CreateAsync(new CreateApiScope { Name = scopeName }, _ct);
        scopeResult.IsSuccess.ShouldBeTrue($"ApiScope CreateAsync failed: {scopeResult}");

        var resourceName = $"api_{Guid.NewGuid():N}";
        var createResult = await fixture.ApiResourceAdmin.CreateAsync(
            new CreateApiResource
            {
                Name = resourceName,
                Scopes = [scopeName]
            },
            _ct);
        createResult.IsSuccess.ShouldBeTrue($"ApiResource CreateAsync failed: {createResult}");

        var secretResult = await fixture.ApiResourceAdmin.CreateSecretAsync(
            createResult.Id!,
            thumbprint!,
            null,
            null,
            null,
            IdentityServerConstants.SecretTypes.X509CertificateThumbprint,
            _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecretAsync failed: {secretResult}");

        var clientId = $"client_{Guid.NewGuid():N}";
        var clientAdmin = fixture.GetRequiredService<IClientAdmin>();
        var clientResult = await clientAdmin.CreateAsync(
            new CreateClient
            {
                ClientId = clientId,
                AllowedGrantTypes = GrantTypes.ClientCredentials.ToList(),
                AllowedScopes = [scopeName],
                ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
            },
            _ct);
        clientResult.IsSuccess.ShouldBeTrue($"Client CreateAsync failed: {clientResult}");

        var tokenResponse = await fixture.HttpClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = fixture.BuildUri("connect/token").ToString(),
            ClientId = clientId,
            ClientSecret = "secret",
            Scope = scopeName
        });

        tokenResponse.IsError.ShouldBeFalse($"Token request failed: {tokenResponse.Error} {tokenResponse.ErrorDescription}");
        tokenResponse.AccessToken.ShouldNotBeNullOrEmpty();

        using var mtlsClient = fixture.CreateMtlsClient(cert);
        var introspectionResponse = await mtlsClient.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = fixture.BuildUri("connect/introspect").ToString(),
            ClientId = resourceName,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,

            Token = tokenResponse.AccessToken
        });

        introspectionResponse.IsError.ShouldBeFalse($"Introspection failed: {introspectionResponse.Error}");
        introspectionResponse.IsActive.ShouldBeTrue("the X509 thumbprint secret created via the admin API should authenticate the introspection request");
    }

    [Fact]
    public async Task jwk_secret_created_via_admin_authenticates_introspection_with_private_key_jwt()
    {
        await using var fixture = new ApiResourceAdminAuthenticationFixture(webApp);
        await fixture.InitializeAsync();

        using var rsaKey = RSA.Create(2048);
        var publicJwkJson = CreatePublicJwkJson(rsaKey);

        var scopeName = $"scope_{Guid.NewGuid():N}";
        var apiScopeAdmin = fixture.GetRequiredService<IApiScopeAdmin>();
        var scopeResult = await apiScopeAdmin.CreateAsync(new CreateApiScope { Name = scopeName }, _ct);
        scopeResult.IsSuccess.ShouldBeTrue($"ApiScope CreateAsync failed: {scopeResult}");

        var resourceName = $"api_{Guid.NewGuid():N}";
        var createResult = await fixture.ApiResourceAdmin.CreateAsync(
            new CreateApiResource
            {
                Name = resourceName,
                Scopes = [scopeName]
            },
            _ct);
        createResult.IsSuccess.ShouldBeTrue($"ApiResource CreateAsync failed: {createResult}");

        var secretResult = await fixture.ApiResourceAdmin.CreateSecretAsync(
            createResult.Id!,
            publicJwkJson,
            null,
            null,
            null,
            IdentityServerConstants.SecretTypes.JsonWebKey,
            _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecretAsync failed: {secretResult}");

        var clientId = $"client_{Guid.NewGuid():N}";
        var clientAdmin = fixture.GetRequiredService<IClientAdmin>();
        var clientResult = await clientAdmin.CreateAsync(
            new CreateClient
            {
                ClientId = clientId,
                AllowedGrantTypes = GrantTypes.ClientCredentials.ToList(),
                AllowedScopes = [scopeName],
                ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
            },
            _ct);
        clientResult.IsSuccess.ShouldBeTrue($"Client CreateAsync failed: {clientResult}");

        var tokenResponse = await fixture.HttpClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = fixture.BuildUri("connect/token").ToString(),
            ClientId = clientId,
            ClientSecret = "secret",
            Scope = scopeName
        });

        tokenResponse.IsError.ShouldBeFalse($"Token request failed: {tokenResponse.Error} {tokenResponse.ErrorDescription}");
        tokenResponse.AccessToken.ShouldNotBeNullOrEmpty();

        var issuer = fixture.BaseAddress.ToString().TrimEnd('/');
        var clientAssertion = CreateClientAssertion(resourceName, issuer, rsaKey);

        var introspectionResponse = await fixture.HttpClient.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = fixture.BuildUri("connect/introspect").ToString(),
            ClientId = resourceName,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
            ClientAssertion = new ClientAssertion
            {
                Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                Value = clientAssertion
            },

            Token = tokenResponse.AccessToken
        });

        introspectionResponse.IsError.ShouldBeFalse($"Introspection failed: {introspectionResponse.Error}");
        introspectionResponse.IsActive.ShouldBeTrue("the JWK secret created via the admin API should authenticate the introspection request");
    }

    [Fact]
    public async Task x509_base64_secret_created_via_admin_authenticates_introspection_with_private_key_jwt()
    {
        await using var fixture = new ApiResourceAdminAuthenticationFixture(webApp);
        await fixture.InitializeAsync();

        using var cert = TestCert.Load();
        var publicCertBase64 = Convert.ToBase64String(cert.Export(X509ContentType.Cert));

        var scopeName = $"scope_{Guid.NewGuid():N}";
        var apiScopeAdmin = fixture.GetRequiredService<IApiScopeAdmin>();
        var scopeResult = await apiScopeAdmin.CreateAsync(new CreateApiScope { Name = scopeName }, _ct);
        scopeResult.IsSuccess.ShouldBeTrue($"ApiScope CreateAsync failed: {scopeResult}");

        var resourceName = $"api_{Guid.NewGuid():N}";
        var createResult = await fixture.ApiResourceAdmin.CreateAsync(
            new CreateApiResource
            {
                Name = resourceName,
                Scopes = [scopeName]
            },
            _ct);
        createResult.IsSuccess.ShouldBeTrue($"ApiResource CreateAsync failed: {createResult}");

        var secretResult = await fixture.ApiResourceAdmin.CreateSecretAsync(
            createResult.Id!,
            publicCertBase64,
            null,
            null,
            null,
            IdentityServerConstants.SecretTypes.X509CertificateBase64,
            _ct);
        secretResult.IsSuccess.ShouldBeTrue($"CreateSecretAsync failed: {secretResult}");

        var clientId = $"client_{Guid.NewGuid():N}";
        var clientAdmin = fixture.GetRequiredService<IClientAdmin>();
        var clientResult = await clientAdmin.CreateAsync(
            new CreateClient
            {
                ClientId = clientId,
                AllowedGrantTypes = GrantTypes.ClientCredentials.ToList(),
                AllowedScopes = [scopeName],
                ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
            },
            _ct);
        clientResult.IsSuccess.ShouldBeTrue($"Client CreateAsync failed: {clientResult}");

        var tokenResponse = await fixture.HttpClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = fixture.BuildUri("connect/token").ToString(),
            ClientId = clientId,
            ClientSecret = "secret",
            Scope = scopeName
        });

        tokenResponse.IsError.ShouldBeFalse($"Token request failed: {tokenResponse.Error} {tokenResponse.ErrorDescription}");
        tokenResponse.AccessToken.ShouldNotBeNullOrEmpty();

        var issuer = fixture.BaseAddress.ToString().TrimEnd('/');
        var signingCredentials = new X509SigningCredentials(cert);
        var clientAssertion = CreateClientAssertion(resourceName, issuer, signingCredentials);

        var introspectionResponse = await fixture.HttpClient.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = fixture.BuildUri("connect/introspect").ToString(),
            ClientId = resourceName,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
            ClientAssertion = new ClientAssertion
            {
                Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                Value = clientAssertion
            },

            Token = tokenResponse.AccessToken
        });

        introspectionResponse.IsError.ShouldBeFalse($"Introspection failed: {introspectionResponse.Error}");
        introspectionResponse.IsActive.ShouldBeTrue("the X509 base64 secret created via the admin API should authenticate the introspection request");
    }

    private static string CreatePublicJwkJson(RSA rsaKey)
    {
        using var publicRsa = RSA.Create();
        publicRsa.ImportParameters(rsaKey.ExportParameters(false));
        var rsaSecurityKey = new RsaSecurityKey(publicRsa)
        {
            KeyId = Guid.NewGuid().ToString("N")
        };
        var publicJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaSecurityKey);
        publicJwk.D.ShouldBeNullOrEmpty();
        publicJwk.P.ShouldBeNullOrEmpty();
        publicJwk.Q.ShouldBeNullOrEmpty();
        publicJwk.DP.ShouldBeNullOrEmpty();
        publicJwk.DQ.ShouldBeNullOrEmpty();
        publicJwk.QI.ShouldBeNullOrEmpty();
        return JsonSerializer.Serialize(publicJwk);
    }

    private static string CreateClientAssertion(string clientId, string audience, RSA rsaKey) =>
        CreateClientAssertion(clientId, audience, new SigningCredentials(new RsaSecurityKey(rsaKey), SecurityAlgorithms.RsaSha256));

    private static string CreateClientAssertion(string clientId, string audience, SigningCredentials signingCredentials)
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
            signingCredentials);

        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.OutboundClaimTypeMap.Clear();
        return tokenHandler.WriteToken(token);
    }
}
