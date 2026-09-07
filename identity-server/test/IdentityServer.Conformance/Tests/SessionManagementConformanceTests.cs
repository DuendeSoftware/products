// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Conformance.Infrastructure;
using Microsoft.Playwright;

namespace Duende.IdentityServer.Conformance;

/// <summary>
/// Conformance tests for the OIDF Session Management certification test plan.
/// </summary>
[Collection("SessionManagementConformanceSuite")]
public sealed class SessionManagementConformanceTests(SessionManagementConformanceSuiteFixture fixture, ITestOutputHelper output)
    : ConformanceTestBase
{
    protected override ConformanceSuiteFixture Fixture => fixture;
    protected override ITestOutputHelper Output => output;
    protected override string CallbackUrlPrefix => "https://localhost:8443/test/a/duende-is-session-mgmt/";
    protected override string InternalHost => "nginx:8444";
    protected override string ExternalHost => "localhost:8444";

    [Fact]
    public async Task DiscoveryEndpointVerification()
    {
        var moduleId = await StartModuleAsync("oidcc-session-management-discovery-endpoint-verification");
        await AssertPassedAsync(moduleId);
    }

    [Fact]
    public async Task SessionManagementRpInitiatedLogout()
    {
        await fixture.LoginAutomation.ResetContextAsync();
        var moduleId = await StartModuleAsync("oidcc-session-management-rp-initiated-logout");
        var openPages = new List<IPage>();
        try
        {
            var urlRewrites = new Dictionary<string, string>
            {
                [$"https://{InternalHost}"] = $"https://{ExternalHost}"
            };

            // First, the suite sends an authorize request
            var step = await NextStep(moduleId);
            await Authorize(moduleId, step);

            // Then the suite sends a session_verify iframe URL to check the session state
            step = await NextStep(moduleId);
            var sessionVerify = step.ShouldBeOfType<ConformanceUrlStep>();
            output.WriteLine($"Navigating to session_verify URL: {sessionVerify.Url}");
            var page = await fixture.LoginAutomation.NavigateToPageAsync(
                sessionVerify.Url, urlRewrites: urlRewrites, log: msg => output.WriteLine(msg));
            openPages.Add(page);
            await AcknowledgeBrowserUrl(moduleId, step);

            // Then the suite sends an end_session request
            step = await NextStep(moduleId);
            await EndSession(moduleId, step);

            // After logout, the suite sends another session_verify to confirm the session is gone
            step = await NextStep(moduleId);
            var postLogoutVerify = step.ShouldBeOfType<ConformanceUrlStep>();
            output.WriteLine($"Navigating to post-logout session_verify URL: {postLogoutVerify.Url}");
            page = await fixture.LoginAutomation.NavigateToPageAsync(
                postLogoutVerify.Url, urlRewrites: urlRewrites, log: msg => output.WriteLine(msg));
            openPages.Add(page);
            await AcknowledgeBrowserUrl(moduleId, step);

            // Suite verifies session state changed and records result
            await AssertPassedAsync(moduleId);
        }
        catch
        {
            await CaptureModuleDetailsAsync(moduleId);
            throw;
        }
        finally
        {
            foreach (var page in openPages)
            {
                await page.CloseAsync();
            }
        }
    }
}
