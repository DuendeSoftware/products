using System;
using System.Collections.Generic;
using Duende.IdentityServer.Licensing.v2;
using IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests.Extensibility;

public class LicensingCustomizationTests
{
    [Fact]
    public void customization_of_ILicenseAccessor_is_not_allowed()
    {
        var mockPipeline = new IdentityServerPipeline();
        
        mockPipeline.OnPostConfigureServices += svcs =>
        {
            svcs.AddTransient<ILicenseAccessor, CustomLicenseAccessor>();
        };

        Assert.Throws<InvalidOperationException>(() => mockPipeline.Initialize());
    }

    [Fact]
    public void customization_of_IProtocolRequestCounter_is_not_allowed()
    {
        var mockPipeline = new IdentityServerPipeline();
        
        mockPipeline.OnPostConfigureServices += svcs =>
        {
            svcs.AddTransient<IProtocolRequestCounter, CustomProtocolRequestCounter>();
        };

        Assert.Throws<InvalidOperationException>(() => mockPipeline.Initialize());
    }

    [Fact]
    public void customization_of_IFeatureManager_is_not_allowed()
    {
        var mockPipeline = new IdentityServerPipeline();
        
        mockPipeline.OnPostConfigureServices += svcs =>
        {
            svcs.AddTransient<ILicenseUsageService, CustomFeatureManager>();
        };

        Assert.Throws<InvalidOperationException>(() => mockPipeline.Initialize());
    }
}


internal class CustomLicenseAccessor : ILicenseAccessor
{
    public License Current { get; }
}

internal class CustomProtocolRequestCounter : IProtocolRequestCounter
{
    public uint RequestCount { get; }

    public void Increment()
    {
    }
}

internal class CustomFeatureManager : ILicenseUsageService
{
    public HashSet<LicenseFeature> BusinessFeaturesUsed { get; } = [];
    public HashSet<LicenseFeature> EnterpriseFeaturesUsed { get; } = [];
    public HashSet<LicenseFeature> OtherFeaturesUsed { get; } = [];
    public void UseFeature(LicenseFeature feature)
    {
    }

    public HashSet<string> UsedClients { get; } = [];
    public void UseClient(string clientId)
    {
    }

    public HashSet<string> UsedIssuers { get; } = [];
    public void UseIssuer(string issuer)
    {
    }
}