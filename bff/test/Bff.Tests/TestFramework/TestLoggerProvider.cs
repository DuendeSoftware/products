// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;

namespace Duende.Bff.Tests.TestFramework;


#pragma warning disable CS9113 // Parameter is unread but needed for ncrunch.
public class TestLoggerProvider(WriteTestOutput writeOutput, string name, bool forceToWriteOutput = false) : ILoggerProvider
#pragma warning restore CS9113 // Parameter is unread but needed for ncrunch.
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

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => this;

        public bool IsEnabled(LogLevel logLevel) => true;

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
            if (forceToWriteOutput)
            {
                _writeOutput?.Invoke(message);
            }
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

    public ILogger CreateLogger(string categoryName) => new DebugLogger(this, categoryName);

    public void Dispose()
    {
    }
}

public delegate void WriteTestOutput(string message);
