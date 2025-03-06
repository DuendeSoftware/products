// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Playwright;

namespace Hosts.Tests.PageModels;

public class LoginPageModel : PageModel
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