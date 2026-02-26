// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Playwright;

namespace Duende.IdentityServer.EndToEndTests.TestInfra;

public static class IdentityServerPageExtensions
{
    public static async Task Login(this IPage page, string userName = "alice", string password = "alice")
    {
        await page.GetLink("Secure").ClickAsync();
        await page.GetByLabel("Username").FillAsync(userName);
        await page.GetByLabel("Password").FillAsync(password);
        await page.GetButton("Login").ClickAsync();

        // page.Url.ShouldEndWith("/Secure");
    }

    public static async Task Logout(this IPage page)
    {
        await page.GetLink("Logout").ClickAsync();
        page.GetByText("You are now logged out").ShouldNotBeNull();
        // Click on "Click here to return..."
        await page.GetLink("here").ClickAsync();
        // Logout link is hidden if not logged in
        (await page.GetLink("Logout").CountAsync()).ShouldBe(0);
    }

    public static async Task RenewTokens(this IPage page)
    {
        // Renew Tokens is on the secure page, so make sure we're there
        await page.GetLink("Secure").ClickAsync();

        var oldAccessToken = await page.GetDisplayedAccessToken();
        await page.GetLink("Renew Tokens").ClickAsync();
        var newAccessToken = await page.GetDisplayedAccessToken();
        newAccessToken.ShouldNotBe(oldAccessToken);
    }

    public static async Task CallApi(this IPage page)
    {
        await page.GetLink("Call API").ClickAsync();
        var apiResult = await page.Locator("pre").TextContentAsync();
        apiResult.ShouldNotBeNull();
        apiResult.ShouldContain("jti");
        apiResult.ShouldContain("sub");
        apiResult.ShouldContain("iss");
    }

    private static ILocator GetLink(this IPage page, string name)
        => page.GetByRole(AriaRole.Link, new() { Name = name });

    private static ILocator GetButton(this IPage page, string name)
        => page.GetByRole(AriaRole.Button, new() { Name = name });

    private static ILocator GetDefinitionByTerm(this IPage page, string term)
        => page.Locator($"dt:has-text('{term}') + dd");

    private static async Task<string?> GetDisplayedAccessToken(this IPage page)
    {
        var accessToken = await page.GetDefinitionByTerm(".Token.access_token").TextContentAsync();
        accessToken.ShouldNotBeNull();
        return accessToken;
    }
}
