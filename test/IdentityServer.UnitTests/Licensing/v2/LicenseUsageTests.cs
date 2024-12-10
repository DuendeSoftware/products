// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Licensing.v2;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IdentityServer.UnitTests.Licensing.v2;

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

        summary.FeaturesUsed.Should().Contain(LicenseFeature.KeyManagement.ToString());
        summary.FeaturesUsed.Should().Contain(LicenseFeature.PAR.ToString());
        summary.FeaturesUsed.Should().Contain(LicenseFeature.ServerSideSessions.ToString());
        summary.FeaturesUsed.Should().Contain(LicenseFeature.DCR.ToString());
        summary.FeaturesUsed.Should().Contain(LicenseFeature.KeyManagement.ToString());

        summary.FeaturesUsed.Should().Contain(LicenseFeature.ResourceIsolation.ToString());
        summary.FeaturesUsed.Should().Contain(LicenseFeature.DynamicProviders.ToString());
        summary.FeaturesUsed.Should().Contain(LicenseFeature.CIBA.ToString());
        summary.FeaturesUsed.Should().Contain(LicenseFeature.DPoP.ToString());

        summary.FeaturesUsed.Should().Contain(LicenseFeature.ISV.ToString());
        summary.FeaturesUsed.Should().Contain(LicenseFeature.Redistribution.ToString());
    }

    [Fact]
    public void used_clients_are_reported()
    {
        _licenseUsageTracker.ClientUsed("mvc.code");
        _licenseUsageTracker.ClientUsed("mvc.dpop");

        var summary = _licenseUsageTracker.GetSummary();

        summary.ClientsUsed.Count.Should().Be(2);
        summary.ClientsUsed.Should().Contain("mvc.code");
        summary.ClientsUsed.Should().Contain("mvc.dpop");
    }

    [Fact]
    public void used_issuers_are_reported()
    {
        _licenseUsageTracker.IssuerUsed("https://localhost:5001");
        _licenseUsageTracker.IssuerUsed("https://acme.com");

        var summary = _licenseUsageTracker.GetSummary();

        summary.IssuersUsed.Count.Should().Be(2);
        summary.IssuersUsed.Should().Contain("https://localhost:5001");
        summary.IssuersUsed.Should().Contain("https://acme.com");
    }
}