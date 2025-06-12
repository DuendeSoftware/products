// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Events;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics;

internal static class DiagnosticLogParameters
{
    public const string Current = "current";
    public const string TotalChunks = "totalChunks";
    public const string DiagnosticData = "diagnosticData";
}

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Information,
        EventId = EventIds.DiagnosticSummaryLogged,
        EventName = "DiagnosticSummaryLogged",
        Message =
            $"Diagnostic data ({{{DiagnosticLogParameters.Current}}} of {{{DiagnosticLogParameters.TotalChunks}}}): {{{DiagnosticLogParameters.DiagnosticData}}}"
    )]
    public static partial void DiagnosticSummaryLogged(this ILogger logger, int current, int totalChunks, string diagnosticData);
}
