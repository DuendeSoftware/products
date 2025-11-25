// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;

namespace Duende.Bff.Diagnostics.DiagnosticEntries;

internal class BasicServerInfoDiagnosticEntry(TimeProvider timeProvider)
    : IDiagnosticEntry
{
    public void Write(DiagnosticContext context, Utf8JsonWriter writer)
    {
        writer.WriteStartObject("BasicServerInfo");

        writer.WriteString("HostName", Dns.GetHostName());
        writer.WriteString("ServerStartTime", context.ServerStartTime.ToString("o"));
        writer.WriteString("CurrentServerTime", timeProvider.GetUtcNow().UtcDateTime.ToString("o"));

        writer.WriteEndObject();
    }
}
