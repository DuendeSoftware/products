// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Licensing.V2;

namespace IdentityServer.UnitTests.Licensing.V2;

public class LicenseUsageTests
{
    private LicenseUsageTracker _licenseUsageTracker;

    private void Init() => _licenseUsageTracker = LicenseUsageTracker.CreateForTests();

    [Fact]
    public void used_features_are_reported()
    {
        Init();

        _licenseUsageTracker.KeyManagementUsed();
        _licenseUsageTracker.ParUsed();
        _licenseUsageTracker.ResourceIsolationUsed();
        _licenseUsageTracker.DynamicProvidersUsed();
        _licenseUsageTracker.CibaUsed();
        _licenseUsageTracker.ServerSideSessionsUsed();
        _licenseUsageTracker.DPoPUsed();

        var summary = _licenseUsageTracker.GetSummary();

        summary.FeaturesUsed.Count.ShouldBe(7);
        summary.FeaturesUsed.ShouldContain("PLT-004"); // KeyManagement
        summary.FeaturesUsed.ShouldContain("PTC-004"); // PAR
        summary.FeaturesUsed.ShouldContain("PLT-021"); // ServerSideSessions
        summary.FeaturesUsed.ShouldContain("IS-001");  // ResourceIsolation
        summary.FeaturesUsed.ShouldContain("PLT-005"); // DynamicProviders
        summary.FeaturesUsed.ShouldContain("PTC-022"); // CIBA
        summary.FeaturesUsed.ShouldContain("PTC-006"); // DPoP
    }

    [Fact]
    public void used_clients_are_reported()
    {
        Init();

        _licenseUsageTracker.ClientUsed("mvc.code");
        _licenseUsageTracker.ClientUsed("mvc.dpop");

        var summary = _licenseUsageTracker.GetSummary();

        summary.ClientsUsed.Count.ShouldBe(2);
        summary.ClientsUsed.ShouldContain("mvc.code");
        summary.ClientsUsed.ShouldContain("mvc.dpop");
    }

    [Fact]
    public void used_issuers_are_reported()
    {
        Init();

        _licenseUsageTracker.IssuerUsed("https://localhost:5001");
        _licenseUsageTracker.IssuerUsed("https://acme.com");

        var summary = _licenseUsageTracker.GetSummary();

        summary.IssuersUsed.Count.ShouldBe(2);
        summary.IssuersUsed.ShouldContain("https://localhost:5001");
        summary.IssuersUsed.ShouldContain("https://acme.com");
    }

    [Fact]
    public void resource_indicator_used_tracks_feature()
    {
        Init();

        _licenseUsageTracker.ResourceIndicatorUsed("https://api.example.com");

        var summary = _licenseUsageTracker.GetSummary();

        summary.FeaturesUsed.ShouldContain("IS-001");
    }

    [Fact]
    public void null_resource_indicator_does_not_track()
    {
        Init();

        _licenseUsageTracker.ResourceIndicatorUsed(null);

        var summary = _licenseUsageTracker.GetSummary();

        summary.FeaturesUsed.ShouldBeEmpty();
    }
}
