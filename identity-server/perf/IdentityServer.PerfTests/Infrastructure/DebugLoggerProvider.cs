// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace IdentityServer.PerfTest.Infrastructure;

public class DebugLoggerProvider : List<string>, ILoggerProvider
{
    public class DebugLogger(DebugLoggerProvider parent, string category) : ILogger, IDisposable
    {
        public void Dispose() { }

        public IDisposable BeginScope<TState>(TState state) => this;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var msg = $"[{logLevel}] {category} : {formatter(state, exception)}";
            parent.Log(msg);
        }
    }

    private void Log(string msg) => Add(msg);

    public ILogger CreateLogger(string categoryName) => new DebugLogger(this, categoryName);

    public void Dispose()
    {
    }
}
