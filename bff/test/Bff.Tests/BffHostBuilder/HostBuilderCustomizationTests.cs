// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Endpoints;
using Duende.Bff.Tests.TestInfra;
using Duende.IdentityServer.Extensions;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.BffHostBuilder;

public class HostBuilderCustomizationTests : BffHostBuilderTestBase, IDisposable
{
    private HostApplicationBuilder _hostBuilder = Host.CreateApplicationBuilder();
    private IBffApplicationBuilder _bffApplication;
    private IBffEndpointBuilder _bffEndpointBuilder;
    private IHost _host = null!;

    public HostBuilderCustomizationTests(ITestOutputHelper output) : base(output)
    {
        _bffApplication = _hostBuilder.AddBffApplication()
            .UsingTestServer();
        _bffEndpointBuilder = _bffApplication.EnableBffEndpoint();
    }

    [Fact]
    public async Task Can_add_custom_login_page()
    {
        _bffEndpointBuilder.UsingCustomEndpoint<ILoginEndpoint, CustomEndpoint>();

        var client = await InitializeAsync();
        await client.GetAsync("/bff/login")
            .CheckResponseContent("custom");
    }

    protected async Task<BffHttpClient> InitializeAsync()
    {
        _host = _hostBuilder.Build();
        return await this.InitializeAsync(_host);
    }

    public void Dispose() => _host.Dispose();
}

public class CustomEndpoint : ILoginEndpoint
{
    public async Task ProcessRequestAsync(HttpContext context, CancellationToken ct = default)
    {
        await context.Response.WriteHtmlAsync("custom");
    }
}
