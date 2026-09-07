// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer;

internal static class ILoggerDevExtensions
{
    extension(ILogger logger)
    {
        public void LogTrace(string message)
        {
            if (logger.IsEnabled(LogLevel.Trace))
            {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
                LoggerExtensions.LogTrace(logger, message);
#pragma warning restore CA2254
            }
        }

        public void LogTrace<T0>(string message, T0 arg0)
        {
            if (logger.IsEnabled(LogLevel.Trace))
            {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
                LoggerExtensions.LogTrace(logger, message, arg0);
#pragma warning restore CA2254
            }
        }

        public void LogTrace<T0, T1>(string message, T0 arg0, T1 arg1)
        {
            if (logger.IsEnabled(LogLevel.Trace))
            {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
                LoggerExtensions.LogTrace(logger, message, arg0, arg1);
#pragma warning restore CA2254
            }
        }

        public void LogTrace<T0, T1, T2>(string message, T0 arg0, T1 arg1, T2 arg2)
        {
            if (logger.IsEnabled(LogLevel.Trace))
            {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
                LoggerExtensions.LogTrace(logger, message, arg0, arg1, arg2);
#pragma warning restore CA2254
            }
        }

        public void LogTrace<T0, T1, T2, T3>(string message, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        {
            if (logger.IsEnabled(LogLevel.Trace))
            {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
                LoggerExtensions.LogTrace(logger, message, arg0, arg1, arg2, arg3);
#pragma warning restore CA2254
            }
        }

        public void LogDebug(string message)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
                LoggerExtensions.LogDebug(logger, message);
#pragma warning restore CA2254
            }
        }

        public void LogDebug<T0>(string message, T0 arg0)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
                LoggerExtensions.LogDebug(logger, message, arg0);
#pragma warning restore CA2254
            }
        }

        public void LogDebug<T0, T1>(string message, T0 arg0, T1 arg1)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
                LoggerExtensions.LogDebug(logger, message, arg0, arg1);
#pragma warning restore CA2254
            }
        }

        public void LogDebug<T0, T1, T2>(string message, T0 arg0, T1 arg1, T2 arg2)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
                LoggerExtensions.LogDebug(logger, message, arg0, arg1, arg2);
#pragma warning restore CA2254
            }
        }

        public void LogDebug<T0, T1, T2, T3>(string message, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
                LoggerExtensions.LogDebug(logger, message, arg0, arg1, arg2, arg3);
#pragma warning restore CA2254
            }
        }
    }

    extension(object value)
    {
        public object SanitizeLogParameter()
        {
            if (value is not string s || string.IsNullOrEmpty(s))
            {
                return value;
            }

            s = s.ReplaceLineEndings(string.Empty);
            var builder = new System.Text.StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (!IsUnsafeLogChar(c))
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();

            static bool IsUnsafeLogChar(char c)
            {
                return char.IsControl(c)
                       || c == '\u0085'  // NEXT LINE
                       || c == '\u2028'  // LINE SEPARATOR
                       || c == '\u2029'; // PARAGRAPH SEPARATOR
            }
        }
    }
}
