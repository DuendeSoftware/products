// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.Api;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;
using Microsoft.AspNetCore.Builder;

namespace Duende.IdentityServer.Interaction.Scenarios.ConsoleFlows;

/// <summary>
/// Scenario that tests the Client Credentials flow with credentials sent in the POST body
/// rather than in the Authorization header.
/// </summary>
public sealed class ConsoleClientCredentialsPostBody : IScenario
{
    private IdentityServerTestHost? _identityServer;
    private ApiHost? _api;

    public string Name => "ConsoleClientCredentialsPostBody";
    public string Description => "Client Credentials flow with credentials in POST body";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        // 1. Start IdentityServer
        _identityServer = new IdentityServerTestHost(configurator, "identity-server");
        _identityServer.AddDefaultResources();
        await _identityServer.StartAsync(ct);

        var authority = _identityServer.BuildUri().ToString().TrimEnd('/');

        // 2. Start the API
        _api = new ApiHost(configurator, "api", authority);
        _api.App.MapGet("/", () => "api");
        await _api.StartAsync(ct);

        // 3. Register the client credentials client
        _identityServer.AddClient(new Client
        {
            ClientId = "client",
            ClientSecrets = [new Secret("secret".Sha256())],
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = ["resource1.scope1", "resource1.scope2"]
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
            Name = "Run Client Credentials Flow (PostBody)",
            Execute = RunClientCredentialsFlowPostBodyAsync
        }
    ];

    private async Task<ExecuteCommandResult> RunClientCredentialsFlowPostBodyAsync(CommandContext ctx)
    {
        var authority = _identityServer!.BuildUri().ToString().TrimEnd('/');
        var apiBase = _api!.BuildUri().ToString().TrimEnd('/');

        using var client = ctx.HttpClientFactory.CreateClient();

        // 1. Discovery
        var disco = await client.GetDiscoveryDocumentAsync(authority);
        if (disco.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Discovery failed: {disco.Error}" };
        }

        // 2. Request token with credentials in POST body
        var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = disco.TokenEndpoint,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
            ClientId = "client",
            ClientSecret = "secret",
            Scope = "resource1.scope1"
        });

        if (tokenResponse.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Token request failed: {tokenResponse.Error}" };
        }

        // 3. Call the protected API
        using var apiRequest = new HttpRequestMessage(HttpMethod.Get, $"{apiBase}/identity");
        apiRequest.SetBearerToken(tokenResponse.AccessToken!);

        var apiResponse = await client.SendAsync(apiRequest);
        if (!apiResponse.IsSuccessStatusCode)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"API call failed: {apiResponse.StatusCode}" };
        }

        return CommandResults.Success();
    }
}
