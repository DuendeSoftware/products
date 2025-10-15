// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Tests.TestHosts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Tests.Endpoints.Management;

public class ManagementBasePathTests(ITestOutputHelper output) : BffIntegrationTestBase(output)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(Constants.ManagementEndpoints.Login)]
    [InlineData(Constants.ManagementEndpoints.Logout)]
    [InlineData(Constants.ManagementEndpoints.SilentLogin)]
    [InlineData(Constants.ManagementEndpoints.SilentLoginCallback)]
    [InlineData(Constants.ManagementEndpoints.User)]
    public async Task custom_ManagementBasePath_should_affect_basepath(string path)
    {
        BffHost.OnConfigureServices += services =>
        {
            services.Configure<BffOptions>(options =>
            {
                options.ManagementBasePath = new PathString("/{path:regex(^[a-zA-Z\\d-]+$)}/bff");
            });
        };
        await BffHost.InitializeAsync();

        var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/custom/bff" + path));
        req.Headers.Add("x-csrf", "1");

        var response = await BffHost.BrowserClient.SendAsync(req, _ct);

        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }
}
