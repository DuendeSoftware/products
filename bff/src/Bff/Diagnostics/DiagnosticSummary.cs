// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using Duende.Bff.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Diagnostics;

internal class DiagnosticSummary(
    DiagnosticDataService diagnosticDataService,
    IOptions<BffOptions> options,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("Duende.BFF.Diagnostics.Summary");

    public async Task PrintSummaryAsync(CT ct = default)
    {
        var bffOptions = options.Value;
        var jsonMemory = await diagnosticDataService.GetJsonBytesAsync(ct);
        var span = jsonMemory.Span;

        var chunkSize = bffOptions.Diagnostics.ChunkSize;
        if (span.Length > chunkSize)
        {
            var totalChunks = (span.Length + bffOptions.Diagnostics.ChunkSize - 1) / chunkSize;
            for (var i = 0; i < totalChunks; i++)
            {
                var offset = i * chunkSize;
                var length = Math.Min(chunkSize, span.Length - offset);
                var chunk = span.Slice(offset, length);
                _logger.DiagnosticSummaryLogged(i + 1, totalChunks, Encoding.UTF8.GetString(chunk));
            }
        }
        else
        {
            _logger.DiagnosticSummaryLogged(1, 1, Encoding.UTF8.GetString(span));
        }
    }
}
