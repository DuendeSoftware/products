using Microsoft.Playwright;

namespace Hosts.Tests.PageModels;

public class LoginPageModel: PageModel
{

    public async Task Login()
    {
        await Page.GetByPlaceholder("Username").ClickAsync();
        await Page.GetByPlaceholder("Username").FillAsync("alice");
        await Page.GetByPlaceholder("Password").ClickAsync();
        await Page.GetByPlaceholder("Password").FillAsync("alice");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
    }
}