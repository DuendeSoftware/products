// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Linq;
using Duende.IdentityServer.Licensing.v2;
using FluentAssertions;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace IdentityServer.UnitTests.Licensing.v2;

public class FeatureManagerTests
{

    public FeatureManagerTests()
    {
        _license = new TestLicenseAccessor();
        _logger = new FakeLogger<FeatureManager>();
        _featureManager = new FeatureManager(_license, _logger);
    }
    
    private FeatureManager _featureManager;
    private readonly TestLicenseAccessor _license;
    private readonly FakeLogger<FeatureManager> _logger;

    [Fact]
    public void used_features_are_reported()
    {
        _featureManager.UseFeature(LicenseFeature.PAR);
        _featureManager.UseFeature(LicenseFeature.DPoP);
        _featureManager.UseFeature(LicenseFeature.KeyManagement);
    
        var usedFeatures = _featureManager.UsedFeatures().ToArray();
        usedFeatures.Should().Contain(LicenseFeature.PAR);
        usedFeatures.Should().Contain(LicenseFeature.DPoP);
        usedFeatures.Should().Contain(LicenseFeature.KeyManagement);
        usedFeatures.Should().NotContain(LicenseFeature.DynamicProviders);
    }

    [Fact]
    public void unlicensed_features_log_warnings_exactly_once()
    {
        _featureManager.UseFeature(LicenseFeature.PAR);
        _featureManager.UseFeature(LicenseFeature.PAR);
       
        _logger.Collector.GetSnapshot().Should()
            .ContainSingle(r => r.Message == "Attempt to use feature PAR, but the license does not allow it");
    }

}