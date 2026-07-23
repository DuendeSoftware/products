// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Interaction.Infrastructure;

/// <summary>
/// An <see cref="ILoggerProvider"/> that forwards all log messages from an inline WebApplication
/// to the Aspire dashboard's Console Logs tab via <see cref="ResourceLoggerService"/>.
/// </summary>
internal sealed class ResourceLoggerProvider(ILogger resourceLogger) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new ResourceLoggerBridge(resourceLogger, categoryName);

    public void Dispose() { }

    private sealed class ResourceLoggerBridge(ILogger resourceLogger, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => resourceLogger.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            resourceLogger.Log(logLevel, eventId, $"[{categoryName}] {message}", exception, (s, _) => s);
        }
    }
}
