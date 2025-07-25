// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class AuthSchemeInfoDiagnosticEntry(IAuthenticationSchemeProvider authenticationSchemeProvider) : IDiagnosticEntry
{
    public async Task WriteAsync(DiagnosticContext context, Utf8JsonWriter writer)
    {
        var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();

        writer.WriteStartObject("AuthSchemeInfo");
        writer.WriteStartArray("Schemes");
        foreach (var scheme in schemes)
        {
            writer.WriteStartObject();
            writer.WriteString(scheme.Name, scheme.HandlerType.FullName ?? "Unknown");
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
