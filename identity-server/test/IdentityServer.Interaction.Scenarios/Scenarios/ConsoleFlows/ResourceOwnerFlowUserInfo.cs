// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;

namespace Duende.IdentityServer.Interaction.Scenarios.ConsoleFlows;

/// <summary>
/// Scenario: Resource Owner Password Credentials flow followed by a UserInfo request.
/// The client requests identity scopes for a user and retrieves the corresponding claims.
/// </summary>
public sealed class ResourceOwnerFlowUserInfo : IScenario
{
    private IdentityServerTestHost? _identityServer;

    public string Name => "ResourceOwnerFlowUserInfo";
    public string Description => "Resource Owner Password Credentials with UserInfo: obtain a token for identity scopes and retrieve user claims";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        _identityServer = new IdentityServerTestHost(configurator, "identity-server");
        _identityServer.AddDefaultUsers();
        _identityServer.AddDefaultResources();
        await _identityServer.StartAsync(ct);

        _identityServer.AddClient(new Client
        {
            ClientId = "roclient",
            ClientSecrets = [new Secret("secret".Sha256())],
            AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
            AllowedScopes =
            [
                IdentityServerConstants.StandardScopes.OpenId,
                "custom.profile"
            ]
        });

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
            Name = "Run Resource Owner Flow with UserInfo",
            Execute = RunFlowAsync
        }
    ];

    private async Task<ExecuteCommandResult> RunFlowAsync(CommandContext ctx)
    {
        var authority = _identityServer!.BuildUri().ToString().TrimEnd('/');

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
            Scope = "openid custom.profile"
        });

        if (tokenResponse.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Token request failed: {tokenResponse.Error}" };
        }

        var userInfoResponse = await client.GetUserInfoAsync(new UserInfoRequest
        {
            Address = disco.UserInfoEndpoint,
            Token = tokenResponse.AccessToken!
        });

        if (userInfoResponse.IsError)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"UserInfo request failed: {userInfoResponse.Error}" };
        }

        (string Type, string Value)[] expectedClaims =
        [
            (JwtClaimTypes.Subject, "2"),
            (JwtClaimTypes.Name, "Bob Smith"),
            (JwtClaimTypes.Email, "BobSmith@example.com"),
            (JwtClaimTypes.Address, """{"street_address":"One Hacker Way","locality":"Heidelberg","postal_code":"69118","country":"Germany"}""")
        ];

        foreach (var expectedClaim in expectedClaims)
        {
            if (!userInfoResponse.Claims.Any(claim =>
                claim.Type == expectedClaim.Type && claim.Value == expectedClaim.Value))
            {
                return new ExecuteCommandResult
                {
                    Success = false,
                    ErrorMessage = $"UserInfo response did not contain the expected '{expectedClaim.Type}' claim."
                };
            }
        }

        return CommandResults.Success();
    }
}
