// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Shouldly;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using IntegrationTests.Clients.Setup;
using Xunit;

namespace IntegrationTests.Clients;

public class DiscoveryClientTests
{
    private const string DiscoveryEndpoint = "https://server/.well-known/openid-configuration";

    private readonly HttpClient _client;

    public DiscoveryClientTests()
    {
        var builder = new WebHostBuilder()
            .UseStartup<Startup>();
        var server = new TestServer(builder);

        _client = server.CreateClient();
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
        });

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