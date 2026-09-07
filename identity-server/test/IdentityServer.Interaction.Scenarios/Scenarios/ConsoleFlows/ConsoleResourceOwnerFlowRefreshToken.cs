// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.Api;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;

namespace Duende.IdentityServer.Interaction.Scenarios.ConsoleFlows;

/// <summary>
/// Scenario that starts IdentityServer + a protected API for testing the Resource Owner
/// Password flow with refresh-token rotation.
/// </summary>
public sealed class ConsoleResourceOwnerFlowRefreshToken : IScenario
{
    private IdentityServerTestHost? _identityServer;
    private ApiHost? _api;

    public string Name => "ConsoleResourceOwnerFlowRefreshToken";
    public string Description => "Resource Owner Password flow with refresh tokens: obtain token, refresh repeatedly, call protected API";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        _identityServer = new IdentityServerTestHost(configurator, "identity-server");
        _identityServer.AddDefaultUsers();
        _identityServer.AddDefaultResources();
        await _identityServer.StartAsync(ct);

        var authority = _identityServer.BuildUri().ToString().TrimEnd('/');

        _api = new ApiHost(configurator, "api", authority);
        await _api.StartAsync(ct);

        _identityServer.AddClient(new Client
        {
            ClientId = "roclient",
            ClientSecrets = [new Secret("secret".Sha256())],
            AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
            AllowOfflineAccess = true,
            AllowedScopes =
            [
                IdentityServerConstants.StandardScopes.OpenId,
                "custom.profile",
                "resource1.scope1",
                "resource2.scope1"
            ],
            RefreshTokenUsage = TokenUsage.OneTimeOnly,
            AbsoluteRefreshTokenLifetime = 3600 * 24,
            SlidingRefreshTokenLifetime = 10,
            RefreshTokenExpiration = TokenExpiration.Sliding
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
            Name = "Run Resource Owner Refresh Token Flow",
            Execute = RunFlowAsync
        }
    ];

    private async Task<ExecuteCommandResult> RunFlowAsync(CommandContext ctx)
    {
        var authority = _identityServer!.BuildUri().ToString().TrimEnd('/');
        var apiBase = _api!.BuildUri().ToString().TrimEnd('/');

        using var client = ctx.HttpClientFactory.CreateClient();

        var disco = await client.GetDiscoveryDocumentAsync(authority);
        if (disco.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Discovery failed: {disco.Error}" };
        }

        var tokenResponse = await client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = disco.TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",
            UserName = "bob",
            Password = "bob",
            Scope = "resource1.scope1 offline_access"
        });

        if (tokenResponse.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Token request failed: {tokenResponse.Error}" };
        }

        if (string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = "Token request did not return a refresh token." };
        }

        using var initialApiRequest = new HttpRequestMessage(HttpMethod.Get, $"{apiBase}/identity");
        initialApiRequest.SetBearerToken(tokenResponse.AccessToken!);

        var initialApiResponse = await client.SendAsync(initialApiRequest);
        if (!initialApiResponse.IsSuccessStatusCode)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Initial API call failed: {initialApiResponse.StatusCode}" };
        }

        var refreshToken = tokenResponse.RefreshToken;
        for (var i = 0; i < 3; i++)
        {
            var refreshResponse = await client.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = disco.TokenEndpoint,
                ClientId = "roclient",
                ClientSecret = "secret",
                RefreshToken = refreshToken
            });

            if (refreshResponse.IsError)
            {
                return new ExecuteCommandResult { Success = false, ErrorMessage = $"Refresh token request failed: {refreshResponse.Error}" };
            }

            if (string.IsNullOrWhiteSpace(refreshResponse.RefreshToken))
            {
                return new ExecuteCommandResult { Success = false, ErrorMessage = "Refresh response did not include a new refresh token." };
            }

            if (refreshResponse.RefreshToken == refreshToken)
            {
                return new ExecuteCommandResult { Success = false, ErrorMessage = "Refresh response did not rotate the refresh token." };
            }

            refreshToken = refreshResponse.RefreshToken;

            using var apiRequest = new HttpRequestMessage(HttpMethod.Get, $"{apiBase}/identity");
            apiRequest.SetBearerToken(refreshResponse.AccessToken!);

            var apiResponse = await client.SendAsync(apiRequest);
            if (!apiResponse.IsSuccessStatusCode)
            {
                return new ExecuteCommandResult { Success = false, ErrorMessage = $"API call failed: {apiResponse.StatusCode}" };
            }
        }

        return CommandResults.Success();
    }
}
