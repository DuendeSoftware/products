// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers;
using System.Text.Json;

namespace Duende.Bff.Diagnostics;

internal class DiagnosticDataService(DateTime serverStartTime, IEnumerable<IDiagnosticEntry> entries)
{
    public async Task<ReadOnlyMemory<byte>> GetJsonBytesAsync(CT ct = default)
    {
        var bufferWriter = new ArrayBufferWriter<byte>();
        await using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();

        var diagnosticContext = new DiagnosticContext(serverStartTime, DateTime.UtcNow);
        foreach (var diagnosticEntry in entries)
        {
            diagnosticEntry.Write(diagnosticContext, writer);
        }

        writer.WriteEndObject();

        await writer.FlushAsync(ct);

        return bufferWriter.WrittenMemory;
    }
}
