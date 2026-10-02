// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Interaction.SharedHosts.MvcClient;
using Duende.IdentityServer.Interaction.SharedHosts.SamlClient;
using Duende.IdentityServer.Interaction.Tests.Infrastructure;
using Duende.IdentityServer.UI.Infra;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using Shouldly;

namespace Duende.IdentityServer.Interaction.Scenarios.SamlFederatedLogout;

/// <summary>
/// Exercises both directions of mixed-protocol logout across the same three-tier
/// topology as <see cref="SamlFederatedLogoutFlow"/>, but this time with a downstream
/// OIDC relying party (<see cref="ClientWebAppTestHost"/>) alongside the downstream
/// SAML SP (<see cref="SamlClientTestHost"/>):
/// <list type="bullet">
/// <item>OIDC-initiated: logout is triggered from the downstream OIDC client's own
/// front channel, and the middle's OIDC end-session flow must also notify the
/// independently authenticated downstream SAML SP session.</item>
/// <item>Upstream-SAML-initiated: logout is triggered at the upstream SAML IdP's own
/// logout endpoint (mirroring <see cref="SamlFederatedLogoutFlow"/>), and the
/// resulting SAML SLO round trip must also notify the downstream OIDC client's
/// front-channel signout-oidc endpoint alongside the downstream SAML SP.</item>
/// </list>
/// </summary>
/// <remarks>
/// Manual walkthrough from the Aspire dashboard (two alternatives, sharing the same
/// running topology started by "MixedProtocolLogoutFlow"):
///
/// <b>Alternative 1: OIDC-initiated logout</b>
/// <list type="number">
/// <item>Start this scenario ("MixedProtocolLogoutFlow") from the Aspire dashboard.</item>
/// <item>Click the "OIDC client home" link, then "Secure" to sign the downstream OIDC
/// client into the middle. Use the middle's local login form (not an external
/// provider) with username <c>alice</c> and password <c>alice</c>.</item>
/// <item>Separately, click the "Start SAML sign-in (downstream secure)" link to sign the
/// downstream SAML SP into the middle. The middle should silently re-assert the
/// existing <c>alice</c> session (no second login prompt) since both clients share
/// the middle's session cookie.</item>
/// <item>Confirm both downstream apps show an authenticated session: the OIDC client's
/// claims page and the SAML SP's claims page (containing a "Logout" link and
/// "nameidentifier" claim, respectively).</item>
/// <item>Return to the OIDC client and click "Logout" to initiate OIDC end-session
/// logout from the middle.</item>
/// <item>Confirm the middle's logout confirmation page, then observe whether the
/// downstream SAML SP session is also terminated as a side effect (revisit the
/// SAML SP home page and check whether its "Logout" link disappears).</item>
/// </list>
///
/// <b>Alternative 2: upstream-SAML-initiated logout</b>
/// <list type="number">
/// <item>Start this scenario ("MixedProtocolLogoutFlow") from the Aspire dashboard
/// (if not already running).</item>
/// <item>Click the "Start SAML sign-in (downstream secure)" link.</item>
/// <item>On the middle IdentityServer's login page, click the external "Upstream SAML
/// IdP" link (not the local login form) so authentication happens at the upstream.</item>
/// <item>Log in at the upstream IdP with username <c>alice</c> and password
/// <c>alice</c>.</item>
/// <item>Verify you land back on the downstream SP's claims page and that a "Logout"
/// link is visible, confirming the downstream SAML session is authenticated.</item>
/// <item>Click the "OIDC client home" link, then "Secure". The middle should silently
/// re-assert the existing upstream-backed session (no second login prompt, and no
/// external-provider redirect) since the OIDC client shares the middle's session
/// cookie with the downstream SAML SP.</item>
/// <item>Confirm the OIDC client's claims page shows an authenticated session with a
/// "Logout" link.</item>
/// <item>Click the "Upstream SAML IdP logout" link (or navigate to the upstream's own
/// logout page) and confirm the "Yes" prompt if shown.</item>
/// <item>Observe the middle host briefly render a combined federated-signout page (a
/// hidden iframe plus a postMessage-driven completion redirect) before landing on a
/// final logged-out page, as in <see cref="SamlFederatedLogoutFlow"/>.</item>
/// <item>Return to the downstream SAML SP and confirm it no longer shows an
/// authenticated session (e.g. the "Logout" link is gone or the claims page redirects
/// to sign-in).</item>
/// <item>Return to the OIDC client and confirm whether its session was also
/// terminated as a side effect of the upstream-initiated SAML SLO round trip (revisit
/// the OIDC client's home page, click "Secure", and check whether it re-challenges
/// for authentication instead of silently re-asserting a session).</item>
/// </list>
/// </remarks>
public sealed class MixedProtocolLogoutFlow : IScenario
{
    private IdentityServerTestHost? _upstreamIdentityProvider;
    private IdentityServerTestHost? _middleServiceProvider;
    private SamlClientTestHost? _downstreamServiceProvider;
    private ClientWebAppTestHost? _downstreamOidcClient;

