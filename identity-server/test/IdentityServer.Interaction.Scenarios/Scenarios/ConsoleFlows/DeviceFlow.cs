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

namespace Duende.IdentityServer.Interaction.Scenarios.ConsoleFlows;

/// <summary>
/// Scenario: Device Authorization Flow.
/// A console client initiates a device authorization request, the user visits the
/// verification URI in a browser to log in and approve, then the client polls for the token.
/// </summary>
public sealed class DeviceFlow : IScenario
{
    private IdentityServerTestHost? _identityServer;
    private ApiHost? _api;

    public string Name => "DeviceFlow";
    public string Description => "Device Authorization flow: client polls while user authorizes via browser";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        // 1. Start IdentityServer
        _identityServer = new IdentityServerTestHost(configurator, "identity-server");
        _identityServer.AddDefaultUsers();
        _identityServer.AddDefaultResources();
        await _identityServer.StartAsync(ct);

        var authority = _identityServer.BuildUri().ToString().TrimEnd('/');

        // 2. Start the API
        _api = new ApiHost(configurator, "api", authority);
        await _api.StartAsync(ct);

        // 3. Register the device flow client
        _identityServer.AddClient(new Client
        {
            ClientId = "device",
            ClientName = "Device Flow Client",
            RequireClientSecret = false,
            AllowedGrantTypes = GrantTypes.DeviceFlow,
            RequireConsent = true,
            AllowOfflineAccess = true,
            AllowedScopes =
            [
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                "resource1.scope1"
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

    public Command[] GetCommands() => [];

    public class Tests(ScenarioFixture<DeviceFlow> fixture) : PageTest, IClassFixture<ScenarioFixture<DeviceFlow>>
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
        public async Task Full_device_flow_with_user_approval()
        {
            var scenario = (DeviceFlow)fixture.Scenario;
            var authority = scenario._identityServer!.BuildUri().ToString().TrimEnd('/');
            var apiBase = scenario._api!.BuildUri().ToString().TrimEnd('/');

            using var httpClient = new HttpClient(Handler, disposeHandler: false);

            // 1. Discover endpoints
            var disco = await httpClient.GetDiscoveryDocumentAsync(authority);
            disco.IsError.ShouldBeFalse(disco.Error);

            // 2. Initiate device authorization request
            var deviceResponse = await httpClient.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
            {
                Address = disco.DeviceAuthorizationEndpoint,
                ClientId = "device",
                Scope = "openid profile resource1.scope1"
            });
            deviceResponse.IsError.ShouldBeFalse(deviceResponse.Error);
            deviceResponse.DeviceCode.ShouldNotBeNullOrEmpty();
            deviceResponse.UserCode.ShouldNotBeNullOrEmpty();
            deviceResponse.VerificationUri.ShouldNotBeNullOrEmpty();

            // 3. User visits verification URI in a browser and approves
            var verificationUrl = deviceResponse.VerificationUriComplete ?? deviceResponse.VerificationUri;
            await Page.GotoAsync(verificationUrl!);

            // Login page
            await Page.WaitForSelectorAsync("input[placeholder='Username']");
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

            // Consent page — approve
            await Page.WaitForSelectorAsync("text=is requesting your permission");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Yes, Allow" }).ClickAsync();

            // Wait for success confirmation
            await Page.WaitForSelectorAsync("text=success");

            // 4. Poll the token endpoint
            TokenResponse? tokenResponse = null;
            for (var i = 0; i < 10; i++)
            {
                tokenResponse = await httpClient.RequestDeviceTokenAsync(new DeviceTokenRequest
                {
                    Address = disco.TokenEndpoint,
                    ClientId = "device",
                    DeviceCode = deviceResponse.DeviceCode
                });

                if (!tokenResponse.IsError)
                {
                    break;
                }

                if (tokenResponse.Error == OidcConstants.TokenErrors.AuthorizationPending ||
                    tokenResponse.Error == OidcConstants.TokenErrors.SlowDown)
                {
                    await Task.Delay(deviceResponse.Interval * 1000);
                    continue;
                }

                // Unexpected error
                tokenResponse.IsError.ShouldBeFalse(tokenResponse.Error);
            }

            tokenResponse.ShouldNotBeNull();
            tokenResponse!.IsError.ShouldBeFalse(tokenResponse.Error);
            tokenResponse.AccessToken.ShouldNotBeNullOrEmpty();

            // 5. Call the protected API
            using var apiRequest = new HttpRequestMessage(HttpMethod.Get, $"{apiBase}/identity");
            apiRequest.SetBearerToken(tokenResponse.AccessToken!);

            var apiResponse = await httpClient.SendAsync(apiRequest);
            apiResponse.IsSuccessStatusCode.ShouldBeTrue($"API returned {apiResponse.StatusCode}");

            var body = await apiResponse.Content.ReadAsStringAsync();
            body.ShouldContain("sub");
        }
    }
}
