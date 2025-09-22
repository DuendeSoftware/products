// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

public class LicenseUsageDiagnosticEntryTests
{
    [Fact]
    public async Task Handles_Single_Value_For_Each_Entry()
    {
        var licenseAccessor = new LicenseAccessor(() => null, new NullLogger<LicenseAccessor>());
        var licenseUsageTracker = new LicenseUsageTracker(licenseAccessor, new NullLoggerFactory());
        var subject = new LicenseUsageDiagnosticEntry(licenseUsageTracker);

        licenseUsageTracker.ClientUsed("Client1");
        licenseUsageTracker.IssuerUsed("https://localhost:50001");
        licenseUsageTracker.FeatureUsed(IdentityServerLicenseFeature.KeyManagement);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var licenseElement = result.RootElement;
        licenseElement.TryGetProperty("LicenseUsageSummary", out var summaryElement).ShouldBeTrue();
        summaryElement.GetProperty("LicenseEdition").GetString().ShouldBe("None");
        summaryElement.GetProperty("ClientsUsedCount").GetInt32().ShouldBe(1);
        summaryElement.TryGetStringArray("IssuersUsed").ShouldBe(["https://localhost:50001"]);
        summaryElement.TryGetStringArray("FeaturesUsed").ShouldBe(["KeyManagement"]);
    }

    [Fact]
    public async Task Handles_Multiple_Values_For_Each_Entry()
    {
        var licenseAccessor = new LicenseAccessor(() => null, new NullLogger<LicenseAccessor>());
        var licenseUsageTracker = new LicenseUsageTracker(licenseAccessor, new NullLoggerFactory());
        var subject = new LicenseUsageDiagnosticEntry(licenseUsageTracker);

        licenseUsageTracker.ClientUsed("Client1");
        licenseUsageTracker.ClientUsed("Client2");
        licenseUsageTracker.IssuerUsed("https://localhost:50001");
        licenseUsageTracker.IssuerUsed("https://localhost:50002");
        licenseUsageTracker.FeatureUsed(IdentityServerLicenseFeature.KeyManagement);
        licenseUsageTracker.FeatureUsed(IdentityServerLicenseFeature.ResourceIsolation);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var licenseElement = result.RootElement;
        licenseElement.TryGetProperty("LicenseUsageSummary", out var summaryElement).ShouldBeTrue();
        summaryElement.GetProperty("LicenseEdition").GetString().ShouldBe("None");
        summaryElement.GetProperty("ClientsUsedCount").GetInt32().ShouldBe(2);
        summaryElement.TryGetStringArray("IssuersUsed").ShouldContain(["https://localhost:50001", "https://localhost:50002"]);
        summaryElement.TryGetStringArray("FeaturesUsed").ShouldContain(["KeyManagement", "ResourceIsolation"]);
    }

    [Fact]
    public async Task EmptySummary_ContainsNoValues()
    {
        var licenseAccessor = new LicenseAccessor(() => null, new NullLogger<LicenseAccessor>());
        var licenseUsageTracker = new LicenseUsageTracker(licenseAccessor, new NullLoggerFactory());
        var subject = new LicenseUsageDiagnosticEntry(licenseUsageTracker);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var licenseElement = result.RootElement;
        licenseElement.TryGetProperty("LicenseUsageSummary", out var summaryElement).ShouldBeTrue();
        summaryElement.GetProperty("LicenseEdition").GetString().ShouldBe("None");
        summaryElement.GetProperty("ClientsUsedCount").GetInt32().ShouldBe(0);
        summaryElement.TryGetStringArray("IssuersUsed").ShouldBeEmpty();
        summaryElement.TryGetStringArray("FeaturesUsed").ShouldBeEmpty();
    }

    [Fact]
    public async Task Handles_Duplicate_Values()
    {
        var licenseAccessor = new LicenseAccessor(() => null, new NullLogger<LicenseAccessor>());
        var licenseUsageTracker = new LicenseUsageTracker(licenseAccessor, new NullLoggerFactory());
        var subject = new LicenseUsageDiagnosticEntry(licenseUsageTracker);

        licenseUsageTracker.ClientUsed("Client1");
        licenseUsageTracker.ClientUsed("Client1");
        licenseUsageTracker.IssuerUsed("https://localhost:50001");
        licenseUsageTracker.IssuerUsed("https://localhost:50001");
        licenseUsageTracker.FeatureUsed(IdentityServerLicenseFeature.KeyManagement);
        licenseUsageTracker.FeatureUsed(IdentityServerLicenseFeature.KeyManagement);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var licenseElement = result.RootElement;
        licenseElement.TryGetProperty("LicenseUsageSummary", out var summaryElement).ShouldBeTrue();
        summaryElement.GetProperty("LicenseEdition").GetString().ShouldBe("None");
        summaryElement.GetProperty("ClientsUsedCount").GetInt32().ShouldBe(1);
        summaryElement.TryGetStringArray("IssuersUsed").ShouldBe(["https://localhost:50001"]);
        summaryElement.TryGetStringArray("FeaturesUsed").ShouldBe(["KeyManagement"]);
    }


    [Fact]
    public async Task Different_License_Edition_Is_Reflected()
    {
        var options = new IdentityServerOptions
        {
            LicenseKey = "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJFbnRlcnByaXNlIiwiaWQiOiI2Njg1In0.UgguIFVBciR8lpTF5RuM3FNcIm8m8wGR4Mt0xOCgo-XknFwXBpxOfr0zVjciGboteOl9AFtrqZLopEjsYXGFh2dkl5AzRyq--Ai5y7aezszlMpq8SkjRRCeBUYLNnEO41_YnfjYhNrcmb0Jx9wMomCv74vU3f8Hulz1ppWtoL-MVcGq0fhv_KOCP49aImCgiawPJ6a_bfs2C1QLpj-GG411OhdyrO9QLIH_We4BEvRUyajraisljB1VQzC8Q6188Mm_BLwl4ZENPaoNE4egiqTAuoTS5tb1l732-CGZwpGuU80NSpJbrUc6jd3rVi_pNf_1rH-O4Xt0HRCWiNCDYgg"
        };
        var licenseAccessor = new LicenseAccessor(() => options.LicenseKey, new NullLogger<LicenseAccessor>());
        var licenseUsageTracker = new LicenseUsageTracker(licenseAccessor, new NullLoggerFactory());
        var subject = new LicenseUsageDiagnosticEntry(licenseUsageTracker);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var licenseElement = result.RootElement;
        licenseElement.TryGetProperty("LicenseUsageSummary", out var summaryElement).ShouldBeTrue();
        summaryElement.GetProperty("LicenseEdition").GetString().ShouldBe("Enterprise");
    }
}
