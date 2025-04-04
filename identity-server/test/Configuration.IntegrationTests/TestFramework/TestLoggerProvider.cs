// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.Extensions.Logging;

namespace IntegrationTests.TestFramework;

public class TestLoggerProvider : ILoggerProvider
{
    public class DebugLogger : ILogger, IDisposable
    {
        private readonly TestLoggerProvider _parent;
        private readonly string _category;

        public DebugLogger(TestLoggerProvider parent, string category)
        {
            _parent = parent;
            _category = category;
        }

        public void Dispose()
        {
        }

        public IDisposable BeginScope<TState>(TState state)
#if NET7_0_OR_GREATER
            where TState : notnull => this;
#endif

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = $"[{logLevel}] {_category} : {formatter(state, exception)}";
            _parent.Log(msg);
        }
    }

    public List<string> LogEntries = new List<string>();

    private void Log(string msg) => LogEntries.Add(msg);

    public ILogger CreateLogger(string categoryName) => new DebugLogger(this, categoryName);

    public void Dispose()
    {
    }
}
