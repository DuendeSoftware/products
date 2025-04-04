// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Hosts.Tests.TestInfra.Retries;
using Hosts.ServiceDefaults;
using Hosts.Tests.PageModels;
using Hosts.Tests.TestInfra;
using Xunit.Abstractions;

namespace Hosts.Tests;

public class BlazorPerComponentTests(ITestOutputHelper output, AppHostFixture fixture)
    : PlaywrightTestBase(output, fixture)
{

    public async Task<PerComponentPageModel> GoToHome()
    {
        await Page.GotoAsync(Fixture.GetUrlTo(AppHostServices.BffBlazorPerComponent).ToString(), Defaults.PageGotoOptions);
        return new PerComponentPageModel()
        {
            Page = Page
        };
    }

    [RetriableFact]
    public async Task Can_load_blazor_webassembly_app()
    {
        await Warmup();

        var homePage = await GoToHome();
        await homePage.Login();
        var callApiPage = await homePage.GoToCallApiPage();


        await callApiPage.InvokeCallApi("InteractiveServer");
        await callApiPage.InvokeCallApi("InteractiveWebAssembly");
        await callApiPage.InvokeCallApi("InteractiveAuto");

    }
    private async Task Warmup()
    {
        // there have been issues where playwright hangs on the first test run.
        // maybe warming up the app will help?
        var httpClient = CreateHttpClient(AppHostServices.BffBlazorPerComponent);
        (await httpClient.GetAsync("/")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

}
