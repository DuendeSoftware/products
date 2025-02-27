// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.Tests.TestFramework;

public class TestLoggerProvider(WriteTestOutput writeOutput, string name) : ILoggerProvider
{
    private readonly WriteTestOutput _writeOutput = writeOutput ?? throw new ArgumentNullException(nameof(writeOutput));
    private readonly string _name = name ?? throw new ArgumentNullException(nameof(name));
    private Stopwatch _watch = Stopwatch.StartNew();

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

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return this;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = $"[{logLevel}] {_category} : {formatter(state, exception)}";
            _parent.Log(msg);
        }
    }

    public List<string> LogEntries { get; } = new();

    private void Log(string msg)
    {
        try
        {
            var message = _watch.Elapsed.TotalMilliseconds.ToString("0000") + "ms - " + _name + msg;
#if NCRUNCH
            Console.WriteLine(message);
#else
            _writeOutput?.Invoke(message);
#endif
        }
        catch (Exception)
        {
            Console.WriteLine("Logging Failed: " + msg);
        }
        LogEntries.Add(msg);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new DebugLogger(this, categoryName);
    }

    public void Dispose()
    {
    }
}

public delegate void WriteTestOutput(string message);
