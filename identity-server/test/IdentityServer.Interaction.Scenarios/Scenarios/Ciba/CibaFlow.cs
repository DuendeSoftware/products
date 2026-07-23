// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.Api;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Interaction.Tests.Infrastructure;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using Shouldly;

namespace Duende.IdentityServer.Interaction.Scenarios.Ciba;

/// <summary>
/// Scenario: CIBA (Client Initiated Backchannel Authentication) flow.
/// A console client initiates a backchannel auth request, the user approves it
/// via the IdentityServer CIBA UI, and the client polls for the token.
/// </summary>
public sealed class CibaFlow : IScenario
{
    private IdentityServerTestHost? _identityServer;
    private ApiHost? _api;

    public string Name => "CibaFlow";
    public string Description => "CIBA: client initiates backchannel auth, user approves via IS UI, client polls for token";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        // 1. Start IdentityServer
        _identityServer = new IdentityServerTestHost(configurator, "identity-server");
        _identityServer.AddDefaultUsers();
        _identityServer.AddDefaultResources();
        await _identityServer.StartAsync(ct);

        // 2. Start the API
        _api = new ApiHost(configurator, "api", _identityServer.BuildUri().ToString().TrimEnd('/'));
        await _api.StartAsync(ct);

        // 3. Register the CIBA client
        _identityServer.AddClient(new Client
        {
            ClientId = "ciba",
            ClientName = "CIBA Client",
            ClientSecrets = [new Secret("secret".Sha256())],
            AllowedGrantTypes = GrantTypes.Ciba,
            RequireConsent = true,
            AllowOfflineAccess = true,
            AllowedScopes =
            [
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                IdentityServerConstants.StandardScopes.Email,
                "resource1.scope1",
                "resource2.scope1"
            ]
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

    public Command[] GetCommands() => [new Command()
    {
        Name = "Trigger Ciba Flow",
        Execute = async (c) =>
        {
            var httpClient = c.HttpClientFactory.CreateClient();
            
            // 1. Discover endpoints
            var disco = await httpClient.GetDiscoveryDocumentAsync(_identityServer.BuildUri().ToString().TrimEnd('/'));
            disco.IsError.ShouldBeFalse(disco.Error);

            // 2. Initiate backchannel authentication request
            var bindingMessage = Guid.NewGuid().ToString("N")[..10];
            var cibaResponse = await httpClient.RequestBackchannelAuthenticationAsync(new BackchannelAuthenticationRequest
            {
                Address = disco.BackchannelAuthenticationEndpoint,
                ClientId = "ciba",
                ClientSecret = "secret",
                Scope = "openid profile resource1.scope1",
                LoginHint = "alice",
                BindingMessage = bindingMessage
            });
            cibaResponse.IsError.ShouldBeFalse(cibaResponse.Error);
            cibaResponse.AuthenticationRequestId.ShouldNotBeNullOrEmpty();

            return CommandResults.Success();
        }
    }];

    public class Tests(ScenarioFixture<CibaFlow> fixture) : PageTest, IClassFixture<ScenarioFixture<CibaFlow>>
    {
        private static readonly HttpClientHandler Handler = new()
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        public override BrowserNewContextOptions ContextOptions() => new()
        {
            IgnoreHTTPSErrors = true
        };

        [Fact]
        public async Task Full_ciba_flow_with_user_approval()
        {
            var scenario = (CibaFlow)fixture.Scenario;
            var authority = scenario._identityServer!.BuildUri().ToString().TrimEnd('/');
            var apiBase = scenario._api!.BuildUri().ToString().TrimEnd('/');

            using var httpClient = new HttpClient(Handler, disposeHandler: false);

            // 1. Discover endpoints
            var disco = await httpClient.GetDiscoveryDocumentAsync(authority);
            disco.IsError.ShouldBeFalse(disco.Error);

            // 2. Initiate backchannel authentication request
            var bindingMessage = Guid.NewGuid().ToString("N")[..10];
            var cibaResponse = await httpClient.RequestBackchannelAuthenticationAsync(new BackchannelAuthenticationRequest
            {
                Address = disco.BackchannelAuthenticationEndpoint,
                ClientId = "ciba",
                ClientSecret = "secret",
                Scope = "openid profile resource1.scope1",
                LoginHint = "alice",
                BindingMessage = bindingMessage
            });
            cibaResponse.IsError.ShouldBeFalse(cibaResponse.Error);
            cibaResponse.AuthenticationRequestId.ShouldNotBeNullOrEmpty();

            // 3. User logs in to IdentityServer and approves the CIBA request via UI
            var isUrl = scenario._identityServer!.BuildUri().ToString();

            // Login as alice
            await Page.GotoAsync(isUrl + "Account/Login");
            await Page.WaitForSelectorAsync("input[placeholder='Username']");
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

            // Navigate to CIBA pending requests
            await Page.GotoAsync(isUrl + "Ciba/All");
            await Page.WaitForSelectorAsync("text=Pending Backchannel Login Requests");

            // Click "Process" on the pending request
            await Page.GetByRole(AriaRole.Link, new() { Name = "Process" }).First.ClickAsync();

            // Consent page — approve with "Yes, Allow"
            await Page.WaitForSelectorAsync("text=is requesting your permission");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Yes, Allow" }).ClickAsync();

            // 4. Poll the token endpoint until the token is issued
            TokenResponse? tokenResponse = null;
            for (var i = 0; i < 10; i++)
            {
                tokenResponse = await httpClient.RequestBackchannelAuthenticationTokenAsync(
                    new BackchannelAuthenticationTokenRequest
                    {
                        Address = disco.TokenEndpoint,
                        ClientId = "ciba",
                        ClientSecret = "secret",
                        AuthenticationRequestId = cibaResponse.AuthenticationRequestId
                    });

                if (!tokenResponse.IsError)
                {
                    break;
                }

                if (tokenResponse.Error == OidcConstants.TokenErrors.AuthorizationPending ||
                    tokenResponse.Error == OidcConstants.TokenErrors.SlowDown)
                {
                    await Task.Delay(1000);
                    continue;
                }

                // Unexpected error
                tokenResponse.IsError.ShouldBeFalse(tokenResponse.Error);
            }

            tokenResponse.ShouldNotBeNull();
            tokenResponse.IsError.ShouldBeFalse(tokenResponse!.Error);
            tokenResponse.AccessToken.ShouldNotBeNullOrEmpty();

            // 5. Call the protected API with the access token
            using var apiRequest = new HttpRequestMessage(HttpMethod.Get, $"{apiBase}/identity");
            apiRequest.SetBearerToken(tokenResponse.AccessToken!);

            var apiResponse = await httpClient.SendAsync(apiRequest);
            apiResponse.IsSuccessStatusCode.ShouldBeTrue($"API returned {apiResponse.StatusCode}");

            var body = await apiResponse.Content.ReadAsStringAsync();
            body.ShouldContain("sub");
        }
    }
}
