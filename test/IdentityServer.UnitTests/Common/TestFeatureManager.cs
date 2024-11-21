// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Licensing.v2;
using System.Collections.Generic;

namespace UnitTests.Common;

internal class TestFeatureManager : IFeatureManager
{
    public List<LicenseFeature> Features { get; set; }

    public IEnumerable<LicenseFeature> UsedFeatures()
    {
        return Features;
    }

    public void UseFeature(LicenseFeature feature)
    {
        Features.Add(feature);
    }
}