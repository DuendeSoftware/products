// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace UnitTests.Common;

 public class MockLogger<T> : ILogger<T>
    {
        public readonly List<string> LogMessages = new();
        public readonly List<LogLevel> LogLevels = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            LogLevels.Add(logLevel);
            LogMessages.Add(formatter(state, exception));
        }
    }
