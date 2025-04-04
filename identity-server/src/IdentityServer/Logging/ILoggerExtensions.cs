// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer;

internal static class ILoggerDevExtensions
{
    public static void LogTrace(this ILogger logger, string message)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            LoggerExtensions.LogTrace(logger, message);
        }
    }

    public static void LogTrace<T0>(this ILogger logger, string message, T0 arg0)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            LoggerExtensions.LogTrace(logger, message, arg0);
        }
    }

    public static void LogTrace<T0, T1>(this ILogger logger, string message, T0 arg0, T1 arg1)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            LoggerExtensions.LogTrace(logger, message, arg0, arg1);
        }
    }

    public static void LogTrace<T0, T1, T2>(this ILogger logger, string message, T0 arg0, T1 arg1, T2 arg2)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            LoggerExtensions.LogTrace(logger, message, arg0, arg1, arg2);
        }
    }

    public static void LogTrace<T0, T1, T2, T3>(this ILogger logger, string message, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            LoggerExtensions.LogTrace(logger, message, arg0, arg1, arg2, arg3);
        }
    }

    public static void LogDebug(this ILogger logger, string message)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            LoggerExtensions.LogDebug(logger, message);
        }
    }

    public static void LogDebug<T0>(this ILogger logger, string message, T0 arg0)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            LoggerExtensions.LogDebug(logger, message, arg0);
        }
    }

    public static void LogDebug<T0, T1>(this ILogger logger, string message, T0 arg0, T1 arg1)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            LoggerExtensions.LogDebug(logger, message, arg0, arg1);
        }
    }

    public static void LogDebug<T0, T1, T2>(this ILogger logger, string message, T0 arg0, T1 arg1, T2 arg2)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            LoggerExtensions.LogDebug(logger, message, arg0, arg1, arg2);
        }
    }

    public static void LogDebug<T0, T1, T2, T3>(this ILogger logger, string message, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            LoggerExtensions.LogDebug(logger, message, arg0, arg1, arg2, arg3);
        }
    }

    public static object SanitizeLogParameter(this object value) => value?.GetType() == typeof(string) ? value.ToString()?.ReplaceLineEndings(string.Empty) : value;
}
