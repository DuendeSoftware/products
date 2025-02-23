using Microsoft.Playwright;

namespace Hosts.Tests.PageModels;

public class BlazorModel : PageModel
{

    public async Task<BlazorModel> Login()
    {
        await Page.GetByRole(AriaRole.Link, new() { Name = "Log in" }).ClickAsync();

        var loginPage = await Build<LoginPageModel>();
        await loginPage.Login();

        return this;
    }

    public async Task VerifyLoggedIn()
    {
        var header = await GetHeader();
        // Get the parent div of the heading
        //var parentDiv = header.Locator("xpath=ancestor::article").First;
        var article = header.Locator("xpath=ancestor::article").First;

        // Verify that we're logged in correctly
        await Expect(article).ToContainTextAsync("amr");
        await Expect(article).ToContainTextAsync("Alice Smith");
    }

    private async Task<ILocator> GetHeader()
    {
        var header = Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, Blazor BFF!" });
        await Expect(header).ToBeVisibleAsync();

        return header;
    }
    public async Task VerifyNotLoggedIn()
    {
        var header = await GetHeader();

        // Get the parent div of the heading
        var article = header.Locator("xpath=ancestor::article").First;

        // When not logged in, no claims should be shown
        await Expect(article).ToContainTextAsync("No session");
        await Expect(article).Not.ToContainTextAsync("amr");
        await Expect(article).Not.ToContainTextAsync("Alice Smith");
    }
}