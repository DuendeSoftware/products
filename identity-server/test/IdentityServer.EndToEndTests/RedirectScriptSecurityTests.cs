// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;

namespace Duende.IdentityServer.EndToEndTests;

public sealed class RedirectScriptSecurityTests : PageTest
{
    private const string TestUrl = "https://redirect-script.test/start";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        IgnoreHTTPSErrors = true
    };

    [Fact]
    public async Task Signin_redirect_scripts_only_navigate_to_http_urls()
    {
        await RouteAllRequestsAsync();
        await Page.GotoAsync(TestUrl);

        var scripts = ReadEmbeddedScripts("signin-redirect.js");
        scripts.Length.ShouldBe(7);
        scripts.Distinct(StringComparer.Ordinal).Count().ShouldBe(1);

        var script = scripts[0];

        await Page.SetContentAsync(CreateMeta("https://client.test/callback"));
        var absoluteNavigation = Page.WaitForURLAsync("https://client.test/callback");
        await Page.AddScriptTagAsync(new() { Content = script });
        await absoluteNavigation;

        await Page.GotoAsync(TestUrl);
        await Page.SetContentAsync(CreateMeta("/relative/callback"));
        var relativeNavigation = Page.WaitForURLAsync("https://redirect-script.test/relative/callback");
        await Page.AddScriptTagAsync(new() { Content = script });
        await relativeNavigation;

        await Page.GotoAsync(TestUrl);
        await Page.SetContentAsync(CreateMeta("javascript:window.redirectScriptExecuted = true"));
        await Page.AddScriptTagAsync(new() { Content = script });
        await Page.WaitForTimeoutAsync(100);
        Page.Url.ShouldBe(TestUrl);
        (await Page.EvaluateAsync<bool>("window.redirectScriptExecuted === true")).ShouldBeFalse();

        await Page.GotoAsync(TestUrl);
        await Page.SetContentAsync(CreateMeta("https://["));
        await Page.AddScriptTagAsync(new() { Content = script });
        await Page.WaitForTimeoutAsync(100);
        Page.Url.ShouldBe(TestUrl);
    }

    [Fact]
    public async Task Federated_signout_only_navigates_to_http_urls_after_matching_message()
    {
        await RouteAllRequestsAsync();
        await Page.GotoAsync(TestUrl);

        var script = ReadEmbeddedScripts("federated-signout.js").Single();
        await Page.SetContentAsync(CreateIframe("https://client.test/logout"));
        await Page.AddScriptTagAsync(new() { Content = script });
        var navigation = Page.WaitForURLAsync("https://client.test/logout", new() { Timeout = 1_000 });
        await Page.FrameLocator("#signout-frame").Locator("body")
            .EvaluateAsync("body => window.parent.postMessage('logout-iframes-complete', window.parent.location.origin)");
        await navigation;

        await Page.GotoAsync(TestUrl);
        await Page.SetContentAsync(CreateIframe("javascript:window.redirectScriptExecuted = true"));
        await Page.AddScriptTagAsync(new() { Content = script });
        await Page.FrameLocator("#signout-frame").Locator("body")
            .EvaluateAsync("body => window.parent.postMessage('logout-iframes-complete', window.parent.location.origin)");
        await Page.WaitForTimeoutAsync(100);
        Page.Url.ShouldBe(TestUrl);
        (await Page.EvaluateAsync<bool>("window.redirectScriptExecuted === true")).ShouldBeFalse();
    }

    private Task RouteAllRequestsAsync() =>
        Page.RouteAsync("**/*", route => route.FulfillAsync(new()
        {
            Status = 200,
            ContentType = "text/html",
            Body = "<!doctype html><html><body></body></html>"
        }));

    private static string[] ReadEmbeddedScripts(string suffix)
    {
        var assembly = typeof(RedirectScriptSecurityTests).Assembly;
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(suffix, StringComparison.Ordinal))
            .ToArray();

        return resourceNames.Select(name =>
        {
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Embedded script '{name}' was not found.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }).ToArray();
    }

    private static string CreateMeta(string url) =>
        $"<meta http-equiv=\"refresh\" content=\"60\" data-url=\"{WebUtility.HtmlEncode(url)}\">";

    private static string CreateIframe(string url) =>
        $"<iframe id=\"signout-frame\" src=\"{TestUrl}\" data-completion-url=\"{WebUtility.HtmlEncode(url)}\"></iframe>";
}
