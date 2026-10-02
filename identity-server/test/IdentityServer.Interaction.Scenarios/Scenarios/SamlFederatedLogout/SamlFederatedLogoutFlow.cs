// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Interaction.SharedHosts.SamlClient;
using Duende.IdentityServer.Interaction.Tests.Infrastructure;
using Duende.IdentityServer.UI.Infra;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using Shouldly;

namespace Duende.IdentityServer.Interaction.Scenarios.SamlFederatedLogout;

/// <summary>
/// Exercises an upstream-IdP-initiated SAML single logout across a three-tier chain:
/// an upstream <see cref="IdentityServerTestHost"/> acting as SAML IdP, a middle
/// <see cref="IdentityServerTestHost"/> acting simultaneously as SAML SP (to the upstream)
/// and SAML IdP (to the downstream SP), and a downstream <see cref="SamlClientTestHost"/>
/// acting as a SAML SP. The middle host is wired as a SAML SP to the upstream via
/// <c>AddSamlDynamicProvider</c>/<c>AddInMemorySamlProviders</c>, and retains
/// <c>AddSaml</c> for its IdP role toward the downstream SP, registered via
/// <c>AddInMemorySamlServiceProviders</c>.
/// </summary>
/// <remarks>
/// Manual walkthrough from the Aspire dashboard:
/// <list type="number">
/// <item>Start this scenario ("SamlFederatedLogoutFlow") from the Aspire dashboard.</item>
/// <item>Click the "Start SAML sign-in (downstream secure)" link.</item>
/// <item>On the middle IdentityServer's login page, click the external "Upstream SAML IdP"
/// link (not the local login form) so authentication happens at the upstream.</item>
/// <item>Log in at the upstream IdP with username <c>alice</c> and password <c>alice</c>.</item>
/// <item>Verify you land back on the downstream SP's claims page and that a "Logout" link
/// is visible, confirming the downstream session is authenticated.</item>
/// <item>Click the "Upstream SAML IdP logout" link (or navigate to the upstream's own
/// logout page) and confirm the "Yes" prompt if shown.</item>
/// <item>Observe the middle host briefly render a combined federated-signout page (a
/// hidden iframe plus a postMessage-driven completion redirect) before landing on a
/// final logged-out page. The browser may transition through this page in milliseconds,
/// so it can be easy to miss; if you can't see it happen, check the Aspire dashboard's
/// structured logs/console for the middle host to confirm the SLO round trip occurred.</item>
/// <item>Return to the downstream SP and confirm it no longer shows an authenticated
/// session (e.g. the "Logout" link is gone or the claims page redirects to sign-in).</item>
/// </list>
/// </remarks>
public sealed class SamlFederatedLogoutFlow : IScenario
{
    private IdentityServerTestHost? _upstreamIdentityProvider;
    private IdentityServerTestHost? _middleServiceProvider;
    private SamlClientTestHost? _downstreamServiceProvider;

    public string Name => "SamlFederatedLogoutFlow";
    public string Description => "Upstream-IdP-initiated SAML federated logout across an upstream IdP, a middle SP/IdP, and a downstream SAML SP";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        var hosts = await MixedProtocolLogoutHosts.StartAsync(configurator, includeOidcClient: false, topologySuffix: "fed", ct);
        _upstreamIdentityProvider = hosts.UpstreamIdentityProvider;
        _middleServiceProvider = hosts.MiddleServiceProvider;
        _downstreamServiceProvider = hosts.DownstreamServiceProvider;

