// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Conformance.Infrastructure;

namespace Duende.IdentityServer.Conformance;

/// <summary>
/// Conformance tests for the OIDF Backchannel RP-Initiated Logout certification test plan.
/// </summary>
[Collection("BackchannelLogoutConformanceSuite")]
public sealed class BackchannelLogoutConformanceTests(BackchannelLogoutConformanceSuiteFixture fixture, ITestOutputHelper output)
    : ConformanceTestBase
{
    protected override ConformanceSuiteFixture Fixture => fixture;
    protected override ITestOutputHelper Output => output;
    protected override string CallbackUrlPrefix => "https://localhost:8443/test/a/duende-is-backchannel-logout/";
    protected override string InternalHost => "nginx:8444";
    protected override string ExternalHost => "localhost:8444";

    [Fact]
    public async Task DiscoveryEndpointVerification()
    {
        var moduleId = await StartModuleAsync("oidcc-backchannel-logout-discovery-endpoint-verification");
        await AssertPassedAsync(moduleId);
    }

    [Fact]
    public async Task BackchannelRpInitiatedLogout()
    {
        var moduleId = await StartModuleAsync("oidcc-backchannel-rp-initiated-logout");

        // Module sends a normal authorize request
        var step = await NextStep(moduleId);
        await Authorize(moduleId, step);

        // Module sends an endsession request, which we follow to trigger backchannel logout notification back to the module
        step = await NextStep(moduleId);
        await EndSession(moduleId, step);

        // Module sends a prompt=none authorize request. IS returns login_required error back to module
        step = await NextStep(moduleId);
        await Authorize(moduleId, step);

        // Suite verifies it received the backchannel logout token
        await AssertPassedAsync(moduleId);
    }
}
