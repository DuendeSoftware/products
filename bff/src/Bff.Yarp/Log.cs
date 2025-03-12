// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.Bff.Yarp.Logging;

internal static class EventIds
{
    public static readonly EventId ProxyError = new(5, "ProxyError");
}

internal static class Log
{

    private static readonly Action<ILogger, string, string, Exception?> ProxyResponseErrorMessage = LoggerMessage.Define<string, string>(
        LogLevel.Information,
        EventIds.ProxyError,
        "Proxy response error. local path: '{localPath}', error: '{error}'");

    public static void ProxyResponseError(this ILogger logger, string localPath, string error)
    {
        ProxyResponseErrorMessage(logger, localPath, error, null);
    }
}
