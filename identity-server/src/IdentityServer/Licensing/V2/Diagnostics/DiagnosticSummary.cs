// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics;

internal class DiagnosticSummary(DiagnosticDataService diagnosticDataService, IdentityServerOptions options, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("Duende.IdentityServer.Diagnostics.Summary");

    public async Task PrintSummary(CT ct)
    {
        var jsonMemory = await diagnosticDataService.GetJsonBytesAsync(ct);
        var span = jsonMemory.Span;

        using var diagnosticActivity = Tracing.DiagnosticsActivitySource.StartActivity("DiagnosticSummary");
        var chunkSize = options.Diagnostics.ChunkSize;
        if (span.Length > chunkSize)
        {
            var totalChunks = (span.Length + options.Diagnostics.ChunkSize - 1) / chunkSize;
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
