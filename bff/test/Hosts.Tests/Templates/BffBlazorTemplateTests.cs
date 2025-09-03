// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Xunit.Playwright;
using Hosts.ServiceDefaults;
using Hosts.Tests.PageModels;
using Hosts.Tests.TestInfra;
using Xunit.Abstractions;

namespace Hosts.Tests.Templates;

public class BffBlazorTemplateTests(ITestOutputHelper output, BffHostTestFixture fixture)
    : BffPlaywrightTestBase(output, fixture)
{
    public async Task<WebAssemblyPageModel> GoToHome()
    {
        await Page.GotoAsync(Fixture.GetUrlTo(AppHostServices.TemplateBffBlazor).ToString(), Defaults.PageGotoOptions);
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

        await homePage.Login();

        var weatherPage = await homePage.GoToWeather();

        await weatherPage.VerifyWeatherListIsShown();
    }

    private async Task Warmup()
    {
        // there have been issues where playwright hangs on the first test run.
        // maybe warming up the app will help?
        var httpClient = CreateHttpClient(AppHostServices.TemplateBffBlazor);
        (await httpClient.GetAsync("/")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
