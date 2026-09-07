// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Playwright;

namespace Duende.Conformance.Infrastructure;

/// <summary>
/// Automates the login, consent, and logout flows using Playwright.
/// The conformance suite redirects to the IdP's authorization endpoint;
/// this class drives a headless browser to complete login and consent so the
/// authorization code is returned to the conformance suite callback.
/// </summary>
public sealed class LoginAutomation : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private IBrowserContext? _context;

    private string Username { get; }
    private string Password { get; }

    private LoginAutomation(
        IPlaywright playwright,
        IBrowser browser,
        string username,
        string password)
    {
        _playwright = playwright;
        _browser = browser;
        Username = username;
        Password = password;
    }

    /// <summary>
    /// Creates a new instance with a headless Chromium browser.
    /// </summary>
    public static async Task<LoginAutomation> CreateAsync(
        string username = "alice",
        string password = "alice")
    {
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        return new LoginAutomation(playwright, browser, username, password);
    }

    /// <summary>
    /// Returns a shared browser context, creating one on first use.
    /// Reusing the context preserves cookies across authorization requests within
    /// the same test module, which is required for tests like prompt=none and max_age
    /// that expect the user to remain logged in across multiple authorization requests.
    /// </summary>
    private async Task<IBrowserContext> GetOrCreateContextAsync()
    {
        _context ??= await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
        return _context;
    }

    /// <summary>
    /// Resets the browser context, clearing all cookies and state.
    /// Call this between test modules to ensure a clean slate.
    /// </summary>
    public async Task ResetContextAsync()
    {
        if (_context is not null)
        {
            await _context.CloseAsync();
            _context = null;
        }
    }

    /// <summary>
    /// Navigates to the given authorization URL, completes login and consent,
    /// and waits for the redirect back to the conformance suite callback.
    /// </summary>
    /// <param name="authorizationUrl">The full authorization endpoint URL with query params.</param>
    /// <param name="callbackUrlPrefix">The expected callback URL prefix to wait for.</param>
    /// <param name="timeout">Maximum time to wait for the flow to complete.</param>
    /// <param name="log">Optional callback for diagnostic logging.</param>
    /// <returns>The result of the authorization flow.</returns>
    public async Task<AuthorizationResult> CompleteAuthorizationAsync(
        string authorizationUrl,
        string callbackUrlPrefix,
        TimeSpan? timeout = null,
        Action<string>? log = null)
    {
        timeout ??= TimeSpan.FromSeconds(30);

        var context = await GetOrCreateContextAsync();
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout((float)timeout.Value.TotalMilliseconds);

        try
        {
            // Navigate to the authorization endpoint — should redirect to login
            log?.Invoke($"Navigating to: {authorizationUrl}");
            await page.GotoAsync(authorizationUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load
            });
            log?.Invoke($"Landed on: {page.Url}");

            // Handle login page
            if (IsLoginPage(page.Url))
            {
                log?.Invoke("Detected login page, filling form...");
                await FillLoginFormAsync(page);
                await page.WaitForLoadStateAsync(LoadState.Load);
                log?.Invoke($"After login, at: {page.Url}");
            }

            // Handle consent page
            if (IsConsentPage(page.Url))
            {
                log?.Invoke("Detected consent page, granting consent...");
                await GrantConsentAsync(page);
                await page.WaitForLoadStateAsync(LoadState.Load);
                log?.Invoke($"After consent, at: {page.Url}");
            }

            // Handle logout confirmation page (if shown)
            if (IsLogoutPage(page.Url))
            {
                log?.Invoke("Detected logout page, confirming logout...");
                await ConfirmLogoutAsync(page);
                await page.WaitForLoadStateAsync(LoadState.Load);
                log?.Invoke($"After logout, at: {page.Url}");
            }

            // Handle post-logout "logged out" page (click redirect link if present)
            if (IsLoggedOutPage(page.Url))
            {
                log?.Invoke("Detected logged-out page, clicking redirect link...");
                await HandleLoggedOutPageAsync(page);
                await page.WaitForLoadStateAsync(LoadState.Load);
                log?.Invoke($"After logged-out redirect, at: {page.Url}");
            }

            // Verify we ended up at the callback
            if (!page.Url.StartsWith(callbackUrlPrefix, StringComparison.OrdinalIgnoreCase))
            {
                log?.Invoke($"Did not reach callback. Final URL: {page.Url}");
                await CaptureScreenshotAsync(page, "unexpected-redirect");
                return AuthorizationResult.ErrorPage(page.Url);
            }

            log?.Invoke("Successfully completed authorization flow");
            return AuthorizationResult.Success;
        }
        catch (Exception ex)
        {
            log?.Invoke($"Authorization flow failed: {ex.GetType().Name}: {ex.Message}");
            await CaptureScreenshotAsync(page, "failure");
            throw;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Returns true if the current URL is the login page.
    /// </summary>
    private static bool IsLoginPage(string url) =>
        url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if the current URL is the consent page.
    /// </summary>
    private static bool IsConsentPage(string url) =>
        url.Contains("/Consent", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if the current URL is a logout confirmation page.
    /// </summary>
    private static bool IsLogoutPage(string url) =>
        url.Contains("/Account/Logout", StringComparison.OrdinalIgnoreCase) &&
        !url.Contains("/LoggedOut", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Fills in the login form and submits it.
    /// </summary>
    private async Task FillLoginFormAsync(IPage page)
    {
        await page.FillAsync("input[name='Input.Username']", Username);
        await page.FillAsync("input[name='Input.Password']", Password);
        await page.ClickAsync("button[name='Input.Button'][value='login']");
    }

    /// <summary>
    /// Grants consent on the consent page.
    /// </summary>
    private static async Task GrantConsentAsync(IPage page) =>
        await page.ClickAsync("button[name='Input.button'][value='yes']");

    /// <summary>
    /// Confirms logout on the logout page.
    /// </summary>
    private static async Task ConfirmLogoutAsync(IPage page) =>
        await page.ClickAsync("button.btn-primary");

    /// <summary>
    /// Returns true if the current URL is a post-logout "logged out" page
    /// that requires clicking a link to redirect back to the RP.
    /// </summary>
    private static bool IsLoggedOutPage(string url) =>
        url.Contains("/Account/Logout/LoggedOut", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Handles the post-logout "logged out" page by clicking the redirect link.
    /// </summary>
    private static async Task HandleLoggedOutPageAsync(IPage page)
    {
        // The LoggedOut page has a link with class "PostLogoutRedirectUri" that
        // redirects back to the RP's post_logout_redirect_uri.
        var link = page.Locator("a.PostLogoutRedirectUri");
        if (await link.CountAsync() > 0)
        {
            await link.ClickAsync();
        }
    }

    /// <summary>
    /// Navigates to the given authorization URL and denies the request (simulates user pressing cancel).
    /// The IdP should redirect back to the callback with an access_denied error.
    /// </summary>
    /// <param name="authorizationUrl">The full authorization endpoint URL with query params.</param>
    /// <param name="callbackUrlPrefix">The expected callback URL prefix to wait for.</param>
    /// <param name="timeout">Maximum time to wait for the flow to complete.</param>
    /// <param name="log">Optional callback for diagnostic logging.</param>
    /// <returns>The result of the authorization flow.</returns>
    public async Task<AuthorizationResult> DenyAuthorizationAsync(
        string authorizationUrl,
        string callbackUrlPrefix,
        TimeSpan? timeout = null,
        Action<string>? log = null)
    {
        timeout ??= TimeSpan.FromSeconds(30);

        // Use a fresh context (no cookies) so the IdP always shows the login page,
        // allowing us to click cancel regardless of prior authentication state.
        var freshContext = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
        var page = await freshContext.NewPageAsync();
        page.SetDefaultTimeout((float)timeout.Value.TotalMilliseconds);

        try
        {
            log?.Invoke($"Navigating to: {authorizationUrl}");
            await page.GotoAsync(authorizationUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load
            });
            log?.Invoke($"Landed on: {page.Url}");

            // Click the cancel button on the login page to deny the request
            if (IsLoginPage(page.Url))
            {
                log?.Invoke("Detected login page, clicking cancel...");
                await DenyOnLoginPageAsync(page);
                await page.WaitForLoadStateAsync(LoadState.Load);
                log?.Invoke($"After cancel, at: {page.Url}");
            }
            else
            {
                log?.Invoke($"Expected login page but landed on: {page.Url}");
            }

            // Verify we ended up at the callback (with an error)
            if (!page.Url.StartsWith(callbackUrlPrefix, StringComparison.OrdinalIgnoreCase))
            {
                log?.Invoke($"Did not reach callback. Final URL: {page.Url}");
                return AuthorizationResult.ErrorPage(page.Url);
            }

            log?.Invoke("Successfully denied authorization flow");
            return AuthorizationResult.Success;
        }
        catch (Exception ex)
        {
            log?.Invoke($"Deny authorization flow failed: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
        finally
        {
            await page.CloseAsync();
            await freshContext.CloseAsync();
        }
    }

    /// <summary>
    /// Clicks the cancel/deny button on the login page to deny the authorization request.
    /// </summary>
    private static async Task DenyOnLoginPageAsync(IPage page) =>
        await page.ClickAsync("button[name='Input.Button'][value='cancel']");

    /// <summary>
    /// Navigates to the authorization URL and lands on the login page without completing the flow.
    /// Uses a fresh browser context (no cookies) to ensure the login page is shown.
    /// Returns the open page so the caller can later call <see cref="CompleteAuthorizationOnPageAsync"/>.
    /// The caller is responsible for closing the page AND the context when done.
    /// </summary>
    public async Task<(IPage Page, IBrowserContext Context)> NavigateToLoginPageAsync(
        string authorizationUrl,
        TimeSpan? timeout = null,
        Action<string>? log = null)
    {
        timeout ??= TimeSpan.FromSeconds(30);

        // Use a fresh context (no cookies) so IS always shows the login page
        var freshContext = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
        var page = await freshContext.NewPageAsync();
        page.SetDefaultTimeout((float)timeout.Value.TotalMilliseconds);

        log?.Invoke($"Navigating to login page (keep-open, fresh context): {authorizationUrl}");
        await page.GotoAsync(authorizationUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load
        });
        log?.Invoke($"Landed on: {page.Url}");

        return (page, freshContext);
    }

    /// <summary>
    /// Completes the authorization flow on an already-open page (e.g., one returned by
    /// <see cref="NavigateToLoginPageAsync"/>). Fills in the login form and waits for the
    /// redirect to the callback URL.
    /// </summary>
    public async Task<AuthorizationResult> CompleteAuthorizationOnPageAsync(
        IPage page,
        string callbackUrlPrefix,
        Action<string>? log = null)
    {
        try
        {
            if (IsLoginPage(page.Url))
            {
                log?.Invoke("Filling login form on kept-open page...");
                await FillLoginFormAsync(page);
                await page.WaitForLoadStateAsync(LoadState.Load);
                log?.Invoke($"After login, at: {page.Url}");
            }

            if (IsConsentPage(page.Url))
            {
                log?.Invoke("Granting consent on kept-open page...");
                await GrantConsentAsync(page);
                await page.WaitForLoadStateAsync(LoadState.Load);
                log?.Invoke($"After consent, at: {page.Url}");
            }

            if (!page.Url.StartsWith(callbackUrlPrefix, StringComparison.OrdinalIgnoreCase))
            {
                log?.Invoke($"Did not reach callback. Final URL: {page.Url}");
                return AuthorizationResult.ErrorPage(page.Url);
            }

            log?.Invoke("Successfully completed authorization flow on kept-open page");
            return AuthorizationResult.Success;
        }
        catch (Exception ex)
        {
            log?.Invoke($"CompleteAuthorizationOnPageAsync failed: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// The caller is responsible for closing the page when done.
    /// This is used for pages that need to stay open for JavaScript to run
    /// (e.g., session management verification pages with iframe postMessage).
    /// </summary>
    /// <param name="url">The URL to navigate to.</param>
    /// <param name="urlRewrites">Optional URL rewrite rules applied to all requests from this page
    /// (e.g., to map internal Docker hostnames to localhost for iframe content).</param>
    /// <param name="timeout">Maximum time to wait for navigation.</param>
    /// <param name="log">Optional callback for diagnostic logging.</param>
    public async Task<IPage> NavigateToPageAsync(
        string url,
        IReadOnlyDictionary<string, string>? urlRewrites = null,
        TimeSpan? timeout = null,
        Action<string>? log = null)
    {
        timeout ??= TimeSpan.FromSeconds(30);

        var context = await GetOrCreateContextAsync();
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout((float)timeout.Value.TotalMilliseconds);

        // Set up URL rewriting for page content (e.g., iframe src that uses internal Docker hostnames).
        // We rewrite URLs in the HTML response body rather than intercepting network requests,
        // because the iframe needs to actually load from the correct origin to access cookies.
        if (urlRewrites is { Count: > 0 })
        {
            await page.RouteAsync("**/*", async route =>
            {
                var response = await route.FetchAsync();
                var contentType = response.Headers.GetValueOrDefault("content-type", "");

                if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    var body = await response.TextAsync();
                    var rewrittenBody = body;

                    foreach (var (from, to) in urlRewrites)
                    {
                        rewrittenBody = rewrittenBody.Replace(from, to, StringComparison.OrdinalIgnoreCase);
                    }

                    if (rewrittenBody != body)
                    {
                        log?.Invoke($"Rewrote URLs in HTML response for: {route.Request.Url}");
                    }

                    await route.FulfillAsync(new RouteFulfillOptions
                    {
                        Response = response,
                        Body = rewrittenBody
                    });
                }
                else
                {
                    await route.FulfillAsync(new RouteFulfillOptions { Response = response });
                }
            });
        }

        log?.Invoke($"Navigating to (keep-open): {url}");
        await page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });
        log?.Invoke($"Landed on: {page.Url}");

        return page;
    }

    private static async Task CaptureScreenshotAsync(IPage page, string label)
    {
        var outputDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "conformance-reports"));
        Directory.CreateDirectory(outputDir);

        var filePath = Path.Combine(outputDir, $"screenshot-{label}.png");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = filePath, FullPage = true });
    }

    public async ValueTask DisposeAsync()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
    }
}

/// <summary>
/// The outcome of a browser-driven authorization flow.
/// </summary>
public sealed class AuthorizationResult
{
    /// <summary>
    /// The authorization flow completed successfully and the browser was redirected
    /// to the conformance suite callback.
    /// </summary>
    public static AuthorizationResult Success { get; } = new() { Succeeded = true };

    /// <summary>
    /// Creates a result indicating the IdP showed an error page instead of redirecting
    /// to the callback. This is expected for negative tests (e.g., missing response_type,
    /// invalid redirect_uri).
    /// </summary>
    public static AuthorizationResult ErrorPage(string finalUrl) => new()
    {
        Succeeded = false,
        FinalUrl = finalUrl
    };

    /// <summary>
    /// Whether the authorization flow completed with a redirect to the callback.
    /// </summary>
    public bool Succeeded { get; private init; }

    /// <summary>
    /// The final URL the browser landed on, if the flow did not succeed.
    /// </summary>
    public string? FinalUrl { get; private init; }
}
