// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace Duende.Bff.Tests;

public class BffOptionsConfigurationTests(ITestOutputHelper output) : BffTestBase(output)
{
    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task calls_outside_http_context_can_get_oidc_configuration(BffSetupType setup)
    {
        Bff.OnConfigureApp += app =>
        {
            app.Map(The.Path, c => ApiHost.ReturnApiCallDetails(c, () => HttpStatusCode.OK))
                .RequireAuthorization()
                .AsBffApiEndpoint();
        };

        await ConfigureBff(setup);

        // this retrieves the openid connect options outside the http context. This shouldn't
        // normally happen but AzureAppConfigurationRefreshMiddleware can cause this.
        // when this happens, this call would fail with No HTTP Context available,
        // but also all subsequent requests, because IOptionsCache caches this.
        var opt = Bff.Resolve<IOptionsMonitor<OpenIdConnectOptions>>();
        opt.Get(Some.BffFrontend().OidcSchemeName);

        await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path)
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.Path);
        apiResult.Sub.ShouldBe(The.Sub);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task calls_outside_http_context_can_get_cookie_configuration(BffSetupType setup)
    {
        Bff.OnConfigureApp += app =>
        {
            app.Map(The.Path, c => ApiHost.ReturnApiCallDetails(c, () => HttpStatusCode.OK))
                .RequireAuthorization()
                .AsBffApiEndpoint();
        };

        await ConfigureBff(setup);

        // this retrieves the cookie connect options outside the http context. This shouldn't
        // normally happen but AzureAppConfigurationRefreshMiddleware can cause this.
        // when this happens, this call would fail with No HTTP Context available,
        // but also all subsequent requests, because IOptionsCache caches this.
        var opt = Bff.Resolve<IOptionsMonitor<CookieAuthenticationOptions>>();
        opt.Get(Some.BffFrontend().CookieSchemeName);

        await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path)
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.Path);
        apiResult.Sub.ShouldBe(The.Sub);
    }
}
