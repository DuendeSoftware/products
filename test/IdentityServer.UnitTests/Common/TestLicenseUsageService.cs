// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Licensing.v2;
using System.Collections.Generic;

namespace UnitTests.Common;

internal class TestLicenseUsageService : ILicenseUsageService
{
    public HashSet<LicenseFeature> BusinessFeaturesUsed { get; }
    public HashSet<LicenseFeature> EnterpriseFeaturesUsed { get; }
    public HashSet<LicenseFeature> OtherFeaturesUsed { get; }

    public void UseFeature(LicenseFeature feature) { }

    public HashSet<string> UsedClients { get; } = new();
    public void UseClient(string clientId) { }

    public HashSet<string> UsedIssuers { get; } = new();
    public void UseIssuer(string issuer) { }
}