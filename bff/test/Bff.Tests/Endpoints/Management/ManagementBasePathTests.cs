// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Configuration;
using Duende.Bff.Tests.TestInfra;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints.Management;

public class ManagementBasePathTests(ITestOutputHelper output) : BffTestBase(output)
{
    [Theory]
    [InlineData(Constants.ManagementEndpoints.Login, HttpStatusCode.Redirect)]
    [InlineData(Constants.ManagementEndpoints.Logout, HttpStatusCode.Redirect)]
#pragma warning disable CS0618 // Type or member is obsolete
    [InlineData(Constants.ManagementEndpoints.SilentLogin, HttpStatusCode.Redirect)]
#pragma warning restore CS0618 // Type or member is obsolete
    [InlineData(Constants.ManagementEndpoints.SilentLoginCallback, HttpStatusCode.OK)]
    [InlineData(Constants.ManagementEndpoints.User, HttpStatusCode.Unauthorized)]
    public async Task custom_ManagementBasePath_should_affect_basepath(string path, HttpStatusCode expectedStatusCode)
    {
        SetupDefaultBffAuthentication();
        Bff.OnConfigureServices += svcs =>
        {
            svcs.Configure<BffOptions>(options =>
            {
                options.ManagementBasePath = new PathString("/{path:regex(^[a-zA-Z\\d-]+$)}/bff");
            });
        };
        await InitializeAsync();

        // Don't follow the redirects, becuase otherwise we might folow a redirect flow that ends up in a 404
        Bff.BrowserClient.RedirectHandler.AutoFollowRedirects = false;


        // Make sure the 'original path' doesn't work
        await VerifyRoute(path, HttpStatusCode.NotFound);

        // but the custom path does work
        await VerifyRoute(path, expectedStatusCode, "/custom/bff");
    }

    private async Task VerifyRoute(string path, HttpStatusCode expectedStatusCode, string? prefix = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, Bff.Url(prefix + path));
        req.Headers.Add("x-csrf", "1");

        var response = await Bff.BrowserClient.SendAsync(req);
        response.StatusCode.ShouldBe(expectedStatusCode);

    }
}
