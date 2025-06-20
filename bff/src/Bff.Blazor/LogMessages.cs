// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.Bff.Blazor;

internal static partial class LogMessages
{
    [LoggerMessage(
        Message = "Persisting authentication state")]
    public static partial void PersistingAuthenticationState(this ILogger logger, LogLevel logLevel);
}
