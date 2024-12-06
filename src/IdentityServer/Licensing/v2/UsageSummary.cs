// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace Duende.IdentityServer.Licensing.v2;

internal class UsageSummary(ILicenseAccessor license, ILicenseUsageService usage) : IUsageSummary
{
    public string LicenseEdition => license.Current.Edition?.ToString() ?? "None";
    public IEnumerable<string> UsedClients => usage.UsedClients;
    public IEnumerable<string> UsedIssuers => usage.UsedIssuers;

    public IEnumerable<string> FeaturesUsed => 
        usage.EnterpriseFeaturesUsed
            .Concat(usage.BusinessFeaturesUsed)
            .Concat(usage.OtherFeaturesUsed)
            .Select(f => f.ToString());
}
