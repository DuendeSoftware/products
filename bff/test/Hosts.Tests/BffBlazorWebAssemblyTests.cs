using Hosts.ServiceDefaults;
using Hosts.Tests.PageModels;
using Hosts.Tests.TestInfra;
using Microsoft.Playwright;
using System.Net.Http;
using Xunit.Abstractions;

namespace Hosts.Tests;

public class BffBlazorWebAssemblyTests(ITestOutputHelper output, AppHostFixture fixture)
    : PlaywrightTestBase(output, fixture)
{
    public async Task<WebAssemblyPageModel> GoToHome()
    {
        await Page.GotoAsync(Fixture.GetUrlTo(AppHostServices.BffBlazorWebassembly).ToString());
        return new WebAssemblyPageModel()
        {
            Page = Page
        };
    }

    [SkippableFact]
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