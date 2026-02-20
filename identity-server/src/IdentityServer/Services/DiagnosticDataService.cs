// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Buffers;
using System.Text;
using System.Text.Json;
using Duende.IdentityServer.Licensing.V2.Diagnostics;

namespace Duende.IdentityServer.Services;

public class DiagnosticDataService
{
    private readonly DateTime _serverStartTime;
    private readonly IEnumerable<IDiagnosticEntry> _entries;

    internal DiagnosticDataService(DateTime serverStartTime, IEnumerable<IDiagnosticEntry> entries)
    {
        _serverStartTime = serverStartTime;
        _entries = entries;
    }

    public async Task<ReadOnlyMemory<byte>> GetJsonBytesAsync(CT ct = default)
    {
        var bufferWriter = new ArrayBufferWriter<byte>();
        await using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();

        var diagnosticContext = new DiagnosticContext(_serverStartTime, DateTime.UtcNow);
        foreach (var diagnosticEntry in _entries)
        {
            await diagnosticEntry.WriteAsync(diagnosticContext, writer);
        }

        writer.WriteEndObject();

        await writer.FlushAsync(ct);

        return bufferWriter.WrittenMemory;
    }

    public async Task<string> GetJsonStringAsync(CT ct = default)
    {
        var bytes = await GetJsonBytesAsync(ct);
        return Encoding.UTF8.GetString(bytes.Span);
    }
}
