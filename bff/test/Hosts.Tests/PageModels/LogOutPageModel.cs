using Microsoft.Playwright;

namespace Hosts.Tests.PageModels;

public class LogOutPageModel : PageModel
{
    protected override Task Verify()
    {
        Page.Url.ShouldContain("/Account/Logout/LoggedOut?");
        return Task.CompletedTask;
    }

    public async Task<WebAssemblyPageModel> GoHome()
    {
        await Page.GetByRole(AriaRole.Link, new() { Name = "here" }).ClickAsync();

        return await Build<WebAssemblyPageModel>();
    }
}