// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Json;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Clients.Setup;
using Duende.IdentityServer.IntegrationTests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Duende.IdentityServer.IntegrationTests.Clients;

public class CustomTokenRequestValidatorClient
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private const string TokenEndpoint = "https://server/connect/token";

    private readonly HttpClient _client;

    public CustomTokenRequestValidatorClient()
    {
        var val = new TestCustomTokenRequestValidator();
        Startup.CustomTokenRequestValidator = val;

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.UseStartup<Startup>();
            });

        var host = hostBuilder.Start();

        _client = host.GetTestClient();
    }

    [Fact]
    public async Task Client_credentials_request_should_contain_custom_response()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,

            ClientId = "client",
            ClientSecret = "secret",
            Scope = "api1"
        }, cancellationToken: _ct);

        var fields = GetFields(response);
        fields["custom"].GetString().ShouldBe("custom");
    }

    [Fact]
    public async Task Resource_owner_credentials_request_should_contain_custom_response()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,

            ClientId = "roclient",
            ClientSecret = "secret",
            Scope = "api1",

            UserName = "bob",
            Password = "bob"
        }, cancellationToken: _ct);

        var fields = GetFields(response);
        fields["custom"].GetString().ShouldBe("custom");
    }

    [Fact]
    public async Task Refreshing_a_token_should_contain_custom_response()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,

            ClientId = "roclient",
            ClientSecret = "secret",
            Scope = "api1 offline_access",

            UserName = "bob",
            Password = "bob"
        }, cancellationToken: _ct);

        response = await _client.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            RefreshToken = response.RefreshToken
        }, cancellationToken: _ct);

        var fields = GetFields(response);
        fields["custom"].GetString().ShouldBe("custom");
    }

    [Fact]
    public async Task Extension_grant_request_should_contain_custom_response()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            Address = TokenEndpoint,
            GrantType = "custom",

            ClientId = "client.custom",
            ClientSecret = "secret",

            Parameters =
            {
                { "scope", "api1" },
                { "custom_credential", "custom credential"}
            }
        }, cancellationToken: _ct);

        var fields = GetFields(response);
        fields["custom"].GetString().ShouldBe("custom");
    }

    private Dictionary<string, JsonElement> GetFields(TokenResponse response) => response.Raw.GetFields();
}
