// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(AnErrorOccurredWhileLoggingTheDiagnosticSummary),
        Message = "An error occurred while logging the diagnostic summary: {Message}")]
    internal static partial void AnErrorOccurredWhileLoggingTheDiagnosticSummary(this ILogger logger, System.Exception exception, object message);
}
