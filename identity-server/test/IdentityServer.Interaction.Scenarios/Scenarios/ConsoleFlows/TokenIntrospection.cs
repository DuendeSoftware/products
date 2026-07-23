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
/// Scenario: Token Introspection with reference tokens.
/// A client obtains a reference (opaque) access token via the resource owner password grant,
/// then a separate API resource introspects it using the introspection endpoint to verify
/// that the token is active and retrieve its claims. Both JSON and JWT response formats
/// are exercised.
/// </summary>
public sealed class TokenIntrospection : IScenario
{
    private IdentityServerTestHost? _identityServer;
    private ApiHost? _api;

    public string Name => "TokenIntrospection";
    public string Description => "Reference token introspection by API resource (JSON + JWT formats)";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        // 1. Start IdentityServer
        _identityServer = new IdentityServerTestHost(configurator, "identity-server");
        _identityServer.AddDefaultUsers();
        _identityServer.AddDefaultResources();

        // Override API resources to add a secret to urn:resource1 so it can call introspection
        _identityServer.SetApiResources(
        [
            new ApiResource("urn:resource1", "Resource 1")
            {
                Scopes = { "resource1.scope1", "resource1.scope2" },
                ApiSecrets = { new Secret("secret".Sha256()) }
            },
            new ApiResource("urn:resource2", "Resource 2")
            {
                Scopes = { "resource2.scope1", "resource2.scope2" }
            }
        ]);

        await _identityServer.StartAsync(ct);

        var authority = _identityServer.BuildUri().ToString().TrimEnd('/');

        // 2. Start the API
        _api = new ApiHost(configurator, "api", authority);
        await _api.StartAsync(ct);

        // 3. Register the client that issues reference tokens
        _identityServer.AddClient(new Client
        {
            ClientId = "roclient.reference",
            ClientSecrets = [new Secret("secret".Sha256())],
            AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
            AllowedScopes = ["resource1.scope1", "resource2.scope1"],
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
            Name = "Introspect (JSON)",
            Execute = ctx => RunTokenIntrospectionAsync(ctx, ResponseFormat.Json)
        },
        new Command
        {
            Name = "Introspect (JWT)",
            Execute = ctx => RunTokenIntrospectionAsync(ctx, ResponseFormat.Jwt)
        }
    ];

    private async Task<ExecuteCommandResult> RunTokenIntrospectionAsync(CommandContext ctx, ResponseFormat responseFormat)
    {
        var authority = _identityServer!.BuildUri().ToString().TrimEnd('/');

        using var httpClient = ctx.HttpClientFactory.CreateClient();

        // 1. Discover endpoints
        var disco = await httpClient.GetDiscoveryDocumentAsync(authority);
        if (disco.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Discovery failed: {disco.Error}" };
        }

        // 2. Obtain a reference token via resource owner password grant
        var tokenResponse = await httpClient.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = disco.TokenEndpoint,
            ClientId = "roclient.reference",
            ClientSecret = "secret",
            UserName = "bob",
            Password = "bob",
            Scope = "resource1.scope1 resource2.scope1"
        });

        if (tokenResponse.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Token request failed: {tokenResponse.Error}" };
        }

        if (string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = "Access token is null or empty" };
        }

        // 3. Introspect using the API resource as the caller (not the token-issuing client)
        var introspectionResponse = await httpClient.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = disco.IntrospectionEndpoint,
            ClientId = "urn:resource1",
            ClientSecret = "secret",
            Token = tokenResponse.AccessToken!,
            ResponseFormat = responseFormat
        });

        if (introspectionResponse.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Introspection failed: {introspectionResponse.Error}" };
        }

        if (!introspectionResponse.IsActive)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = "Token should be active but is not" };
        }

        // 4. Verify expected claims are present
        var claims = introspectionResponse.Claims.ToList();
        if (!claims.Any(c => c.Type == "client_id" && c.Value == "roclient.reference"))
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = "Introspection response missing expected client_id claim" };
        }

        if (!claims.Any(c => c.Type == "sub"))
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = "Introspection response missing expected sub claim" };
        }

        if (responseFormat == ResponseFormat.Jwt && string.IsNullOrEmpty(introspectionResponse.Raw))
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = "JWT response format returned empty raw response" };
        }

        return CommandResults.Success();
    }
}
