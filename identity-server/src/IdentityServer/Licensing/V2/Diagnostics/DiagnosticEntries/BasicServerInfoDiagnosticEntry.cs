// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class BasicServerInfoDiagnosticEntry(Func<string> hostNameResolver) : IDiagnosticEntry
{
    public Task WriteAsync(DiagnosticContext context, Utf8JsonWriter writer)
    {
        writer.WriteStartObject("BasicServerInfo");

        writer.WriteString("HostName", hostNameResolver());
        writer.WriteString("ServerStartTime", context.ServerStartTime.ToString("o"));
        writer.WriteString("CurrentServerTime", context.CurrentSeverTime.ToString("o"));

        writer.WriteEndObject();

        return Task.CompletedTask;
    }
}
