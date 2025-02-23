// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Licensing.V2;
using Shouldly;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IdentityServer.UnitTests.Licensing.V2;

public class LicenseUsageTests
{
    private readonly LicenseUsageTracker _licenseUsageTracker;

    public LicenseUsageTests()
    {
        var options = new IdentityServerOptions();
        var licenseAccessor = new LicenseAccessor(options, NullLogger<LicenseAccessor>.Instance);
        _licenseUsageTracker = new LicenseUsageTracker(licenseAccessor);
    }

    [Fact]
    public void used_features_are_reported()
    {
        _licenseUsageTracker.FeatureUsed(LicenseFeature.KeyManagement);
        _licenseUsageTracker.FeatureUsed(LicenseFeature.PAR);
        _licenseUsageTracker.FeatureUsed(LicenseFeature.ResourceIsolation);
        _licenseUsageTracker.FeatureUsed(LicenseFeature.DynamicProviders);
        _licenseUsageTracker.FeatureUsed(LicenseFeature.CIBA);
        _licenseUsageTracker.FeatureUsed(LicenseFeature.ServerSideSessions);
        _licenseUsageTracker.FeatureUsed(LicenseFeature.DPoP);
        _licenseUsageTracker.FeatureUsed(LicenseFeature.DCR);
        _licenseUsageTracker.FeatureUsed(LicenseFeature.ISV);
        _licenseUsageTracker.FeatureUsed(LicenseFeature.Redistribution);

        var summary = _licenseUsageTracker.GetSummary();

        summary.FeaturesUsed.ShouldContain(LicenseFeature.KeyManagement.ToString());
        summary.FeaturesUsed.ShouldContain(LicenseFeature.PAR.ToString());
        summary.FeaturesUsed.ShouldContain(LicenseFeature.ServerSideSessions.ToString());
        summary.FeaturesUsed.ShouldContain(LicenseFeature.DCR.ToString());
        summary.FeaturesUsed.ShouldContain(LicenseFeature.KeyManagement.ToString());

        summary.FeaturesUsed.ShouldContain(LicenseFeature.ResourceIsolation.ToString());
        summary.FeaturesUsed.ShouldContain(LicenseFeature.DynamicProviders.ToString());
        summary.FeaturesUsed.ShouldContain(LicenseFeature.CIBA.ToString());
        summary.FeaturesUsed.ShouldContain(LicenseFeature.DPoP.ToString());

        summary.FeaturesUsed.ShouldContain(LicenseFeature.ISV.ToString());
        summary.FeaturesUsed.ShouldContain(LicenseFeature.Redistribution.ToString());
    }

    [Fact]
    public void used_clients_are_reported()
    {
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
        _licenseUsageTracker.IssuerUsed("https://localhost:5001");
        _licenseUsageTracker.IssuerUsed("https://acme.com");

        var summary = _licenseUsageTracker.GetSummary();

        summary.IssuersUsed.Count.ShouldBe(2);
        summary.IssuersUsed.ShouldContain("https://localhost:5001");
        summary.IssuersUsed.ShouldContain("https://acme.com");
    }
}