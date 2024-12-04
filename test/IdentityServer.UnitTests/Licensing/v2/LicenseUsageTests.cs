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
        _featureManager.UseFeature(LicenseFeature.KeyManagement);
        _featureManager.UseFeature(LicenseFeature.PAR);
        _featureManager.UseFeature(LicenseFeature.ResourceIsolation);
        _featureManager.UseFeature(LicenseFeature.DynamicProviders);
        _featureManager.UseFeature(LicenseFeature.CIBA);
        _featureManager.UseFeature(LicenseFeature.ServerSideSessions);
        _featureManager.UseFeature(LicenseFeature.DPoP);
        _featureManager.UseFeature(LicenseFeature.DCR);
        _featureManager.UseFeature(LicenseFeature.ISV);
        _featureManager.UseFeature(LicenseFeature.Redistribution);
    
        _featureManager.BusinessFeaturesUsed.Should().Contain(LicenseFeature.KeyManagement);
        _featureManager.BusinessFeaturesUsed.Should().Contain(LicenseFeature.PAR);
        _featureManager.BusinessFeaturesUsed.Should().Contain(LicenseFeature.ServerSideSessions);
        _featureManager.BusinessFeaturesUsed.Should().Contain(LicenseFeature.DCR);
        _featureManager.BusinessFeaturesUsed.Should().HaveCount(4);

        _featureManager.EnterpriseFeaturesUsed.Should().Contain(LicenseFeature.ResourceIsolation);
        _featureManager.EnterpriseFeaturesUsed.Should().Contain(LicenseFeature.DynamicProviders);
        _featureManager.EnterpriseFeaturesUsed.Should().Contain(LicenseFeature.CIBA);
        _featureManager.EnterpriseFeaturesUsed.Should().Contain(LicenseFeature.DPoP);
        _featureManager.EnterpriseFeaturesUsed.Should().HaveCount(4);

        _featureManager.OtherFeaturesUsed.Should().Contain(LicenseFeature.ISV);
        _featureManager.OtherFeaturesUsed.Should().Contain(LicenseFeature.Redistribution);
        _featureManager.OtherFeaturesUsed.Should().HaveCount(2);
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