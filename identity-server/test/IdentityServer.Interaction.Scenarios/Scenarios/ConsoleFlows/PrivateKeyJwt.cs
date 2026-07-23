// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.Api;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Interaction.Scenarios.ConsoleFlows;

/// <summary>
/// Scenario: Client authentication using Private Key JWT (client_assertion).
/// Tests three credential types: RSA JWK, EC JWK (P-256), and X.509 certificate.
/// Each signs a JWT with its private key and sends it as a client assertion.
/// </summary>
public sealed class PrivateKeyJwt : IScenario
{
    private IdentityServerTestHost? _identityServer;
    private ApiHost? _api;
    private RSA? _rsaKey;
    private ECDsa? _ecKey;
    private X509Certificate2? _certificate;

    public string Name => "PrivateKeyJwt";
    public string Description => "Client assertion with Private Key JWT (RSA, EC, X.509)";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        // 1. Start IdentityServer with JWT bearer client authentication support
        _identityServer = new IdentityServerTestHost(configurator, "identity-server",
            configureIdentityServer: isBuilder =>
            {
                isBuilder.AddJwtBearerClientAuthentication();
            });
        _identityServer.AddDefaultUsers();
        _identityServer.AddDefaultResources();
        await _identityServer.StartAsync(ct);

        var authority = _identityServer.BuildUri().ToString().TrimEnd('/');

        // 2. Start the API
        _api = new ApiHost(configurator, "api", authority);
        await _api.StartAsync(ct);

        // 3. Generate keys and register clients

        // RSA key
        _rsaKey = RSA.Create(2048);
        var rsaSecurityKey = new RsaSecurityKey(_rsaKey)
        {
            KeyId = Guid.NewGuid().ToString("N")
        };
        var rsaPublicJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaSecurityKey);
        var rsaJwkJson = JsonSerializer.Serialize(rsaPublicJwk);

        _identityServer.AddClient(new Client
        {
            ClientId = "client.jwt.rsa",
            ClientSecrets =
            [
                new Secret
                {
                    Type = IdentityServerConstants.SecretTypes.JsonWebKey,
                    Value = rsaJwkJson
                }
            ],
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = ["resource1.scope1"]
        });

        // EC key (P-256)
        _ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var ecSecurityKey = new ECDsaSecurityKey(_ecKey)
        {
            KeyId = Guid.NewGuid().ToString("N")
        };
        var ecPublicJwk = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(ecSecurityKey);
        var ecJwkJson = JsonSerializer.Serialize(ecPublicJwk);

        _identityServer.AddClient(new Client
        {
            ClientId = "client.jwt.ec",
            ClientSecrets =
            [
                new Secret
                {
                    Type = IdentityServerConstants.SecretTypes.JsonWebKey,
                    Value = ecJwkJson
                }
            ],
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = ["resource1.scope1"]
        });

        // X.509 certificate (self-signed)
        var certRequest = new CertificateRequest(
            "CN=PrivateKeyJwtTestClient",
            RSA.Create(2048),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        _certificate = certRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddYears(1));

        _identityServer.AddClient(new Client
        {
            ClientId = "client.jwt.x509",
            ClientSecrets =
            [
                new Secret
                {
                    Type = IdentityServerConstants.SecretTypes.X509CertificateBase64,
                    Value = Convert.ToBase64String(_certificate.Export(X509ContentType.Cert))
                }
            ],
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = ["resource1.scope1"]
        });

        Links = [_identityServer.Link, _api.Link];
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_api != null)
        {
            await _api.DisposeAsync();
        }

        if (_identityServer != null)
        {
            await _identityServer.DisposeAsync();
        }

        _rsaKey?.Dispose();
        _ecKey?.Dispose();
        _certificate?.Dispose();
    }

    public Command[] GetCommands() =>
    [
        new Command
        {
            Name = "Private Key JWT (RSA)",
            Execute = ctx => RunFlowAsync(ctx, "client.jwt.rsa",
                new SigningCredentials(new RsaSecurityKey(_rsaKey!), SecurityAlgorithms.RsaSha256))
        },
        new Command
        {
            Name = "Private Key JWT (EC P-256)",
            Execute = ctx => RunFlowAsync(ctx, "client.jwt.ec",
                new SigningCredentials(new ECDsaSecurityKey(_ecKey!), SecurityAlgorithms.EcdsaSha256))
        },
        new Command
        {
            Name = "Private Key JWT (X.509)",
            Execute = ctx => RunFlowAsync(ctx, "client.jwt.x509",
                new X509SigningCredentials(_certificate!))
        }
    ];

    private async Task<ExecuteCommandResult> RunFlowAsync(CommandContext ctx, string clientId, SigningCredentials credentials)
    {
        var authority = _identityServer!.BuildUri().ToString().TrimEnd('/');
        var apiBase = _api!.BuildUri().ToString().TrimEnd('/');

        using var httpClient = ctx.HttpClientFactory.CreateClient();

        // 1. Discover endpoints
        var disco = await httpClient.GetDiscoveryDocumentAsync(authority);
        if (disco.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Discovery failed: {disco.Error}" };
        }

        // 2. Create a client assertion JWT
        var assertion = CreateClientAssertion(clientId, disco.Issuer!, credentials);

        // 3. Request token with client assertion
        var tokenResponse = await httpClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = disco.TokenEndpoint,
            ClientId = clientId,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
            Scope = "resource1.scope1",
            ClientAssertion = new ClientAssertion
            {
                Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                Value = assertion
            }
        });

        if (tokenResponse.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Token request failed: {tokenResponse.Error}" };
        }

        if (string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = "Access token is null or empty" };
        }

        // 4. Call the protected API
        using var apiRequest = new HttpRequestMessage(HttpMethod.Get, $"{apiBase}/identity");
        apiRequest.SetBearerToken(tokenResponse.AccessToken!);

        var apiResponse = await httpClient.SendAsync(apiRequest);
        if (!apiResponse.IsSuccessStatusCode)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"API call failed: {apiResponse.StatusCode}" };
        }

        return CommandResults.Success();
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
