// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Conformance.Infrastructure;

namespace Duende.IdentityServer.Conformance;

/// <summary>
/// End-to-end conformance tests for Duende IdentityServer against the OIDF Logout
/// certification test plan.
/// </summary>
[Collection("LogoutConformanceSuite")]
public sealed class LogoutConformanceTests(LogoutConformanceSuiteFixture fixture, ITestOutputHelper output)
    : ConformanceTestBase
{
    protected override ConformanceSuiteFixture Fixture => fixture;
    protected override ITestOutputHelper Output => output;
    protected override string CallbackUrlPrefix => "https://localhost:8443/test/a/duende-is-logout/";
    protected override string InternalHost => "nginx:8444";
    protected override string ExternalHost => "localhost:8444";

    // Discovery endpoint — no browser interaction needed
    [Fact]
    public async Task DiscoveryEndpointVerification()
    {
        var moduleId = await StartModuleAsync("oidcc-rp-initiated-logout-discovery-endpoint-verification");
        await AssertPassedAsync(moduleId);
    }

    [Fact]
    public async Task RpInitiatedLogout()
    {
        await Fixture.LoginAutomation.ResetContextAsync();
        var moduleId = await StartModuleAsync("oidcc-rp-initiated-logout");

        // Module sends a normal authorize request
        var step = await NextStep(moduleId);
        await Authorize(moduleId, step);

        // Module sends an endsession request. IS logs out and redirects to post_logout_redirect_uri
        step = await NextStep(moduleId);
        await EndSession(moduleId, step);

        // Module sends a prompt=none authorize request. IS returns login_required error (user is logged out)
        step = await NextStep(moduleId);
        await Authorize(moduleId, step);

        await AssertPassedAsync(moduleId);
    }

    [Fact]
    public async Task RpInitiatedLogout_NoState()
    {
        await Fixture.LoginAutomation.ResetContextAsync();
        var moduleId = await StartModuleAsync("oidcc-rp-initiated-logout-no-state");

        // Module sends a normal authorize request
        var step = await NextStep(moduleId);
        await Authorize(moduleId, step);

        // Module sends an endsession request. IS logs out and redirects to post_logout_redirect_uri
        step = await NextStep(moduleId);
        await EndSession(moduleId, step);

        // Module sends a prompt=none authorize request. IS returns login_required error (user is logged out)
        step = await NextStep(moduleId);
        await Authorize(moduleId, step);

        await AssertPassedAsync(moduleId);
    }

    [Fact]
    public async Task RpInitiatedLogout_QueryAddedToPostLogoutRedirectUri() => await SimpleLogout("oidcc-rp-initiated-logout-query-added-to-post-logout-redirect-uri", needsScreenshot: true);

    [Fact]
    public async Task RpInitiatedLogout_NoPostLogoutRedirectUri() => await SimpleLogout("oidcc-rp-initiated-logout-no-post-logout-redirect-uri", needsScreenshot: true);

    [Fact]
    public async Task RpInitiatedLogout_NoIdTokenHint() => await SimpleLogout("oidcc-rp-initiated-logout-no-id-token-hint", needsScreenshot: true);

    [Fact]
    public async Task RpInitiatedLogout_NoParams() => await SimpleLogout("oidcc-rp-initiated-logout-no-params", needsScreenshot: true);

    [Fact]
    public async Task RpInitiatedLogout_OnlyState() => await SimpleLogout("oidcc-rp-initiated-logout-only-state", needsScreenshot: true);

    [Fact]
    public async Task RpInitiatedLogout_BadPostLogoutRedirectUri() => await SimpleLogout("oidcc-rp-initiated-logout-bad-post-logout-redirect-uri", needsScreenshot: true);

    [Fact]
    public async Task RpInitiatedLogout_ModifiedIdTokenHint() => await SimpleLogout("oidcc-rp-initiated-logout-modified-id-token-hint", needsScreenshot: true);

    [Fact]
    public async Task RpInitiatedLogout_BadIdTokenHint() => await SimpleLogout("oidcc-rp-initiated-logout-bad-id-token-hint", needsScreenshot: true);
}
