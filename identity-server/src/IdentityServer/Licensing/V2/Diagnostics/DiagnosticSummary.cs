// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics;

internal class DiagnosticSummary(IEnumerable<IDiagnosticEntry> entries, ILogger<DiagnosticSummary> logger)
{
    public async Task PrintSummary()
    {
        var bufferWriter = new ArrayBufferWriter<byte>();
        await using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();

        foreach (var diagnosticEntry in entries)
        {
            await diagnosticEntry.WriteAsync(writer);
        }

        writer.WriteEndObject();

        await writer.FlushAsync();

        logger.LogInformation("{Message}", Encoding.UTF8.GetString(bufferWriter.WrittenSpan));
    }
}