    public string Name => "MixedProtocolLogoutFlow";
    public string Description => "OIDC-initiated and upstream-SAML-initiated logout across an upstream SAML IdP, a middle SP/IdP, a downstream OIDC client, and a downstream SAML SP";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        var hosts = await MixedProtocolLogoutHosts.StartAsync(configurator, includeOidcClient: true, topologySuffix: "mixed", ct);
        _upstreamIdentityProvider = hosts.UpstreamIdentityProvider;
        _middleServiceProvider = hosts.MiddleServiceProvider;
        _downstreamServiceProvider = hosts.DownstreamServiceProvider;
        _downstreamOidcClient = hosts.DownstreamOidcClient
            ?? throw new InvalidOperationException(
                $"{nameof(MixedProtocolLogoutHosts)}.{nameof(MixedProtocolLogoutHosts.StartAsync)} " +
                "did not return a downstream OIDC client even though includeOidcClient was true.");

        // Union of the links required by both directions of the flow (OIDC-initiated
        // and upstream-SAML-initiated).
        Links =
        [
            _downstreamOidcClient.Link,
            new ScenarioLink("Start SAML sign-in (downstream secure)", _downstreamServiceProvider.BuildUri("/Secure")),
            new ScenarioLink("Downstream SAML SP home", _downstreamServiceProvider.BuildUri()),
            new ScenarioLink("Middle IdentityServer (SP+IdP)", _middleServiceProvider.BuildUri()),
            new ScenarioLink("Upstream SAML IdP", _upstreamIdentityProvider.BuildUri()),
            new ScenarioLink("Upstream SAML IdP logout", _upstreamIdentityProvider.BuildUri("/Account/Logout"))
        ];
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_downstreamOidcClient != null)
        {
            await _downstreamOidcClient.DisposeAsync();
        }

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

    /// <summary>
    /// Both tests share the same running topology (started once via
    /// <see cref="ScenarioFixture{T}"/> as an <see cref="IClassFixture{T}"/>), but
    /// each <see cref="PageTest"/> fact gets its own fresh browser context (and
    /// therefore no shared cookies) from the Playwright xUnit integration. Since
    /// every server-side session in this topology is keyed off browser cookies, a
    /// fresh context has no session to inherit from a prior test, so the two tests
    /// below are independent regardless of xUnit's execution order. xUnit v3 also
    /// only parallelizes across test classes, not across methods within the same
    /// class, so these two facts always run sequentially against the shared hosts.
    /// </summary>
    public sealed class Tests(ScenarioFixture<MixedProtocolLogoutFlow> fixture)
        : PageTest, IClassFixture<ScenarioFixture<MixedProtocolLogoutFlow>>
    {
        public override BrowserNewContextOptions ContextOptions() => new()
        {
            IgnoreHTTPSErrors = true
        };

        [Fact]
        public async Task oidc_client_logout_notifies_downstream_saml_sp()
        {
            const int boundedTimeoutMs = 15_000;

            var oidcClientUrl = fixture.Link("oidc-downstream-webapp-mixed").ToString();
            var oidcSignoutOidcUrl = oidcClientUrl.TrimEnd('/') + "/signout-oidc";
            var downstreamSamlLogoutNotificationUrl =
                fixture.Link("Downstream SAML SP home").ToString().TrimEnd('/') + "/Saml2/Logout";

            var pageErrors = new List<string>();
            var consoleMessages = new List<string>();
            var cspViolations = new List<string>();
            var observedRequests = new List<string>();
            var errorResponses = new List<string>();
            var oidcFrontChannelSignoutRequested = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var downstreamSamlLogoutNotified = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            // All listeners are attached before logout is triggered below, mirroring
            // SamlFederatedLogoutFlow: every relevant network signal must be captured
            // race-safely rather than inferred from DOM state after the fact.
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

                if (request.Url.StartsWith(oidcSignoutOidcUrl, StringComparison.OrdinalIgnoreCase))
                {
                    oidcFrontChannelSignoutRequested.TrySetResult(request.Url);
                }
                else if (request.Url.StartsWith(downstreamSamlLogoutNotificationUrl, StringComparison.OrdinalIgnoreCase))
                {
                    downstreamSamlLogoutNotified.TrySetResult(request.Url);
                }
            };
            string DiagnosticSummary() =>
                $"Current URL: {Page.Url}. " +
                $"OIDC front-channel signout-oidc observed: {oidcFrontChannelSignoutRequested.Task.IsCompletedSuccessfully}. " +
                $"Downstream SAML /Saml2/Logout notification observed: {downstreamSamlLogoutNotified.Task.IsCompletedSuccessfully}. " +
                $"Console messages: {string.Join(" | ", consoleMessages)}. " +
                $"Page errors: {string.Join(" | ", pageErrors)}. " +
                $"Error responses: {string.Join(" | ", errorResponses)}. " +
                $"Observed requests: {string.Join(" | ", observedRequests)}.";

            // 1. Sign the OIDC client into the middle locally as alice/alice.
            await Page.GotoAsync(oidcClientUrl);
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=Claims");
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).ToBeVisibleAsync();

            // 2. Establish the downstream SAML SP session through the same middle
            // session (the middle should silently re-assert the existing alice
            // session, since both clients share the middle's session cookie).
            await Page.GotoAsync(fixture.Link("Start SAML sign-in (downstream secure)").ToString());
            await Page.WaitForSelectorAsync("text=Claims");
            var samlBody = await Page.TextContentAsync("body");
            samlBody.ShouldNotBeNull();
            samlBody.ShouldContain("nameidentifier");

            // 3. Confirm both downstream apps show an authenticated session. The
            // OIDC client only renders "Claims" on its /Secure page (not on its
            // Index/home page), so re-assert the session via /Secure rather than
            // waiting for "Claims" on the home page, which would never appear.
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).ToBeVisibleAsync();
            await Page.GotoAsync(oidcClientUrl);
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=Claims");
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).ToBeVisibleAsync();

            // Attach the error-response listener only now, scoped to the logout phase
            // itself: the setup steps above (login, favicon requests, etc.) can
            // produce browser-generated 404s (e.g. the downstream SAML host has no
            // favicon.ico) that are irrelevant to whether logout itself succeeded, and
            // would otherwise make the errorResponses.ShouldBeEmpty() assertion below
            // flaky. The handler records status and URL synchronously (no awaited
            // response body), so there is no async event-handler race with the
            // assertions performed after this point.
            Page.Response += (_, response) =>
            {
                if (response.Status >= 400)
                {
                    errorResponses.Add($"{response.Status} {response.Url}");
                }
            };

            // 4. Trigger OIDC-initiated end-session logout from the OIDC client, and
            // confirm the middle's logout confirmation prompt.
            await Page.GetByRole(AriaRole.Link, new() { Name = "Logout" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=Logout");
            var yesButton = Page.GetByRole(AriaRole.Button, new() { Name = "Yes" });
            if (await yesButton.IsVisibleAsync())
            {
                await yesButton.ClickAsync();
            }

            // (1) The middle's OIDC end-session flow must have driven the browser
            // through the OIDC client's own front-channel signout-oidc endpoint.
            try
            {
                await oidcFrontChannelSignoutRequested.Task.WaitAsync(TimeSpan.FromMilliseconds(boundedTimeoutMs));
            }
            catch (TimeoutException ex)
            {
                throw new InvalidOperationException(
                    $"The middle's OIDC end-session logout never triggered the downstream OIDC client's " +
                    $"front-channel signout-oidc endpoint ({oidcSignoutOidcUrl}) within {boundedTimeoutMs}ms. " +
                    $"{DiagnosticSummary()}",
                    ex);
            }

            // (2) The middle's OIDC end-session flow must also notify the
            // independently authenticated downstream SAML SP session, terminating
            // it as a side effect of the OIDC-initiated logout.
            try
            {
                await downstreamSamlLogoutNotified.Task.WaitAsync(TimeSpan.FromMilliseconds(boundedTimeoutMs));
            }
            catch (TimeoutException ex)
            {
                throw new InvalidOperationException(
                    $"The middle's OIDC-initiated end-session logout never notified the downstream SAML SP " +
                    $"session (no request observed to '{downstreamSamlLogoutNotificationUrl}') within " +
                    $"{boundedTimeoutMs}ms. {DiagnosticSummary()}",
                    ex);
            }

            try
            {
                await Page.WaitForSelectorAsync("text=logged out", new() { Timeout = boundedTimeoutMs });
            }
            catch (TimeoutException ex)
            {
                throw new InvalidOperationException(
                    $"The OIDC-initiated logout did not render the final logged-out page within " +
                    $"{boundedTimeoutMs}ms. {DiagnosticSummary()}",
                    ex);
            }

            // Give the background front-channel logout iframe(s) time to finish
            // before checking session state or error responses; the visible "logged
            // out" text renders immediately and does not itself guarantee hidden
            // iframes (and any error responses they may produce) have completed.
            try
            {
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = boundedTimeoutMs });
            }
            catch (TimeoutException)
            {
                // best effort; fall through to explicit session checks below
            }

            pageErrors.ShouldBeEmpty(DiagnosticSummary());
            cspViolations.ShouldBeEmpty(
                $"CSP violation(s) observed during OIDC-initiated mixed logout even though the flow completed. {DiagnosticSummary()}");
            errorResponses.ShouldBeEmpty(
                $"Unexpected HTTP error response(s) observed during OIDC-initiated mixed logout. {DiagnosticSummary()}");

            // OIDC client session is gone: returning to its home page and hitting
            // Secure should re-challenge for authentication instead of silently
            // re-asserting a session.
            await Page.GotoAsync(oidcClientUrl);
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Expect(Page.GetByPlaceholder("Username")).ToBeVisibleAsync(new() { Timeout = boundedTimeoutMs });

            // Downstream SAML SP session is also gone, since the notification was
            // observed above as part of the middle's OIDC-initiated logout.
            await Page.GotoAsync(fixture.Link("Downstream SAML SP home").ToString());
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).Not.ToBeVisibleAsync();

            await Page.GotoAsync(fixture.Link("Start SAML sign-in (downstream secure)").ToString());
            await Expect(Page.GetByPlaceholder("Username")).ToBeVisibleAsync(new() { Timeout = boundedTimeoutMs });
        }

        [Fact]
        public async Task upstream_saml_logout_notifies_downstream_saml_sp_and_oidc_client()
        {
            const string expectedScriptHash = MixedProtocolLogoutHosts.ExpectedSamlRelayScriptHash;
            const int boundedTimeoutMs = 15_000;

            var upstreamSsoUrl = fixture.Link("Upstream SAML IdP").ToString().TrimEnd('/') + "/Saml2/SSO";
            var upstreamBaseUrl = fixture.Link("Upstream SAML IdP").ToString().TrimEnd('/');
            var oidcClientUrl = fixture.Link("oidc-downstream-webapp-mixed").ToString();
            var oidcSignoutOidcUrl = oidcClientUrl.TrimEnd('/') + "/signout-oidc";
            var downstreamSamlBaseUrl = fixture.Link("Downstream SAML SP home").ToString().TrimEnd('/');
            var downstreamSamlLogoutNotificationUrl = downstreamSamlBaseUrl + "/Saml2/Logout";

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

            // 1. Establish the downstream SAML SP session first, authenticating at
            // the upstream SAML IdP (not the middle's local login form), so the
            // middle records a SAML session for the downstream SP under an upstream
            // identity, and the upstream records a SAML session for the middle.
            await Page.GotoAsync(fixture.Link("Start SAML sign-in (downstream secure)").ToString());

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
            var samlBody = await Page.TextContentAsync("body");
            samlBody.ShouldNotBeNull();
            samlBody.ShouldContain("nameidentifier");
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).ToBeVisibleAsync();

            // 2. Ride the middle's session established via the upstream identity with
            // the OIDC client: navigating to the OIDC client and clicking "Secure"
            // should silently re-assert the existing upstream-backed session (no
            // second login prompt, no external-provider redirect) since the OIDC
            // client shares the middle's session cookie with the downstream SAML SP.
            await Page.GotoAsync(oidcClientUrl);
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=Claims");
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).ToBeVisibleAsync();

            // 3. Confirm both downstream apps show an authenticated session before
            // triggering logout.
            await Page.GotoAsync(fixture.Link("Downstream SAML SP home").ToString());
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).ToBeVisibleAsync();

            // Upstream-IdP-initiated SAML single logout. Navigating to the upstream
            // IdP's own logout endpoint triggers a local sign-out there, which in turn
            // causes the upstream to send a front-channel SAML LogoutRequest to the
            // middle host (registered as a SAML SP on the upstream). Because the
            // middle also has active downstream SP sessions (the SamlClientTestHost
            // and, in this scenario, the OIDC client riding the same middle session),
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
            var combinedPageHtml = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var endSessionCallbackRequested = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var completionRequested = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var terminalUpstreamSloRequested = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var oidcFrontChannelSignoutRequested = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var downstreamSamlLogoutNotified = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
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
                else if (request.Url.StartsWith(oidcSignoutOidcUrl, StringComparison.OrdinalIgnoreCase))
                {
                    oidcFrontChannelSignoutRequested.TrySetResult(request.Url);
                }
                else if (request.Url.StartsWith(downstreamSamlLogoutNotificationUrl, StringComparison.OrdinalIgnoreCase))
                {
                    // Match only the actual downstream SAML SP host's own
                    // /Saml2/Logout notification endpoint, so this cannot
                    // accidentally match the middle's inbound dynamic-provider
                    // endpoint (/federation/saml-idp/Saml2/Logout), which also
                    // contains the "/Saml2/Logout" fragment but is a different host.
                    downstreamSamlLogoutNotified.TrySetResult(request.Url);
                }
            };
            // Asynchronously captures the combined signout page's HTML body. Kept
            // separate from the (synchronous) errorResponses recording below so that
            // error-response bookkeeping can never race with assertions. The relevant
            // completion is tracked via the combinedPageHtml TaskCompletionSource,
            // which the assertions below explicitly await before inspecting its
            // result; any exception raised while reading the response body is caught
            // internally, so this method is safe to fire-and-forget without a shared
            // task collection (which would otherwise risk a collection-modified
            // exception if a late response arrived while something enumerated it).
            async Task CaptureCombinedPageHtmlAsync(IResponse response)
            {
                // The middle's combined federated-signout page is rendered as the
                // direct HTTP response to the inbound SAML LogoutRequest at its
                // dynamic SP module endpoint (/federation/saml-idp/Saml2/Logout).
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
            }

            string DiagnosticSummary() =>
                $"Current URL: {Page.Url}. " +
                $"Combined page observed: {combinedPageHtml.Task.IsCompletedSuccessfully}. " +
                $"HTML-derived completion URL: {(completionUrl ?? "<not yet parsed>")}. " +
                $"End-session callback observed: {endSessionCallbackRequested.Task.IsCompletedSuccessfully}. " +
                $"Completion request observed: {completionRequested.Task.IsCompletedSuccessfully}. " +
                $"Upstream SLO observed: {terminalUpstreamSloRequested.Task.IsCompletedSuccessfully}. " +
                $"OIDC front-channel signout-oidc observed: {oidcFrontChannelSignoutRequested.Task.IsCompletedSuccessfully}. " +
                $"Downstream SAML /Saml2/Logout notification observed: {downstreamSamlLogoutNotified.Task.IsCompletedSuccessfully}. " +
                $"Console messages: {string.Join(" | ", consoleMessages)}. " +
                $"Page errors: {string.Join(" | ", pageErrors)}. " +
                $"Error responses: {string.Join(" | ", errorResponses)}. " +
                $"Observed requests: {string.Join(" | ", observedRequests)}.";

            // Attach the error-response listener only now, scoped to the logout phase
            // itself: the setup steps above (upstream login, SAML sign-in, OIDC
            // sign-in, favicon requests, etc.) can produce browser-generated 404s that
            // are irrelevant to whether logout itself succeeded, and would otherwise
            // make the errorResponses.ShouldBeEmpty() assertion below flaky. The
            // handler records status and URL synchronously (no awaited response
            // body), so there is no async event-handler race with the assertions
            // performed after this point. The combined signout page's HTML body is
            // still captured asynchronously (see CaptureCombinedPageHtmlAsync above),
            // but that capture is tracked and explicitly awaited via
            // combinedPageHtml.Task before any assertion inspects its result.
            Page.Response += (_, response) =>
            {
                if (response.Status >= 400)
                {
                    errorResponses.Add($"{response.Status} {response.Url}");
                }

                if (response.Url.Contains("federation/saml-idp/Saml2/Logout", StringComparison.Ordinal))
                {
                    // Fire-and-forget: CaptureCombinedPageHtmlAsync catches its own
                    // exceptions internally, and completion is observed via
                    // combinedPageHtml.Task, awaited below.
                    _ = CaptureCombinedPageHtmlAsync(response);
                }
            };

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

            // (5) The middle's front-channel logout iframe set must also notify the
            // downstream OIDC client's own front-channel signout-oidc endpoint,
            // terminating its session at the middle as a side effect of the
            // upstream-initiated SAML SLO round trip.
            try
            {
                await oidcFrontChannelSignoutRequested.Task.WaitAsync(TimeSpan.FromMilliseconds(boundedTimeoutMs));
            }
            catch (TimeoutException ex)
            {
                throw new InvalidOperationException(
                    $"The upstream-initiated SAML SLO round trip never triggered the downstream OIDC " +
                    $"client's front-channel signout-oidc endpoint ({oidcSignoutOidcUrl}) within " +
                    $"{boundedTimeoutMs}ms. {DiagnosticSummary()}",
                    ex);
            }

            // (6) The middle's front-channel logout iframe set must also notify the
            // downstream SAML SP's own /Saml2/Logout endpoint directly (distinct
            // from the middle's inbound dynamic-provider endpoint at
            // /federation/saml-idp/Saml2/Logout), terminating that session too.
            try
            {
                await downstreamSamlLogoutNotified.Task.WaitAsync(TimeSpan.FromMilliseconds(boundedTimeoutMs));
            }
            catch (TimeoutException ex)
            {
                throw new InvalidOperationException(
                    $"The upstream-initiated SAML SLO round trip never notified the downstream SAML SP's " +
                    $"own logout endpoint ({downstreamSamlLogoutNotificationUrl}) within {boundedTimeoutMs}ms. " +
                    $"{DiagnosticSummary()}",
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

            // Give the background federated-signout iframe chain (upstream -> middle ->
            // downstream) time to finish its nested front-channel notifications before
            // checking session state or error responses; the visible "logged out" text
            // renders immediately and does not itself guarantee the hidden iframes
            // (and any error responses they may produce) have completed.
            try
            {
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = boundedTimeoutMs });
            }
            catch (TimeoutException)
            {
                // best effort; fall through to explicit session checks below
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

            // No in-memory server log sink is exposed to scenario Tests, so
            // server-side error-log assertions are not available here; that coverage
            // is provided by SamlFederatedSignoutTests and the other full integration
            // tests. This assertion instead fails the test on any browser-observable
            // HTTP error (status >= 400) seen anywhere during the logout round trip
            // above, which is the strongest signal a real browser has of a
            // server-side failure.
            errorResponses.ShouldBeEmpty(
                $"Unexpected HTTP error response(s) observed during federated logout. {DiagnosticSummary()}");

            // Downstream SAML session is gone: returning to the downstream home page
            // should no longer show a Logout link, and hitting the Secure page should
            // re-challenge for authentication.
            await Page.GotoAsync(fixture.Link("Downstream SAML SP home").ToString());
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).Not.ToBeVisibleAsync();

            // The upstream-initiated SAML SLO round trip also terminates the
            // downstream OIDC client's session, established above (see the
            // oidcFrontChannelSignoutRequested wait). Confirm it re-challenges
            // for authentication instead of silently re-asserting a session.
            await Page.GotoAsync(oidcClientUrl);
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            try
            {
                await Expect(Page.GetByPlaceholder("Username")).ToBeVisibleAsync(new() { Timeout = boundedTimeoutMs });
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"After upstream-initiated SAML SLO completed, the downstream OIDC client silently " +
                    $"re-asserted its existing session (no re-challenge for authentication) instead of " +
                    $"requiring a fresh login. {DiagnosticSummary()}",
                    ex);
            }

            // Middle session is gone too: a fresh SAML challenge must no longer be
            // silently satisfied by the middle's own session. As established in step 1
            // of this test, the middle's login page never auto-redirects to the
            // upstream provider; it renders its own local login form alongside an
            // external "Upstream SAML IdP" link. So the correct re-challenge signal
            // is that this external link is present again (proving the middle has no
            // session to silently re-assert) and, after following it, authentication
            // actually happens at the upstream IdP's own host rather than completing
            // without user interaction. Asserting only "a Username field is visible"
            // right after the redirect (without clicking the external link first)
            // would trivially pass against the middle's own local login form even if
            // the middle silently re-asserted nothing, so that alone cannot
            // distinguish a genuine re-challenge from a false pass.
            await Page.GotoAsync(fixture.Link("Start SAML sign-in (downstream secure)").ToString());
            var freshUpstreamProviderLink = Page.GetByRole(AriaRole.Link, new() { Name = "Upstream SAML IdP" });
            await freshUpstreamProviderLink.WaitForAsync(new() { Timeout = boundedTimeoutMs });
            await freshUpstreamProviderLink.ClickAsync();
            await Expect(Page.GetByPlaceholder("Username")).ToBeVisibleAsync(new() { Timeout = boundedTimeoutMs });
            Page.Url.StartsWith(upstreamBaseUrl, StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
                $"Expected the fresh SAML challenge, after following the external upstream provider link, to " +
                $"require re-authentication at the upstream IdP ({upstreamBaseUrl}), but the browser landed on " +
                $"{Page.Url} instead, which suggests the middle silently re-asserted a stale session rather " +
                $"than requiring a fresh upstream login. {DiagnosticSummary()}");
        }
    }
}
