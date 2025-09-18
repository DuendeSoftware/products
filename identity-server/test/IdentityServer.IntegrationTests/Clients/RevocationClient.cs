// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Clients.Setup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Duende.IdentityServer.IntegrationTests.Clients;

public class RevocationClient
{
    private const string TokenEndpoint = "https://server/connect/token";
    private const string RevocationEndpoint = "https://server/connect/revocation";
    private const string IntrospectionEndpoint = "https://server/connect/introspect";

    private readonly HttpClient _client;

    public RevocationClient()
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
    public async Task Revoking_reference_token_should_invalidate_token()
    {
        // request acccess token
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient.reference",
            ClientSecret = "secret",

            Scope = "api1",
            UserName = "bob",
            Password = "bob"
        });

        response.IsError.ShouldBeFalse();

        // introspect - should be active
        var introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api",
            ClientSecret = "secret",

            Token = response.AccessToken
        });

        introspectionResponse.IsActive.ShouldBe(true);

        // revoke access token
        var revocationResponse = await _client.RevokeTokenAsync(new TokenRevocationRequest
        {
            Address = RevocationEndpoint,
            ClientId = "roclient.reference",
            ClientSecret = "secret",

            Token = response.AccessToken
        });

        // introspect - should be inactive
        introspectionResponse = await _client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = IntrospectionEndpoint,
            ClientId = "api",
            ClientSecret = "secret",

            Token = response.AccessToken
        });

        introspectionResponse.IsActive.ShouldBe(false);
    }
}
