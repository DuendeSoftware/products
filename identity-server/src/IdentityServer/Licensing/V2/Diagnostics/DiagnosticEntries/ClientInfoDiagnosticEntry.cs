// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Text.Json;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class ClientInfoDiagnosticEntry(ClientLoadedTracker clientLoadedTracker) : IDiagnosticEntry
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = false
    };

    public Task WriteAsync(DiagnosticContext context, Utf8JsonWriter writer)
    {
        writer.WriteStartArray("Clients");
        foreach (var (_, client) in clientLoadedTracker.Clients)
        {
            JsonSerializer.Serialize(writer, client, _serializerOptions);
        }

        writer.WriteEndArray();

        return Task.CompletedTask;
    }
}
