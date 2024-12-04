// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Linq;
using Duende.IdentityServer.Licensing.v2;
using FluentAssertions;
using Xunit;

namespace IdentityServer.UnitTests.Licensing.v2;

public class LicenseUsageTests
{
    public LicenseUsageTests()
    {
        _featureManager = new LicenseUsageService();
    }
    
    private LicenseUsageService _featureManager;

    [Fact]
    public void used_features_are_reported()
    {
        _featureManager.UseFeature(LicenseFeature.PAR);
        _featureManager.UseFeature(LicenseFeature.DPoP);
        _featureManager.UseFeature(LicenseFeature.KeyManagement);
    
        _featureManager.UsedFeatures.Should().Contain(LicenseFeature.PAR);
        _featureManager.UsedFeatures.Should().Contain(LicenseFeature.DPoP);
        _featureManager.UsedFeatures.Should().Contain(LicenseFeature.KeyManagement);
        _featureManager.UsedFeatures.Should().NotContain(LicenseFeature.DynamicProviders);
    }

    [Fact]
    public void used_clients_are_reported()
    {
        _featureManager.UseClient("mvc.code");
        _featureManager.UseClient("mvc.dpop");
    
        _featureManager.UsedClients.Should().Contain("mvc.code");
        _featureManager.UsedClients.Should().Contain("mvc.dpop");
        _featureManager.UsedIssuers.Should().NotContain("https://bogus.com");
    }

    [Fact]
    public void used_issuers_are_reported()
    {
        _featureManager.UseIssuer("https://localhost:5001");
        _featureManager.UseIssuer("https://acme.com");
    
        _featureManager.UsedIssuers.Should().Contain("https://localhost:5001");
        _featureManager.UsedIssuers.Should().Contain("https://acme.com");
        _featureManager.UsedIssuers.Should().NotContain("https://bogus.com");
    }
}