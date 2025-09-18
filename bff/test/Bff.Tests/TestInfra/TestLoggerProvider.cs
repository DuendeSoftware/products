// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Tests.TestInfra;

public class TestLoggerProvider(WriteTestOutput writeOutput, string name) : ILoggerProvider
{
    private readonly WriteTestOutput _writeOutput = writeOutput ?? throw new ArgumentNullException(nameof(writeOutput));
    private readonly string _name = name ?? throw new ArgumentNullException(nameof(name));

    private class DebugLogger : ILogger, IDisposable
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

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => this;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = $"[{logLevel}] {_category} : {formatter(state, exception)} " + exception?.ToString();
            _parent.Log(msg);
        }
    }

    public List<string> LogEntries { get; } = new();

    private void Log(string msg)
    {
        try
        {
            _writeOutput?.Invoke(_name + msg);
        }
        catch (Exception)
        {
            Console.WriteLine("Logging Failed: " + msg);
        }
        LogEntries.Add(msg);
    }

    public ILogger CreateLogger(string categoryName) => new DebugLogger(this, categoryName);

    public void Dispose()
    {
    }
}
