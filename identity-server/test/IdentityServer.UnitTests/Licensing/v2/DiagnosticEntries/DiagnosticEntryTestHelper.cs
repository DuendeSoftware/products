// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers;
using System.Text;
using System.Text.Json;
using Duende.IdentityServer.Licensing.V2.Diagnostics;

namespace IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

internal static class DiagnosticEntryTestHelper
{
    public static async Task<JsonDocument> WriteEntryToJson(IDiagnosticEntry subject, DateTime? serverStartTime = null, DateTime? currentServerTime = null)
    {
        var bufferWriter = new ArrayBufferWriter<byte>();

        await using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { Indented = false });
        writer.WriteStartObject();

        await subject.WriteAsync(new DiagnosticContext(serverStartTime ?? DateTime.UtcNow.AddMinutes(-5), currentServerTime ?? DateTime.UtcNow), writer);

        writer.WriteEndObject();
        await writer.FlushAsync();

        var json = Encoding.UTF8.GetString(bufferWriter.WrittenSpan);

        return JsonDocument.Parse(json);
    }
}
