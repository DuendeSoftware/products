// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text.Json;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.DPoP;
using Duende.AspNetCore.Authentication.JwtBearer.DPoP;
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
/// Scenario: Client Credentials flow with DPoP (Demonstrating Proof of Possession).
/// Uses Duende.AccessTokenManagement to handle DPoP proof generation, token caching,
/// and automatic proof creation for API calls — the same integration path customers use.
/// </summary>
public sealed class ClientCredentialsDPoP : IScenario
{
    private IdentityServerTestHost? _identityServer;
    private ApiHost? _api;

    public string Name => "ClientCredentialsDPoP";
    public string Description => "Client Credentials + DPoP: obtain DPoP-bound token, call protected API";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        // 1. Start IdentityServer
        _identityServer = new IdentityServerTestHost(configurator, "identity-server");
        _identityServer.AddDefaultUsers();
        _identityServer.AddDefaultResources();
        await _identityServer.StartAsync(ct);

        var authority = _identityServer.BuildUri().ToString().TrimEnd('/');

        // 2. Start the DPoP-enabled API
        _api = new ApiHost(configurator, "api", authority,
            configureServices: services =>
            {
                services.AddDistributedMemoryCache();
                services.ConfigureDPoPTokensForScheme("token", options =>
                {
                    options.EnableReplayDetection = false;
                });
            });
        await _api.StartAsync(ct);

        // 3. Register client
        _identityServer.AddClient(new Client
        {
            ClientId = "dpop-cc-client",
            ClientSecrets = [new Secret("secret".Sha256())],
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            RequireDPoP = true,
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
    }

    public Command[] GetCommands() =>
    [
        new Command
        {
            Name = "Run Client Credentials DPoP Flow",
            Execute = RunClientCredentialsDPoPAsync
        }
    ];

    private async Task<ExecuteCommandResult> RunClientCredentialsDPoPAsync(CommandContext ctx)
    {
        var authority = _identityServer!.BuildUri().ToString().TrimEnd('/');
        var apiBase = _api!.BuildUri().ToString().TrimEnd('/');

        // 1. Discover token endpoint
        using var discoClient = ctx.HttpClientFactory.CreateClient();
        var disco = await discoClient.GetDiscoveryDocumentAsync(authority);
        if (disco.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Discovery failed: {disco.Error}" };
        }

        // 2. Build a service provider with AccessTokenManagement configured for DPoP
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddClientCredentialsTokenManagement()
            .AddClient("dpop-client", client =>
            {
                client.TokenEndpoint = new Uri(disco.TokenEndpoint!);
                client.ClientId = ClientId.Parse("dpop-cc-client");
                client.ClientSecret = ClientSecret.Parse("secret");
                client.DPoPJsonWebKey = CreateDPoPKey();
            });

        services.AddClientCredentialsHttpClient("dpop-api", ClientCredentialsClientName.Parse("dpop-client"), config =>
        {
            config.BaseAddress = new Uri(apiBase);
        });

        await using var sp = services.BuildServiceProvider();

        // 3. Call the DPoP-protected API using the managed HTTP client
        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("dpop-api");
        var response = await client.GetAsync("/identity");

        if (!response.IsSuccessStatusCode)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"API call failed with status {response.StatusCode}" };
        }

        var body = await response.Content.ReadAsStringAsync();
        if (!body.Contains("client_id"))
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = "API response missing expected claims" };
        }

        return CommandResults.Success();
    }

    private static DPoPProofKey CreateDPoPKey()
    {
        var key = new RsaSecurityKey(RSA.Create(2048));
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        jwk.Alg = "PS256";
        var jwkJson = JsonSerializer.Serialize(jwk);

        return DPoPProofKey.Parse(jwkJson);
    }
}
