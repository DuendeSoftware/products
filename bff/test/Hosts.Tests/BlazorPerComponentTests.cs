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
        await Page.GotoAsync(Fixture.GetUrlTo(AppHostServices.BffBlazorPerComponent).ToString());
        return new PerComponentPageModel()
        {
            Page = Page
        };
    }

    [SkippableFact]
    public async Task Can_load_blazor_webassembly_app()
    {
        var homePage = await GoToHome();
        await homePage.Login();
        var callApiPage = await homePage.GoToCallApiPage();


        await callApiPage.InvokeCallApi("InteractiveServer");
        await callApiPage.InvokeCallApi("InteractiveWebAssembly");
        await callApiPage.InvokeCallApi("InteractiveAuto");

    }


}