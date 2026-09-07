// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace Duende.IdentityServer.Interaction.Scenarios.ConsoleFlows;

/// <summary>
/// Scenario that starts IdentityServer with Local API authentication enabled, then uses
/// client credentials to obtain tokens and call the IdentityServer's own local API endpoint.
/// Tests both JWT and reference access tokens, plus the unauthenticated (failure) case.
/// </summary>
public sealed class ConsoleClientCredentialsCallingIdentityServerApi : IScenario
{
    private IdentityServerTestHost? _identityServer;

    public string Name => "ConsoleClientCredentialsCallingIdentityServerApi";

    public string Description =>
        "Client Credentials flow calling IdentityServer's local API with JWT and reference tokens";

    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        _identityServer = new IdentityServerTestHost(
            configurator,
            "identity-server",
            configureServices: services =>
            {
                services.AddAuthentication()
                    .AddLocalApi(options =>
                    {
                        options.ExpectedScope = IdentityServerConstants.LocalApi.ScopeName;
                    });

                services.AddAuthorization(options =>
                {
                    options.AddPolicy(IdentityServerConstants.LocalApi.PolicyName, policy =>
                    {
                        policy.AddAuthenticationSchemes(IdentityServerConstants.LocalApi.AuthenticationScheme);
                        policy.RequireAuthenticatedUser();
                    });
                });
            });

        _identityServer.SetApiScopes([new ApiScope(IdentityServerConstants.LocalApi.ScopeName)]);

        // Map a local API endpoint on IdentityServer itself
        _identityServer.App.MapGet("/localApi", (HttpContext c) =>
        {
            var claims = c.User.Claims.Select(claim => new { claim.Type, claim.Value });
            return Results.Ok(claims);
        }).RequireAuthorization(IdentityServerConstants.LocalApi.PolicyName);

        // Register clients that can request the IdentityServerApi scope
        _identityServer.AddClient(new Client
        {
            ClientId = "client",
            ClientSecrets = [new Secret("secret".Sha256())],
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = [IdentityServerConstants.LocalApi.ScopeName]
        });

        _identityServer.AddClient(new Client
        {
            ClientId = "client.reference",
            ClientSecrets = [new Secret("secret".Sha256())],
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = [IdentityServerConstants.LocalApi.ScopeName],
            AccessTokenType = AccessTokenType.Reference
        });

        await _identityServer.StartAsync(ct);

        Links = [_identityServer.Link];
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_identityServer != null)
        {
            await _identityServer.DisposeAsync();
        }
    }

    public Command[] GetCommands() =>
    [
        new Command
        {
            Name = "JWT Access Token",
            Execute = ctx => CallLocalApiAsync(ctx, "client")
        },
        new Command
        {
            Name = "Reference Access Token",
            Execute = ctx => CallLocalApiAsync(ctx, "client.reference")
        },
        new Command
        {
            Name = "No Access Token (expect failure)",
            Execute = CallWithoutTokenAsync
        }
    ];

    private async Task<ExecuteCommandResult> CallLocalApiAsync(CommandContext ctx, string clientId)
    {
        var authority = _identityServer!.BuildUri().ToString().TrimEnd('/');

        using var client = ctx.HttpClientFactory.CreateClient();

        // 1. Discovery
        var disco = await client.GetDiscoveryDocumentAsync(authority);
        if (disco.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Discovery failed: {disco.Error}" };
        }

        // 2. Request token
        var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = disco.TokenEndpoint,
            ClientId = clientId,
            ClientSecret = "secret",
            Scope = IdentityServerConstants.LocalApi.ScopeName
        });

        if (tokenResponse.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Token request failed: {tokenResponse.Error}" };
        }

        // 3. Call the local API on IdentityServer
        using var apiRequest = new HttpRequestMessage(HttpMethod.Get, $"{authority}/localApi");
        apiRequest.SetBearerToken(tokenResponse.AccessToken!);

        var apiResponse = await client.SendAsync(apiRequest);
        if (!apiResponse.IsSuccessStatusCode)
        {
            return new ExecuteCommandResult
            {
                Success = false,
                ErrorMessage = $"Local API call failed: {apiResponse.StatusCode}"
            };
        }

        return CommandResults.Success();
    }

    private async Task<ExecuteCommandResult> CallWithoutTokenAsync(CommandContext ctx)
    {
        var authority = _identityServer!.BuildUri().ToString().TrimEnd('/');

        using var client = ctx.HttpClientFactory.CreateClient();

        // Call without any token - should get 401
        var response = await client.GetAsync($"{authority}/localApi");

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return CommandResults.Success();
        }

        return new ExecuteCommandResult
        {
            Success = false,
            ErrorMessage = $"Expected 401 Unauthorized but got {response.StatusCode}"
        };
    }
}
