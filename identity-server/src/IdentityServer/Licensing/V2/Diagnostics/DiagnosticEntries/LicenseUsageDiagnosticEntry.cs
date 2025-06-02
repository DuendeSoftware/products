// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class LicenseUsageDiagnosticEntry(LicenseUsageTracker licenseUsageTracker) : IDiagnosticEntry
{
    public Task WriteAsync(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("LicenseUsageSummary");

        var licenseUsageSummary = licenseUsageTracker.GetSummary();

        writer.WriteNumber("ClientsUsedCount", licenseUsageSummary.ClientsUsed.Count);

        writer.WriteStartArray("IssuersUsed");
        foreach (var issuer in licenseUsageSummary.IssuersUsed)
        {
            writer.WriteStringValue(issuer);
        }
        writer.WriteEndArray();

        writer.WriteStartArray("FeaturesUsed");
        foreach (var feature in licenseUsageSummary.FeaturesUsed)
        {
            writer.WriteStringValue(feature);
        }
        writer.WriteEndArray();

        writer.WriteString("LicenseEdition", licenseUsageSummary.LicenseEdition);

        writer.WriteEndObject();

        return Task.CompletedTask;
    }
}
