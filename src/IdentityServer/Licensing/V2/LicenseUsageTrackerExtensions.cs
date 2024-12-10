// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace Duende.IdentityServer.Licensing.V2;

internal static class LicenseUsageTrackerExtensions
{
    internal static void UseResourceIndicator(this LicenseUsageTracker licenseUsage, string? resourceIndicator)
    {
        if (!string.IsNullOrWhiteSpace(resourceIndicator))
        {
            licenseUsage.FeatureUsed(LicenseFeature.ResourceIsolation);
        }
    }

    internal static void UseResourceIndicators(this LicenseUsageTracker licenseUsage, IEnumerable<string> resourceIndicators)
    {
        if (resourceIndicators.Any())
        {
            licenseUsage.FeatureUsed(LicenseFeature.ResourceIsolation);
        }
    }
}