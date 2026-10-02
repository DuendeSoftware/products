// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework.Extensions;

internal static partial class Log
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Concurrency exception removing records: {Exception}")]
    internal static partial void ConcurrencyExceptionRemovingRecordsValue(ILogger logger, object exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Too many concurrency exceptions. Exiting.")]
    internal static partial void TooManyConcurrencyExceptionsExiting(ILogger logger);

}
