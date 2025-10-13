// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Clients.Setup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Duende.IdentityServer.IntegrationTests.Clients;

public class DiscoveryClientTests
{
    private const string DiscoveryEndpoint = "https://server/.well-known/openid-configuration";
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly HttpClient _client;

    public DiscoveryClientTests()
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
    public async Task Discovery_document_should_have_expected_values()
    {
        var doc = await _client.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
        {
            Address = DiscoveryEndpoint,
            Policy =
            {
                ValidateIssuerName = false
            }
        }, _ct);

        // endpoints
        doc.TokenEndpoint.ShouldBe("https://server/connect/token");
        doc.AuthorizeEndpoint.ShouldBe("https://server/connect/authorize");
        doc.IntrospectionEndpoint.ShouldBe("https://server/connect/introspect");
        doc.EndSessionEndpoint.ShouldBe("https://server/connect/endsession");

        // jwk
        doc.KeySet.Keys.Count.ShouldBe(1);
        doc.KeySet.Keys.First().E.ShouldNotBeNull();
        doc.KeySet.Keys.First().N.ShouldNotBeNull();
    }
}
