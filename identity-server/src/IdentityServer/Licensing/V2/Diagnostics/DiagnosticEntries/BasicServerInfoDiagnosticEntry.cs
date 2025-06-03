// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class BasicServerInfoDiagnosticEntry(Func<string> hostNameResolver) : IDiagnosticEntry
{
    public Task WriteAsync(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("BasicServerInfo");

        writer.WriteString("HostName", hostNameResolver());

        writer.WriteEndObject();

        return Task.CompletedTask;
    }
}
