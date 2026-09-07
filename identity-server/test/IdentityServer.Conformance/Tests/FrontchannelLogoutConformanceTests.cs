// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Conformance.Infrastructure;

namespace Duende.IdentityServer.Conformance;

/// <summary>
/// Conformance tests for the OIDF Frontchannel RP-Initiated Logout certification test plan.
/// </summary>
[Collection("FrontchannelLogoutConformanceSuite")]
public sealed class FrontchannelLogoutConformanceTests(FrontchannelLogoutConformanceSuiteFixture fixture, ITestOutputHelper output)
    : ConformanceTestBase
{
    protected override ConformanceSuiteFixture Fixture => fixture;
    protected override ITestOutputHelper Output => output;
    protected override string CallbackUrlPrefix => "https://localhost:8443/test/a/duende-is-frontchannel-logout/";
    protected override string InternalHost => "nginx:8444";
    protected override string ExternalHost => "localhost:8444";

    [Fact]
    public async Task DiscoveryEndpointVerification()
    {
        var moduleId = await StartModuleAsync("oidcc-frontchannel-logout-discovery-endpoint-verification");
        await AssertPassedAsync(moduleId);
    }

    [Fact]
    public async Task FrontchannelRpInitiatedLogout()
    {
        var moduleId = await StartModuleAsync("oidcc-frontchannel-rp-initiated-logout");

        // Module sends a normal authorize request
        var step = await NextStep(moduleId);
        await Authorize(moduleId, step);

        // Module sends an endsession request, which triggers frontchannel logout
        step = await NextStep(moduleId);
        await EndSession(moduleId, step);

        // Module sends a prompt=none authorize request. IS returns login_required error back to module
        step = await NextStep(moduleId);
        await Authorize(moduleId, step);

        // Suite verifies the frontchannel logout iframe was rendered
        await AssertPassedAsync(moduleId);
    }
}
