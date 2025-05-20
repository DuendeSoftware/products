// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class DataProtectionDiagnosticEntry(IOptions<DataProtectionOptions> dataProtectionOptions, IOptions<KeyManagementOptions> keyManagementOptions) : IDiagnosticEntry
{
    public Task WriteAsync(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("DataProtectionConfiguration");
        writer.WriteString("ApplicationDiscriminator", dataProtectionOptions?.Value?.ApplicationDiscriminator ?? "Not Configured");
        writer.WriteString("XmlEncryptor", keyManagementOptions?.Value.XmlEncryptor?.GetType().FullName ?? "Not Configured");
        writer.WriteString("XmlRepository", keyManagementOptions?.Value.XmlRepository?.GetType().FullName ?? "Not Configured");
        writer.WriteEndObject();

        return Task.CompletedTask;
    }
}
