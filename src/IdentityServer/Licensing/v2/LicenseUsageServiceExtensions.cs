// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace Duende.IdentityServer.Licensing.v2;

internal static class LicenseUsageServiceExtensions
{
    internal static void UseResourceIndicator(this ILicenseUsageService licenseUsage, string? resourceIndicator)
    {
        if (!string.IsNullOrWhiteSpace(resourceIndicator))
        {
            licenseUsage.UseFeature(LicenseFeature.ResourceIsolation);
        }
    }

    internal static void UseResourceIndicators(this ILicenseUsageService licenseUsage, IEnumerable<string> resourceIndicators)
    {
        if (resourceIndicators.Any())
        {
            licenseUsage.UseFeature(LicenseFeature.ResourceIsolation);
        }
    }
}