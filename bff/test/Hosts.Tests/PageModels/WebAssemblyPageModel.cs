// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Playwright;

namespace Hosts.Tests.PageModels;

public class WebAssemblyPageModel : BlazorModel
{
    public async Task<WeatherPageModel> GoToWeather()
    {
        await Page.GetByRole(AriaRole.Link, new() { Name = "Weather" }).ClickAsync();

        return await Build<WeatherPageModel>();
    }

    public async Task<WebAssemblyPageModel> LogOut()
    {
        // Log out again
        await Page.GetByRole(AriaRole.Link, new() { Name = "Log out" }).ClickAsync();

        var logoutPage = await Build<LogOutPageModel>();
        return await logoutPage.GoHome();
    }
}