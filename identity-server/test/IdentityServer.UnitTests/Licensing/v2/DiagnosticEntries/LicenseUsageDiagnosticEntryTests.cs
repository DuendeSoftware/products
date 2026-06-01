// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

namespace IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

public class LicenseUsageDiagnosticEntryTests
{
    [Fact]
    public async Task Handles_Single_Value_For_Each_Entry()
    {
        var licenseUsageTracker = LicenseUsageTracker.CreateForTests();
        var subject = new LicenseUsageDiagnosticEntry(licenseUsageTracker);

        licenseUsageTracker.ClientUsed("Client1");
        licenseUsageTracker.IssuerUsed("https://localhost:50001");
        licenseUsageTracker.KeyManagementUsed();

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var licenseElement = result.RootElement;
        licenseElement.TryGetProperty("LicenseUsageSummary", out var summaryElement).ShouldBeTrue();
        summaryElement.TryGetStringArray("EntitledSkus").ShouldBeEmpty();
        summaryElement.GetProperty("ClientsUsedCount").GetInt32().ShouldBe(1);
        summaryElement.TryGetStringArray("IssuersUsed").ShouldBe(["https://localhost:50001"]);
        summaryElement.TryGetStringArray("FeaturesUsed").ShouldBe(["Automatic Key Management"]);
    }

    [Fact]
    public async Task Handles_Multiple_Values_For_Each_Entry()
    {
        var licenseUsageTracker = LicenseUsageTracker.CreateForTests();
        var subject = new LicenseUsageDiagnosticEntry(licenseUsageTracker);

        licenseUsageTracker.ClientUsed("Client1");
        licenseUsageTracker.ClientUsed("Client2");
        licenseUsageTracker.IssuerUsed("https://localhost:50001");
        licenseUsageTracker.IssuerUsed("https://localhost:50002");
        licenseUsageTracker.KeyManagementUsed();
        licenseUsageTracker.ResourceIsolationUsed();

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var licenseElement = result.RootElement;
        licenseElement.TryGetProperty("LicenseUsageSummary", out var summaryElement).ShouldBeTrue();
        summaryElement.TryGetStringArray("EntitledSkus").ShouldBeEmpty();
        summaryElement.GetProperty("ClientsUsedCount").GetInt32().ShouldBe(2);
        summaryElement.TryGetStringArray("IssuersUsed").ShouldContain(["https://localhost:50001", "https://localhost:50002"]);
        summaryElement.TryGetStringArray("FeaturesUsed").ShouldContain(["Automatic Key Management", "Resource Isolation"]);
    }

    [Fact]
    public async Task EmptySummary_ContainsNoValues()
    {
        var licenseUsageTracker = LicenseUsageTracker.CreateForTests();
        var subject = new LicenseUsageDiagnosticEntry(licenseUsageTracker);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var licenseElement = result.RootElement;
        licenseElement.TryGetProperty("LicenseUsageSummary", out var summaryElement).ShouldBeTrue();
        summaryElement.TryGetStringArray("EntitledSkus").ShouldBeEmpty();
        summaryElement.GetProperty("ClientsUsedCount").GetInt32().ShouldBe(0);
        summaryElement.TryGetStringArray("IssuersUsed").ShouldBeEmpty();
        summaryElement.TryGetStringArray("FeaturesUsed").ShouldBeEmpty();
    }

    [Fact]
    public async Task Handles_Duplicate_Values()
    {
        var licenseUsageTracker = LicenseUsageTracker.CreateForTests();
        var subject = new LicenseUsageDiagnosticEntry(licenseUsageTracker);

        licenseUsageTracker.ClientUsed("Client1");
        licenseUsageTracker.ClientUsed("Client1");
        licenseUsageTracker.IssuerUsed("https://localhost:50001");
        licenseUsageTracker.IssuerUsed("https://localhost:50001");
        licenseUsageTracker.KeyManagementUsed();
        licenseUsageTracker.KeyManagementUsed();

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var licenseElement = result.RootElement;
        licenseElement.TryGetProperty("LicenseUsageSummary", out var summaryElement).ShouldBeTrue();
        summaryElement.TryGetStringArray("EntitledSkus").ShouldBeEmpty();
        summaryElement.GetProperty("ClientsUsedCount").GetInt32().ShouldBe(1);
        summaryElement.TryGetStringArray("IssuersUsed").ShouldBe(["https://localhost:50001"]);
        summaryElement.TryGetStringArray("FeaturesUsed").ShouldBe(["Automatic Key Management"]);
    }
}