        Links =
        [
            new ScenarioLink("Start SAML sign-in (downstream secure)", _downstreamServiceProvider.BuildUri("/Secure")),
            new ScenarioLink("Downstream SAML SP home", _downstreamServiceProvider.BuildUri()),
            new ScenarioLink("Middle IdentityServer (SP+IdP)", _middleServiceProvider.BuildUri()),
            new ScenarioLink("Upstream SAML IdP", _upstreamIdentityProvider.BuildUri()),
            new ScenarioLink("Upstream SAML IdP logout", _upstreamIdentityProvider.BuildUri("/Account/Logout"))
        ];
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_downstreamServiceProvider != null)
        {
            await _downstreamServiceProvider.DisposeAsync();
        }

        if (_middleServiceProvider != null)
        {
            await _middleServiceProvider.DisposeAsync();
        }

        if (_upstreamIdentityProvider != null)
        {
            await _upstreamIdentityProvider.DisposeAsync();
        }
    }

    public Command[] GetCommands() => [];

    public sealed class Tests(ScenarioFixture<SamlFederatedLogoutFlow> fixture)
        : PageTest, IClassFixture<ScenarioFixture<SamlFederatedLogoutFlow>>
    {
        public override BrowserNewContextOptions ContextOptions() => new()
        {
            IgnoreHTTPSErrors = true
        };

        [Fact]
        public async Task Federated_login_then_logout()
        {
            const string expectedScriptHash = MixedProtocolLogoutHosts.ExpectedSamlRelayScriptHash;
            var upstreamSsoUrl = fixture.Link("Upstream SAML IdP").ToString().TrimEnd('/') + "/Saml2/SSO";
            var cspViolation = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var upstreamSsoRequested = false;

            Page.Console += (_, message) =>
            {
                if (message.Text.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase)
                    && message.Text.Contains(expectedScriptHash, StringComparison.Ordinal))
                {
                    cspViolation.TrySetResult(message.Text);
                }
            };
            Page.Request += (_, request) =>
            {
                if (request.Url.StartsWith(upstreamSsoUrl + "?", StringComparison.OrdinalIgnoreCase))
                {
                    upstreamSsoRequested = true;
                }
            };

            await Page.GotoAsync(fixture.Link("Start SAML sign-in (downstream secure)").ToString());

            // The middle IdentityServer offers both a local login form and an
            // external "Upstream SAML IdP" link (the dynamic SAML provider). Since
            // this scenario exercises upstream-IdP-initiated federated logout, the
            // user must authenticate at the upstream (not the middle's own local
            // account store, which happens to also define an "alice" user) so the
            // middle records a SAML session for the downstream SP under an upstream
            // identity, and the upstream records a SAML session for the middle.
            var upstreamProviderLink = Page.GetByRole(AriaRole.Link, new() { Name = "Upstream SAML IdP" });
            await upstreamProviderLink.WaitForAsync(new() { Timeout = 10_000 });
            await upstreamProviderLink.ClickAsync();

            var upstreamLogin = Page.GetByPlaceholder("Username").WaitForAsync(new() { Timeout = 10_000 });
            var firstResult = await Task.WhenAny(upstreamLogin, cspViolation.Task);
            if (firstResult == cspViolation.Task)
            {
                var relayForm = Page.Locator("form[name='samlPostBindingSubmit']");
                await Expect(relayForm).ToHaveCountAsync(1);
                (await relayForm.GetAttributeAsync("action")).ShouldBe(upstreamSsoUrl);
                await Expect(relayForm.Locator("input[name='SAMLRequest']")).ToHaveCountAsync(1);
                upstreamSsoRequested.ShouldBeFalse();

                throw new InvalidOperationException(
                    $"The browser remained on the SAML POST relay page at {Page.Url} because CSP blocked " +
                    $"document.forms.samlPostBindingSubmit.submit(). " +
                    $"No request reached the upstream IdP SSO endpoint. Browser console: {await cspViolation.Task}");
            }

            await upstreamLogin;
            Page.Url.ShouldStartWith(fixture.Link("Upstream SAML IdP").ToString().TrimEnd('/'));
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

            await Page.WaitForSelectorAsync("text=Claims");
            var body = await Page.TextContentAsync("body");
            body.ShouldNotBeNull();
            // The downstream SP claims page renders whatever claim types the
            // Sustainsys.Saml2 SP middleware maps the assertion's NameID/attributes
            // to (e.g. ".../claims/nameidentifier"), not the OIDC "sub" claim type
            // literal. Assert on the mapped subject claim to confirm downstream
            // authenticated state.
            body.ShouldContain("nameidentifier");

            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).ToBeVisibleAsync();

            // Upstream-IdP-initiated SAML single logout. Navigating to the upstream
            // IdP's own logout endpoint triggers a local sign-out there, which in turn
            // causes the upstream to send a front-channel SAML LogoutRequest to the
            // middle host (registered as a SAML SP on the upstream). Because the
            // middle also has an active downstream SP session (the SamlClientTestHost),
            // the middle renders its combined federated-signout page (hidden iframe +
            // postMessage + completion redirect) instead of completing the SLO
            // response immediately.
            //
            // All listeners below are attached before logout is triggered, and every
            // assertion is mandatory: the combined page's own inline script can
            // navigate away within milliseconds (on postMessage, or its 5s fallback
            // timer), so DOM locator queries taken after the fact can lose that race.
            // Capturing the HTTP response body (rather than querying the DOM) for the
            // combined page is race-safe because it observes the exact bytes the
            // server sent, regardless of how quickly client-side JS then navigates
            // away from that document.
            const int boundedTimeoutMs = 15_000;
            var upstreamBaseUrl = fixture.Link("Upstream SAML IdP").ToString().TrimEnd('/');

            var combinedPageHtml = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var endSessionCallbackRequested = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var completionRequested = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var terminalUpstreamSloRequested = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pageErrors = new List<string>();
            var consoleMessages = new List<string>();
            var cspViolations = new List<string>();
            var observedRequests = new List<string>();
            var errorResponses = new List<string>();
            string? completionUrl = null;

            Page.PageError += (_, error) => pageErrors.Add(error);
            Page.Console += (_, message) =>
            {
                consoleMessages.Add(message.Text);
                if (message.Text.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase))
                {
                    cspViolations.Add(message.Text);
                }
            };
            Page.Request += (_, request) =>
            {
                observedRequests.Add(request.Url);

                if (request.Url.Contains("connect/endsession/callback?endSessionId=", StringComparison.OrdinalIgnoreCase))
                {
                    endSessionCallbackRequested.TrySetResult(request.Url);
                }
                else if (request.Url.Contains("/saml/slo/sp-complete?logoutId=", StringComparison.OrdinalIgnoreCase))
                {
                    completionRequested.TrySetResult(request.Url);
                }
                else if (request.Url.StartsWith(upstreamBaseUrl + "/Saml2/SLO", StringComparison.OrdinalIgnoreCase))
                {
                    // The middle's outbound LogoutResponse is delivered here, back to
                    // the upstream IdP that initiated the SAML SLO round trip.
                    terminalUpstreamSloRequested.TrySetResult(request.Url);
                }
            };
            Page.Response += async (_, response) =>
            {
                if (response.Status >= 400)
                {
                    var errorBody = "";
                    try
                    {
                        errorBody = await response.TextAsync();
                    }
                    catch (PlaywrightException)
                    {
                    }

                    errorResponses.Add($"{response.Status} {response.Url} BODY[{errorBody}]");
                }

                // The middle's combined federated-signout page is rendered as the
                // direct HTTP response to the inbound SAML LogoutRequest at its
                // dynamic SP module endpoint (/federation/saml-idp/Saml2/Logout).
                if (!response.Url.Contains("federation/saml-idp/Saml2/Logout", StringComparison.Ordinal))
                {
                    return;
                }

                if (!response.Headers.TryGetValue("content-type", out var contentType) ||
                    !contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                try
                {
                    var html = await response.TextAsync();
                    if (html.Contains("id=\"signout-frame\"", StringComparison.Ordinal))
                    {
                        combinedPageHtml.TrySetResult(html);
                    }
                }
                catch (PlaywrightException)
                {
                    // Response body may already be unavailable (e.g. aborted/redirected); ignored.
                }
            };

            string DiagnosticSummary() =>
                $"Current URL: {Page.Url}. " +
                $"Combined page observed: {combinedPageHtml.Task.IsCompletedSuccessfully}. " +
                $"HTML-derived completion URL: {(completionUrl ?? "<not yet parsed>")}. " +
                $"End-session callback observed: {endSessionCallbackRequested.Task.IsCompletedSuccessfully}. " +
                $"Completion request observed: {completionRequested.Task.IsCompletedSuccessfully}. " +
                $"Upstream SLO observed: {terminalUpstreamSloRequested.Task.IsCompletedSuccessfully}. " +
                $"Console messages: {string.Join(" | ", consoleMessages)}. " +
                $"Page errors: {string.Join(" | ", pageErrors)}. " +
                $"Error responses: {string.Join(" | ", errorResponses)}. " +
                $"Observed requests: {string.Join(" | ", observedRequests)}.";

            await Page.GotoAsync(fixture.Link("Upstream SAML IdP logout").ToString());

            var yesButton = Page.GetByRole(AriaRole.Button, new() { Name = "Yes" });
            if (await yesButton.IsVisibleAsync())
            {
                await yesButton.ClickAsync();
            }

            // (1) The middle's combined signout page must have been rendered, with the
            // hidden iframe and its completion-URL data attribute intact.
            string capturedHtml;
            try
            {
                capturedHtml = await combinedPageHtml.Task.WaitAsync(TimeSpan.FromMilliseconds(boundedTimeoutMs));
            }
            catch (TimeoutException ex)
            {
                throw new InvalidOperationException(
                    $"The middle's combined federated-signout page (containing #signout-frame) was never " +
                    $"observed as an HTTP response within {boundedTimeoutMs}ms. {DiagnosticSummary()}",
                    ex);
            }

            capturedHtml.ShouldContain("id=\"signout-frame\"");
            capturedHtml.ShouldContain("data-completion-url=");

            var completionUrlMatch = Regex.Match(capturedHtml, "data-completion-url=\"([^\"]+)\"");
            completionUrlMatch.Success.ShouldBeTrue(
                $"Combined page HTML did not contain a parsable data-completion-url attribute. {DiagnosticSummary()}");
            completionUrl = System.Net.WebUtility.HtmlDecode(completionUrlMatch.Groups[1].Value);
            completionUrl.ShouldContain("/saml/slo/sp-complete?logoutId=");

            // (2) The hidden iframe must have requested the OIDC end-session callback
            // to notify the downstream SP session.
            try
            {
                await endSessionCallbackRequested.Task.WaitAsync(TimeSpan.FromMilliseconds(boundedTimeoutMs));
            }
            catch (TimeoutException ex)
            {
                throw new InvalidOperationException(
                    $"The middle's combined federated-signout page did not trigger the OIDC end-session " +
                    $"callback within {boundedTimeoutMs}ms. Completion URL from HTML: {completionUrl}. " +
                    $"{DiagnosticSummary()}",
                    ex);
            }

            // (3) After the postMessage (or the script's 5s fallback timer) fires, the
            // browser must navigate to the completion endpoint carrying the protected
            // logout handle, which sends the LogoutResponse back to the upstream IdP.
            try
            {
                await completionRequested.Task.WaitAsync(TimeSpan.FromMilliseconds(boundedTimeoutMs));
            }
            catch (TimeoutException ex)
            {
                throw new InvalidOperationException(
                    $"The browser never navigated to the completion endpoint ({completionUrl}) within " +
                    $"{boundedTimeoutMs}ms. {DiagnosticSummary()}",
                    ex);
            }

            // (4) The middle's outbound LogoutResponse must reach the upstream IdP's own
            // SLO endpoint, completing the round trip.
            try
            {
                await terminalUpstreamSloRequested.Task.WaitAsync(TimeSpan.FromMilliseconds(boundedTimeoutMs));
            }
            catch (TimeoutException ex)
            {
                throw new InvalidOperationException(
                    $"The middle never sent its outbound LogoutResponse back to the upstream IdP's SLO " +
                    $"endpoint ({upstreamBaseUrl}/Saml2/SLO) within {boundedTimeoutMs}ms. {DiagnosticSummary()}",
                    ex);
            }

            try
            {
                await Page.WaitForSelectorAsync("text=You are now logged out", new() { Timeout = boundedTimeoutMs });
            }
            catch (TimeoutException ex)
            {
                throw new InvalidOperationException(
                    $"The upstream-initiated SAML logout did not render the final logged-out page within " +
                    $"{boundedTimeoutMs}ms. {DiagnosticSummary()}",
                    ex);
            }

            pageErrors.ShouldBeEmpty(DiagnosticSummary());

            // Even though the flow above completed successfully, a CSP violation
            // anywhere in this second phase (post-login, during the federated logout
            // round trip) indicates the combined signout page's inline script or
            // iframe postMessage handshake was blocked and something else (e.g. the
            // 5s fallback timer) papered over it. Fail loudly rather than let this
            // regress silently.
            cspViolations.ShouldBeEmpty(
                $"CSP violation(s) observed during federated logout even though the flow completed. {DiagnosticSummary()}");

            // No in-memory server log sink is exposed to scenario Tests (see
            // SamlFederatedLogoutFlow class remarks / .weave plan), so server-side
            // error-log assertions are not available here; that coverage is provided
            // by SamlFederatedSignoutTests and the other full integration tests. This
            // assertion instead fails the test on any browser-observable HTTP error
            // (status >= 400) seen anywhere during the logout round trip above, which
            // is the strongest signal a real browser has of a server-side failure.
            errorResponses.ShouldBeEmpty(
                $"Unexpected HTTP error response(s) observed during federated logout. {DiagnosticSummary()}");

            // Give the background federated-signout iframe chain (upstream -> middle ->
            // downstream) time to finish its nested front-channel notifications before
            // checking session state; the visible "logged out" text renders immediately
            // and does not itself guarantee the hidden iframes have completed.
            try
            {
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = boundedTimeoutMs });
            }
            catch (TimeoutException)
            {
                // best effort; fall through to explicit session checks below
            }

            // Downstream session is gone: returning to the downstream home page should
            // no longer show a Logout link, and hitting the Secure page should
            // re-challenge for authentication.
            await Page.GotoAsync(fixture.Link("Downstream SAML SP home").ToString());
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).Not.ToBeVisibleAsync();

            // Middle session is gone too: a fresh re-challenge from the downstream SP
            // must land back on the upstream login form (the middle can no longer
            // silently re-assert an existing session) rather than completing
            // authentication without user interaction.
            await Page.GotoAsync(fixture.Link("Start SAML sign-in (downstream secure)").ToString());
            await Expect(Page.GetByPlaceholder("Username")).ToBeVisibleAsync(new() { Timeout = boundedTimeoutMs });
        }
    }
}
