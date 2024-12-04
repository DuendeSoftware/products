// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Licensing.v2;
using System.Collections.Generic;

namespace UnitTests.Common;

internal class TestFeatureManager : ILicenseUsageService
{
    public HashSet<LicenseFeature> UsedFeatures { get; } = new();

    public void UseFeature(LicenseFeature feature) => UsedFeatures.Add(feature);

    public HashSet<string> UsedClients { get; } = new();
    public void UseClient(string clientId) => UsedClients.Add(clientId);

    public HashSet<string> UsedIssuers { get; } = new();
    public void UseIssuer(string issuer) => UsedIssuers.Add(issuer);
}