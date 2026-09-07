// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Text.Json;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.Api;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Interaction.Scenarios.ConsoleFlows;

/// <summary>
/// Exercises a client credentials request with a parameterized transaction scope and verifies
/// that the parsed transaction value is emitted as a client claim in the access token.
/// </summary>
public sealed class ParameterizedScope : IScenario
{
    private const string ClientId = "parameterized.client";
    private const string ClientSecret = "secret";

    private IdentityServerTestHost? _identityServer;
    private ApiHost? _api;
    private string? _authority;
    private string? _apiBase;

    public string Name => "ParameterizedScope";
    public string Description => "Client credentials flow: request a parameterized scope and expose its value as a token claim";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        _identityServer = new IdentityServerTestHost(
            configurator,
            "identity-server",
            builder => builder
                .AddScopeParser<ScenarioParameterizedScopeParser>()
                .AddCustomTokenRequestValidator<ScenarioParameterizedScopeTokenRequestValidator>());
        _identityServer.SetApiScopes(
        [
            new ApiScope("transaction", "Transaction")
            {
                Description = "Some Transaction"
            }
        ]);
        _identityServer.AddClient(new Client
        {
            ClientId = ClientId,
            ClientSecrets = { new Secret(ClientSecret.Sha256()) },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = { "transaction" }
        });
        await _identityServer.StartAsync(ct);

        _authority = BuildLoopbackUri(_identityServer).ToString().TrimEnd('/');
        _api = new ApiHost(configurator, "api", _authority);
        await _api.StartAsync(ct);
        _apiBase = BuildLoopbackUri(_api).ToString().TrimEnd('/');

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
            Name = "Run Parameterized Scope Flow",
            Execute = RunFlowAsync
        }
    ];

    private async Task<ExecuteCommandResult> RunFlowAsync(CommandContext ctx)
    {
        var authority = _authority ?? throw new InvalidOperationException("Scenario has not been started.");
        var apiBase = _apiBase ?? throw new InvalidOperationException("Scenario has not been started.");

        using var client = ctx.HttpClientFactory.CreateClient();
        var discovery = await client.GetDiscoveryDocumentAsync(authority);
        if (discovery.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Discovery failed: {discovery.Error}" };
        }

        var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = discovery.TokenEndpoint,
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            Scope = "transaction:123"
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

        var claimsJson = await apiResponse.Content.ReadAsStringAsync();
        using var claims = JsonDocument.Parse(claimsJson);
        var hasTransactionClaim = claims.RootElement.EnumerateArray().Any(claim =>
            claim.GetProperty("type").GetString() == "client_transaction" &&
            claim.GetProperty("value").GetString() == "123");
        if (!hasTransactionClaim)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = "API response missing client_transaction claim with value '123'" };
        }

        return CommandResults.Success();
    }

    private static Uri BuildLoopbackUri(TestHost host)
    {
        var uri = host.BuildUri();
        return new UriBuilder(uri)
        {
            Host = "127.0.0.1"
        }.Uri;
    }
}

public sealed class ScenarioParameterizedScopeParser(ILogger<DefaultScopeParser> logger) : DefaultScopeParser(logger)
{
    public override void ParseScopeValue(ParseScopeContext scopeContext)
    {
        ArgumentNullException.ThrowIfNull(scopeContext);
        const string transactionScopeName = "transaction";
        const string transactionScopePrefix = transactionScopeName + ":";

        if (scopeContext.RawValue.StartsWith(transactionScopePrefix, StringComparison.InvariantCulture))
        {
            var parts = scopeContext.RawValue.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                scopeContext.SetParsedValues(transactionScopeName, parts[1]);
            }
            else
            {
                scopeContext.SetError("transaction scope missing transaction parameter value");
            }
        }
        else if (scopeContext.RawValue == transactionScopeName)
        {
            scopeContext.SetIgnore();
        }
        else
        {
            base.ParseScopeValue(scopeContext);
        }
    }
}

public sealed class ScenarioParameterizedScopeTokenRequestValidator : ICustomTokenRequestValidator
{
    public Task ValidateAsync(CustomTokenRequestValidationContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var transaction = context.Result?.ValidatedRequest.ValidatedResources.ParsedScopes
            .FirstOrDefault(x => x.ParsedName == "transaction");
        if (transaction?.ParsedParameter != null)
        {
            context.Result?.ValidatedRequest.ClientClaims.Add(
                new Claim(transaction.ParsedName, transaction.ParsedParameter));
        }

        return Task.CompletedTask;
    }
}
