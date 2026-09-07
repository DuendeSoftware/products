// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Conformance.Infrastructure;

namespace Duende.IdentityServer.Conformance;

/// <summary>
/// Fixture for the OIDC Core conformance test suite.
/// </summary>
public sealed class OidcCoreConformanceSuiteFixture : ConformanceSuiteFixture
{
    private static readonly string PlanConfig = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestPlans", "test-plan-config-oidccore.json"));

    protected override string PlanConfigFilePath => PlanConfig;
}

[CollectionDefinition("OidcCoreConformanceSuite")]
public sealed class OidcCoreConformanceSuiteCollection : ICollectionFixture<OidcCoreConformanceSuiteFixture>;

/// <summary>
/// Fixture for the Logout conformance test suite.
/// </summary>
public sealed class LogoutConformanceSuiteFixture : ConformanceSuiteFixture
{
    private static readonly string PlanConfig = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestPlans", "test-plan-config-logout.json"));

    protected override string PlanConfigFilePath => PlanConfig;
}

[CollectionDefinition("LogoutConformanceSuite")]
public sealed class LogoutConformanceSuiteCollection : ICollectionFixture<LogoutConformanceSuiteFixture>;

/// <summary>
/// Fixture for the Backchannel RP-Initiated Logout conformance test plan.
/// </summary>
public sealed class BackchannelLogoutConformanceSuiteFixture : ConformanceSuiteFixture
{
    private static readonly string PlanConfig = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestPlans", "test-plan-config-logout-backchannel.json"));

    protected override string PlanConfigFilePath => PlanConfig;
}

[CollectionDefinition("BackchannelLogoutConformanceSuite")]
public sealed class BackchannelLogoutConformanceSuiteCollection : ICollectionFixture<BackchannelLogoutConformanceSuiteFixture>;

/// <summary>
/// Fixture for the Frontchannel RP-Initiated Logout conformance test plan.
/// </summary>
public sealed class FrontchannelLogoutConformanceSuiteFixture : ConformanceSuiteFixture
{
    private static readonly string PlanConfig = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestPlans", "test-plan-config-logout-frontchannel.json"));

    protected override string PlanConfigFilePath => PlanConfig;
}

[CollectionDefinition("FrontchannelLogoutConformanceSuite")]
public sealed class FrontchannelLogoutConformanceSuiteCollection : ICollectionFixture<FrontchannelLogoutConformanceSuiteFixture>;

/// <summary>
/// Fixture for the Session Management conformance test plan.
/// </summary>
public sealed class SessionManagementConformanceSuiteFixture : ConformanceSuiteFixture
{
    private static readonly string PlanConfig = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestPlans", "test-plan-config-logout-session-management.json"));

    protected override string PlanConfigFilePath => PlanConfig;
}

[CollectionDefinition("SessionManagementConformanceSuite")]
public sealed class SessionManagementConformanceSuiteCollection : ICollectionFixture<SessionManagementConformanceSuiteFixture>;

/// <summary>
/// Fixture for the FAPI 2.0 conformance test suite.
/// </summary>
public sealed class Fapi2ConformanceSuiteFixture : ConformanceSuiteFixture
{
    private static readonly string PlanConfig = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestPlans", "test-plan-config-fapi2.json"));

    protected override string PlanConfigFilePath => PlanConfig;
}

[CollectionDefinition("Fapi2ConformanceSuite")]
public sealed class Fapi2ConformanceSuiteCollection : ICollectionFixture<Fapi2ConformanceSuiteFixture>;
