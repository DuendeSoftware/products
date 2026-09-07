// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Conformance.Infrastructure;

namespace Duende.IdentityServer.Conformance;

/// <summary>
/// End-to-end conformance tests for Duende IdentityServer against the OIDF OIDC Core
/// certification test plan.
/// </summary>
[Collection("OidcCoreConformanceSuite")]
public sealed class OidcCoreConformanceTests(OidcCoreConformanceSuiteFixture fixture, ITestOutputHelper output)
    : ConformanceTestBase
{
    protected override ConformanceSuiteFixture Fixture => fixture;
    protected override ITestOutputHelper Output => output;
    protected override string CallbackUrlPrefix => "https://localhost:8443/test/a/duende-is-oidc-core/callback";
    protected override string InternalHost => "nginx:8444";
    protected override string ExternalHost => "localhost:8444";

    [Fact] public async Task Server() => await SimpleLogin("oidcc-server");
    [Fact] public async Task UserinfoGet() => await SimpleLogin("oidcc-userinfo-get");
    [Fact] public async Task UserinfoPostHeader() => await SimpleLogin("oidcc-userinfo-post-header");
    [Fact] public async Task UserinfoPostBody() => await SimpleLogin("oidcc-userinfo-post-body");
    [Fact] public async Task EnsureRequestWithoutNonceSucceeds() => await SimpleLogin("oidcc-ensure-request-without-nonce-succeeds-for-code-flow");
    [Fact] public async Task ScopeProfile() => await SimpleLogin("oidcc-scope-profile");
    [Fact] public async Task ScopeEmail() => await SimpleLogin("oidcc-scope-email");
    [Fact] public async Task ScopeAddress() => await SimpleLogin("oidcc-scope-address");
    [Fact] public async Task ScopePhone() => await SimpleLogin("oidcc-scope-phone");
    [Fact] public async Task ScopeAll() => await SimpleLogin("oidcc-scope-all");
    [Fact] public async Task AlternateHappyFlow() => await SimpleLogin("oidcc-alternate-happy-flow");
    [Fact] public async Task DisplayPage() => await SimpleLogin("oidcc-display-page");
    [Fact] public async Task DisplayPopup() => await SimpleLogin("oidcc-display-popup");
    [Fact] public async Task PromptNoneNotLoggedIn() => await SimpleLogin("oidcc-prompt-none-not-logged-in");
    [Fact] public async Task EnsureRequestWithUnknownParameterSucceeds() => await SimpleLogin("oidcc-ensure-request-with-unknown-parameter-succeeds");
    [Fact] public async Task LoginHint() => await SimpleLogin("oidcc-login-hint");
    [Fact] public async Task UiLocales() => await SimpleLogin("oidcc-ui-locales");
    [Fact] public async Task ClaimsLocales() => await SimpleLogin("oidcc-claims-locales");
    [Fact] public async Task EnsureRequestWithAcrValuesSucceeds() => await SimpleLogin("oidcc-ensure-request-with-acr-values-succeeds");
    [Fact] public async Task CodeReuse() => await SimpleLogin("oidcc-codereuse");
    [Fact] public async Task CodeReuse30Seconds() => await SimpleLogin("oidcc-codereuse-30seconds");
    [Fact] public async Task EnsurePostRequestSucceeds() => await SimpleLogin("oidcc-ensure-post-request-succeeds");
    [Fact] public async Task ServerClientSecretPost() => await SimpleLogin("oidcc-server-client-secret-post");
    [Fact] public async Task UnsignedRequestObject() => await SimpleLogin("oidcc-unsigned-request-object-supported-correctly-or-rejected-as-unsupported");
    [Fact] public async Task ClaimsEssential() => await SimpleLogin("oidcc-claims-essential");
    [Fact] public async Task EnsureRequestObjectWithRedirectUri() => await SimpleLogin("oidcc-ensure-request-object-with-redirect-uri");
    [Fact] public async Task EnsureRequestWithValidPkceSucceeds() => await SimpleLogin("oidcc-ensure-request-with-valid-pkce-succeeds");

    [Fact]
    public async Task EnsureRegisteredRedirectUri()
    {
        await fixture.LoginAutomation.ResetContextAsync();
        var moduleId = await StartModuleAsync("oidcc-ensure-registered-redirect-uri");

        // The conformance suite sends an authorize with an unregistered redirect_uri.
        // Expect IS to show an error page
        var step = await NextStep(moduleId);
        await AssertErrorPageAsync(moduleId, step);
    }

    [Fact]
    public async Task RefreshToken()
    {
        await fixture.LoginAutomation.ResetContextAsync();
        var moduleId = await StartModuleAsync("oidcc-refresh-token");

        // This module uses two clients, which each obtain refresh tokens
        // Complete both logins, then let AssertPassedAsync finish.
        var step1 = await NextStep(moduleId);
        await Authorize(moduleId, step1);

        var step2 = await NextStep(moduleId);
        await Authorize(moduleId, step2);

        await AssertPassedAsync(moduleId, step2, navigateBrowserUrls: false);
    }

    [Fact]
    public async Task ResponseTypeMissing()
    {
        await fixture.LoginAutomation.ResetContextAsync();
        var moduleId = await StartModuleAsync("oidcc-response-type-missing");

        // The conformance suite sends an authorize request with no response_type.
        // Expect IS to show an error page
        var step = await NextStep(moduleId);
        await AssertErrorPageAsync(moduleId, step);
    }

    [Fact]
    public async Task PromptLogin()
    {
        await fixture.LoginAutomation.ResetContextAsync();
        var moduleId = await StartModuleAsync("oidcc-prompt-login");

        // Login normally
        var step1 = await NextStep(moduleId);
        await Authorize(moduleId, step1);

        // The suite sends a second authorize request with prompt=login, and we login again.
        var step2 = await NextStep(moduleId);
        await Authorize(moduleId, step2);

        // The suite creates an image placeholder for the re-auth prompt screenshot.
        await Fixture.Client.UploadPlaceholderScreenshotAsync(moduleId);
        await AssertPassedAsync(moduleId, step2);
    }

    [Fact]
    public async Task PromptNoneLoggedIn()
    {
        await fixture.LoginAutomation.ResetContextAsync();
        var moduleId = await StartModuleAsync("oidcc-prompt-none-logged-in");

        // Module redirects to authorize
        var step1 = await NextStep(moduleId);
        await Authorize(moduleId, step1);

        // Module redirects to authorize again
        var step2 = await NextStep(moduleId);
        await Authorize(moduleId, step2);

        await AssertPassedAsync(moduleId, step2);
    }

    // -------------------------------------------------------------------------
    // Max-age=1: login, wait 2s, then a second redirect requiring re-auth
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MaxAge1()
    {
        await fixture.LoginAutomation.ResetContextAsync();
        var moduleId = await StartModuleAsync("oidcc-max-age-1");

        var step1 = await NextStep(moduleId);
        await Authorize(moduleId, step1);

        // Browser URL re-check of the first auth: acknowledge without navigating.
        // Navigating would issue a new code (user is still logged in) and the conformance
        // suite would use that code for the second authorization, bypassing the max_age=1
        // re-auth requirement and producing the same auth_time.
        var step2 = await NextStep(moduleId);
        await AcknowledgeBrowserUrl(moduleId, step2);

        // The suite sends the second authorize (with max_age=1) directly server-side
        // after a 2-second wait. No further browser interaction is needed.
        // The suite creates an image placeholder for the re-auth prompt screenshot.
        await Fixture.Client.UploadPlaceholderScreenshotAsync(moduleId);
        await AssertPassedAsync(moduleId, step2, navigateBrowserUrls: false);
    }

    // -------------------------------------------------------------------------
    // Max-age=10000: login, then a browser URL re-auth (session still valid)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MaxAge10000()
    {
        await fixture.LoginAutomation.ResetContextAsync();
        var moduleId = await StartModuleAsync("oidcc-max-age-10000");

        var step1 = await NextStep(moduleId);
        await Authorize(moduleId, step1);

        var step2 = await NextStep(moduleId);
        // max_age=10000 — session still valid, IS silently re-auths via redirect
        await Authorize(moduleId, step2);

        await AssertPassedAsync(moduleId, step2);
    }

    // -------------------------------------------------------------------------
    // ID token hint: login, then a second redirect using the id_token as hint
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IdTokenHint()
    {
        await fixture.LoginAutomation.ResetContextAsync();
        var moduleId = await StartModuleAsync("oidcc-id-token-hint");

        var step1 = await NextStep(moduleId);
        await Authorize(moduleId, step1);

        var step2 = await NextStep(moduleId);
        await Authorize(moduleId, step2);

        // Don't navigate pending browser URL re-checks — they would issue new codes
        // and corrupt the conformance suite's state.
        await AssertPassedAsync(moduleId, step2, navigateBrowserUrls: false);
    }
}
