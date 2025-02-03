using System.Text.RegularExpressions;
using Hosts.Tests.TestInfra;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using Shouldly;

namespace Hosts.Tests;
public class BlazorWebAssemblyTests(AppHostFixture fixture) : PageTest, IClassFixture<AppHostFixture>
{
    [Fact]
    public async Task Can_load_blazor_webassembly_app()
    {
        // Navigate to the app
        await Page.GotoAsync("https://localhost:5105");
        (await Page.TitleAsync()).ShouldBe("Home");
        // Wait for the app to load


        // Click the get started link.
        await Page.GetByRole(AriaRole.Link, new() { Name = "Call Api" }).ClickAsync();
        (await Page.TitleAsync()).ShouldBe("Home");

        await Login();

        await InvokeCallApi("InteractiveServer");
        await InvokeCallApi("InteractiveWebAssembly");
        await InvokeCallApi("InteractiveAuto");

    }

    private async Task Login()
    {
        await Page.GetByPlaceholder("Username").ClickAsync();
        await Page.GetByPlaceholder("Username").FillAsync("alice");
        await Page.GetByPlaceholder("Password").ClickAsync();
        await Page.GetByPlaceholder("Password").FillAsync("alice");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
    }

    private async Task InvokeCallApi(string headingName)
    {
        // Get the heading with the name "InteractiveServer"
        var heading = Page.GetByRole(AriaRole.Heading, new() { Name = headingName });

        // Get the parent div of the heading
        var parentDiv = heading.Locator("xpath=ancestor::div[@class='col']").First;

        // Assert that the parent div is found
        parentDiv.ShouldNotBeNull();

        var button = parentDiv.GetByRole(AriaRole.Button, new() { Name = "Call Api" });
        await button.ClickAsync();

        await Expect(parentDiv).ToContainTextAsync("Token ID");
    }
}
