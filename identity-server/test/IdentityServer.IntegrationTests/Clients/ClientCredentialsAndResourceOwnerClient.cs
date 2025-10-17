// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Clients.Setup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Duende.IdentityServer.IntegrationTests.Clients;

public class ClientCredentialsandResourceOwnerClient
{
    private const string TokenEndpoint = "https://server/connect/token";

    private readonly HttpClient _client;

    public ClientCredentialsandResourceOwnerClient()
    {
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
    public async Task Resource_scope_should_be_requestable_via_client_credentials()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client.and.ro",
            ClientSecret = "secret",
            Scope = "api1"
        });

        response.IsError.ShouldBe(false);
    }

    [Fact]
    public async Task Openid_scope_should_not_be_requestable_via_client_credentials()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client.and.ro",
            ClientSecret = "secret",
            Scope = "openid api1"
        });

        response.IsError.ShouldBe(true);
    }

    [Fact]
    public async Task Openid_scope_should_be_requestable_via_password()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client.and.ro",
            ClientSecret = "secret",
            Scope = "openid",

            UserName = "bob",
            Password = "bob"
        });

        response.IsError.ShouldBe(false);
    }

    [Fact]
    public async Task Openid_and_resource_scope_should_be_requestable_via_password()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "client.and.ro",
            ClientSecret = "secret",
            Scope = "openid api1",

            UserName = "bob",
            Password = "bob"
        });

        response.IsError.ShouldBe(false);
    }
}
