// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Configuration;
using Duende.Bff.Tests.TestHosts;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints.Management;

public class ManagementBasePathTests(ITestOutputHelper output) : BffIntegrationTestBase(output)
{
    [Theory]
    [InlineData(Constants.ManagementEndpoints.Login)]
    [InlineData(Constants.ManagementEndpoints.Logout)]
#pragma warning disable CS0618 // Type or member is obsolete
    [InlineData(Constants.ManagementEndpoints.SilentLogin)]
#pragma warning restore CS0618 // Type or member is obsolete
    [InlineData(Constants.ManagementEndpoints.SilentLoginCallback)]
    [InlineData(Constants.ManagementEndpoints.User)]
    public async Task custom_ManagementBasePath_should_affect_basepath(string path)
    {
        Bff.OnConfigureServices += svcs =>
        {
            svcs.Configure<BffOptions>(options =>
            {
                options.ManagementBasePath = new PathString("/{path:regex(^[a-zA-Z\\d-]+$)}/bff");
            });
        };
        await Bff.InitializeAsync();

        var req = new HttpRequestMessage(HttpMethod.Get, Bff.Url("/custom/bff" + path));
        req.Headers.Add("x-csrf", "1");

        var response = await Bff.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }
}
