// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.ConformanceReport.Endpoints;
using Duende.IdentityServer.ConformanceReport;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.ConformanceReport;

/// <summary>
/// Integration tests for the IdentityServer adapter.
/// Verifies that an IdentityServer host with conformance report enabled
/// can successfully generate and serve the HTML report.
/// </summary>
public class ConformanceReportIntegrationTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        var clients = new List<Client>
        {
            new()
            {
                ClientId = "sample-client",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RedirectUris = { "https://localhost:5001/callback" },
                AllowedScopes = { "openid", "profile" }
            }
        };

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddAuthorization();
        builder.Services
            .AddIdentityServer(options =>
            {
                options.EmitIssuerIdentificationResponseParameter = true;
            })
            .AddInMemoryClients(clients)
            .AddInMemoryIdentityResources([new IdentityResources.OpenId(), new IdentityResources.Profile()])
            .AddConformanceReport(options =>
            {
                options.Enabled = true;
                options.EnableOAuth21Assessment = true;
                options.EnableFapi2SecurityAssessment = true;
                options.ConfigureAuthorization = policy =>
                    policy.RequireAssertion(_ => true);
            });

        _app = builder.Build();
        _app.UseRouting();
        _app.UseIdentityServer();
        _app.UseAuthorization();
        _app.MapConformanceReport();

        await _app.StartAsync();
        _client = _app.GetTestServer().CreateClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ConformanceReportEndpointReturnsHtmlReport()
    {
        var response = await _client.GetAsync("/_duende/conformance-report");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var contentType = response.Content.Headers.ContentType;
        _ = contentType.ShouldNotBeNull();
        contentType.MediaType.ShouldBe("text/html");

        var html = await response.Content.ReadAsStringAsync();
        html.ShouldNotBeEmpty();
        html.ShouldContain("<!DOCtYPE html>");
        html.ShouldContain("OAuth 2.1");
        html.ShouldContain("FAPI 2.0");
    }
}
