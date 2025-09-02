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
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            LoggerExtensions.LogTrace(logger, message);
#pragma warning restore CA2254
        }
    }

    public static void LogTrace<T0>(this ILogger logger, string message, T0 arg0)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            LoggerExtensions.LogTrace(logger, message, arg0);
#pragma warning restore CA2254
        }
    }

    public static void LogTrace<T0, T1>(this ILogger logger, string message, T0 arg0, T1 arg1)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            LoggerExtensions.LogTrace(logger, message, arg0, arg1);
#pragma warning restore CA2254
        }
    }

    public static void LogTrace<T0, T1, T2>(this ILogger logger, string message, T0 arg0, T1 arg1, T2 arg2)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            LoggerExtensions.LogTrace(logger, message, arg0, arg1, arg2);
#pragma warning restore CA2254
        }
    }

    public static void LogTrace<T0, T1, T2, T3>(this ILogger logger, string message, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            LoggerExtensions.LogTrace(logger, message, arg0, arg1, arg2, arg3);
#pragma warning restore CA2254
        }
    }

    public static void LogDebug(this ILogger logger, string message)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            LoggerExtensions.LogDebug(logger, message);
#pragma warning restore CA2254
        }
    }

    public static void LogDebug<T0>(this ILogger logger, string message, T0 arg0)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            LoggerExtensions.LogDebug(logger, message, arg0);
#pragma warning restore CA2254
        }
    }

    public static void LogDebug<T0, T1>(this ILogger logger, string message, T0 arg0, T1 arg1)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            LoggerExtensions.LogDebug(logger, message, arg0, arg1);
#pragma warning restore CA2254
        }
    }

    public static void LogDebug<T0, T1, T2>(this ILogger logger, string message, T0 arg0, T1 arg1, T2 arg2)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            LoggerExtensions.LogDebug(logger, message, arg0, arg1, arg2);
#pragma warning restore CA2254
        }
    }

    public static void LogDebug<T0, T1, T2, T3>(this ILogger logger, string message, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            LoggerExtensions.LogDebug(logger, message, arg0, arg1, arg2, arg3);
#pragma warning restore CA2254
        }
    }

    public static object SanitizeLogParameter(this object value)
    {
        if (value is not string s || string.IsNullOrEmpty(s))
        {
            return value;
        }

        var builder = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (!char.IsControl(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
