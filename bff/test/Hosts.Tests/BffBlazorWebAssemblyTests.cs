// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Xunit.Playwright;
using Duende.Xunit.Playwright.Retries;
using Hosts.ServiceDefaults;
using Hosts.Tests.PageModels;
using Hosts.Tests.TestInfra;
using Xunit.Abstractions;

namespace Hosts.Tests;

public class BffBlazorWebAssemblyTests(ITestOutputHelper output, BffHostTestFixture fixture)
    : BffPlaywrightTestBase(output, fixture)
{
    public async Task<WebAssemblyPageModel> GoToHome()
    {
        await Page.GotoAsync(Fixture.GetUrlTo(AppHostServices.BffBlazorWebassembly).ToString(), Defaults.PageGotoOptions);
        return new WebAssemblyPageModel()
        {
            Page = Page
        };
    }

    [RetryableFact]
    public async Task Can_login_and_load_local_api()
    {
        await Warmup();


        var homePage = await GoToHome();

        await homePage.VerifyNotLoggedIn();

        await homePage.Login();

        await homePage.VerifyLoggedIn();

        var weatherPage = await homePage.GoToWeather();

        await weatherPage.VerifyWeatherListIsShown();

        await homePage.LogOut();
    }

    private async Task Warmup()
    {
        // there have been issues where playwright hangs on the first test run.
        // maybe warming up the app will help?
        var httpClient = CreateHttpClient(AppHostServices.BffBlazorWebassembly);
        (await httpClient.GetAsync("/")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
