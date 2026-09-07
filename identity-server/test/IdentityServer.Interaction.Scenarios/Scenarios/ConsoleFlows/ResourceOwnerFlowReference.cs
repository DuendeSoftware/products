// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.Api;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;

namespace Duende.IdentityServer.Interaction.Scenarios.ConsoleFlows;

/// <summary>
/// Scenario: Resource Owner Password Credentials flow with a reference access token.
/// The protected API validates the opaque token through the introspection endpoint.
/// </summary>
public sealed class ResourceOwnerFlowReference : IScenario
{
    private IdentityServerTestHost? _identityServer;
    private ApiHost? _api;

    public string Name => "ResourceOwnerFlowReference";
    public string Description => "Resource Owner Password Credentials: obtain a reference token and call an introspection-enabled API";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        _identityServer = new IdentityServerTestHost(configurator, "identity-server");
        _identityServer.AddDefaultUsers();
        ConfigureResources(_identityServer);
        await _identityServer.StartAsync(ct);

        var authority = _identityServer.BuildUri().ToString().TrimEnd('/');

        _api = new ApiHost(
            configurator,
            "api",
            authority,
            audience: "urn:resource1",
            introspectionClientId: "urn:resource1",
            introspectionClientSecret: "secret");
        await _api.StartAsync(ct);

        _identityServer.AddClient(new Client
        {
            ClientId = "roclient.reference",
            ClientSecrets = [new Secret("secret".Sha256())],
            AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
            AllowedScopes = ["resource1.scope1", "resource2.scope1", "scope3"],
            AccessTokenType = AccessTokenType.Reference
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
            Name = "Run Resource Owner Flow (Reference Token)",
            Execute = RunFlowAsync
        }
    ];

    private static void ConfigureResources(IdentityServerTestHost identityServer)
    {
        identityServer.SetIdentityResources(
        [
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email(),
            new IdentityResource(
                "custom.profile",
                [JwtClaimTypes.Name, JwtClaimTypes.Email, "location", JwtClaimTypes.Address])
        ]);

        identityServer.SetApiScopes(
        [
            new ApiScope(IdentityServerConstants.LocalApi.ScopeName),
            new ApiScope("resource1.scope1"),
            new ApiScope("resource1.scope2"),
            new ApiScope("resource2.scope1"),
            new ApiScope("resource2.scope2"),
            new ApiScope("resource3.scope1"),
            new ApiScope("resource3.scope2"),
            new ApiScope("scope3"),
            new ApiScope("scope4"),
            new ApiScope("shared.scope"),
            new ApiScope("transaction", "Transaction")
            {
                Description = "Some Transaction"
            }
        ]);

        identityServer.SetApiResources(
        [
            new ApiResource("urn:resource1", "Resource 1")
            {
                Description = "Something very long and descriptive",
                ApiSecrets = { new Secret("secret".Sha256()) },
                Scopes = { "resource1.scope1", "resource1.scope2", "shared.scope" }
            },
            new ApiResource("urn:resource2", "Resource 2")
            {
                Description = "Something very long and descriptive",
                ApiSecrets = { new Secret("secret".Sha256()) },
                UserClaims = { JwtClaimTypes.Name, JwtClaimTypes.Email },
                Scopes = { "resource2.scope1", "resource2.scope2", "shared.scope" }
            },
            new ApiResource("urn:resource3", "Resource 3 (isolated)")
            {
                ApiSecrets = { new Secret("secret".Sha256()) },
                RequireResourceIndicator = true,
                Scopes = { "resource3.scope1", "resource3.scope2", "shared.scope" }
            }
        ]);
    }

    private async Task<ExecuteCommandResult> RunFlowAsync(CommandContext ctx)
    {
        var authority = _identityServer!.BuildUri().ToString().TrimEnd('/');
        var apiBase = _api!.BuildUri().ToString().TrimEnd('/');

        using var client = ctx.HttpClientFactory.CreateClient();

        var discovery = await client.GetDiscoveryDocumentAsync(authority);
        if (discovery.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Discovery failed: {discovery.Error}" };
        }

        var tokenResponse = await client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = discovery.TokenEndpoint,
            ClientId = "roclient.reference",
            ClientSecret = "secret",
            UserName = "bob",
            Password = "bob",
            Scope = "resource1.scope1 resource2.scope1 scope3"
        });

        if (tokenResponse.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Token request failed: {tokenResponse.Error}" };
        }

        if (string.IsNullOrEmpty(tokenResponse.AccessToken) || tokenResponse.AccessToken.Contains('.'))
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = "Token response did not contain a reference access token" };
        }

        using var apiRequest = new HttpRequestMessage(HttpMethod.Get, $"{apiBase}/identity");
        apiRequest.SetBearerToken(tokenResponse.AccessToken);

        var apiResponse = await client.SendAsync(apiRequest);
        if (!apiResponse.IsSuccessStatusCode)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"API call failed: {apiResponse.StatusCode}" };
        }

        return CommandResults.Success();
    }
}
