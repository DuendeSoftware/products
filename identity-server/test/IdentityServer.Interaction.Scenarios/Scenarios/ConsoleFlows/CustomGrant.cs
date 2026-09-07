// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.Api;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Interaction.Scenarios.ConsoleFlows;

/// <summary>
/// Scenario: Custom extension grants with and without a subject.
/// Each grant obtains an access token and uses it to call a protected API.
/// </summary>
public sealed class CustomGrant : IScenario
{
    private const string ClientId = "client.custom";
    private const string ClientSecret = "secret";

    private IdentityServerTestHost? _identityServer;
    private ApiHost? _api;

    public string Name => "CustomGrant";
    public string Description => "Custom extension grants: obtain tokens with and without a subject, then call a protected API";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        _identityServer = new IdentityServerTestHost(
            configurator,
            "identity-server",
            configureServices: services =>
            {
                services.AddSingleton<IExtensionGrantValidator>(new CustomGrantValidator("custom", "1"));
                services.AddSingleton<IExtensionGrantValidator>(new CustomGrantValidator("custom.nosubject", subject: null));
            });
        _identityServer.AddDefaultUsers();
        ConfigureResources(_identityServer);
        _identityServer.AddClient(new Client
        {
            ClientId = ClientId,
            ClientSecrets = [new Secret(ClientSecret.Sha256())],
            AllowedGrantTypes = ["custom", "custom.nosubject"],
            AllowedScopes = ["resource1.scope1", "resource2.scope1"]
        });
        await _identityServer.StartAsync(ct);

        var authority = _identityServer.BuildUri().ToString().TrimEnd('/');

        _api = new ApiHost(configurator, "api", authority);
        await _api.StartAsync(ct);

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
            Name = "Run Custom Grant (Subject)",
            Execute = ctx => RunFlowAsync(ctx, "custom", expectedSubject: "1")
        },
        new Command
        {
            Name = "Run Custom Grant (No Subject)",
            Execute = ctx => RunFlowAsync(ctx, "custom.nosubject", expectedSubject: null)
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

    private async Task<ExecuteCommandResult> RunFlowAsync(
        CommandContext ctx,
        string grantType,
        string? expectedSubject)
    {
        var authority = _identityServer!.BuildUri().ToString().TrimEnd('/');
        var apiBase = _api!.BuildUri().ToString().TrimEnd('/');

        using var client = ctx.HttpClientFactory.CreateClient();

        var discovery = await client.GetDiscoveryDocumentAsync(authority);
        if (discovery.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Discovery failed: {discovery.Error}" };
        }

        var tokenResponse = await client.RequestTokenAsync(new TokenRequest
        {
            Address = discovery.TokenEndpoint,
            GrantType = grantType,
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            Parameters =
            {
                { "scope", "resource1.scope1" },
                { "custom_credential", "custom credential" }
            }
        });

        if (tokenResponse.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Token request failed: {tokenResponse.Error}" };
        }

        using var apiRequest = new HttpRequestMessage(HttpMethod.Get, $"{apiBase}/identity");
        apiRequest.SetBearerToken(tokenResponse.AccessToken!);

        var apiResponse = await client.SendAsync(apiRequest);
        if (!apiResponse.IsSuccessStatusCode)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"API call failed: {apiResponse.StatusCode}" };
        }

        var responseContent = await apiResponse.Content.ReadAsStringAsync();
        using var claims = JsonDocument.Parse(responseContent);
        var subject = claims.RootElement
            .EnumerateArray()
            .FirstOrDefault(claim => claim.GetProperty("type").GetString() == "sub");
        var actualSubject = subject.ValueKind == JsonValueKind.Undefined
            ? null
            : subject.GetProperty("value").GetString();

        if (actualSubject != expectedSubject)
        {
            return new ExecuteCommandResult
            {
                Success = false,
                ErrorMessage = $"Expected subject '{expectedSubject ?? "<none>"}' but received '{actualSubject ?? "<none>"}'."
            };
        }

        return CommandResults.Success();
    }

    private sealed class CustomGrantValidator(string grantType, string? subject) : IExtensionGrantValidator
    {
        public string GrantType => grantType;

        public Task ValidateAsync(ExtensionGrantValidationContext context, Ct ct)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Request.Raw.Get("custom_credential") == null)
            {
                context.Result = new GrantValidationResult(
                    TokenRequestErrors.InvalidGrant,
                    "invalid custom credential");
            }
            else if (subject == null)
            {
                context.Result = new GrantValidationResult();
            }
            else
            {
                context.Result = new GrantValidationResult(subject, authenticationMethod: "custom");
            }

            return Task.CompletedTask;
        }
    }
}
