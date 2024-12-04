// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Duende.IdentityServer.Licensing.v2;

internal class LicenseSummary(ILicenseAccessor license, ILicenseUsageService usage) : ILicenseSummary
{
    public string Summary
    {
        get
        {
            var sb = new StringBuilder();
            sb.AppendLine("IdentityServer Usage Summary:");
            sb.AppendLine($"\tLicense: {LicenseEdition}");

            AppendSummary(sb, "Client Id", usage.UsedClients);
            AppendSummary(sb, "Business Edition Feature", usage.BusinessFeaturesUsed);
            AppendSummary(sb, "Enterprise Edition Feature", usage.EnterpriseFeaturesUsed);
            AppendSummary(sb, "Other Feature", usage.OtherFeaturesUsed);
            AppendSummary(sb, "Issuer", usage.UsedIssuers);

            return sb.ToString();
        }
    }

    private void AppendSummary<T>(StringBuilder sb, string label, IReadOnlyCollection<T> items)
    {
        if (items.Count == 1)
        {
            sb.AppendLine($"\t{items.Count} {label} Used: {items.Single()}");
        }
        else if (items.Count > 1)
        {
            sb.AppendLine($"\t{items.Count} {label}s Used: {string.Join(", ", items)}");
        }
    }

    public string LicenseEdition => license.Current.Edition?.ToString() ?? "None";
    public IEnumerable<string> UsedClients => usage.UsedClients;
    public IEnumerable<string> UsedIssuers => usage.UsedIssuers;

    public IEnumerable<string> EnterpriseFeaturesUsed => usage.EnterpriseFeaturesUsed.Select(f => f.ToString());
    public IEnumerable<string> BusinessFeaturesUsed => usage.BusinessFeaturesUsed.Select(f => f.ToString());
    public IEnumerable<string> OtherFeaturesUsed => usage.OtherFeaturesUsed.Select(f => f.ToString());
}
