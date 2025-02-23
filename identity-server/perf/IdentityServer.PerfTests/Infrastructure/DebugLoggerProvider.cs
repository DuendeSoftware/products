// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace IdentityServer.PerfTest.Infrastructure
{
    public class DebugLoggerProvider : List<string>, ILoggerProvider
    {
        public class DebugLogger : ILogger, IDisposable
        {
            private readonly DebugLoggerProvider _parent;
            private readonly string _category;

            public DebugLogger(DebugLoggerProvider parent, string category)
            {
                _parent = parent;
                _category = category;
            }

            public void Dispose()
            {
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return this;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                var msg = $"[{logLevel}] {_category} : {formatter(state, exception)}";
                _parent.Log(msg);
            }
        }

        private void Log(string msg)
        {
            Add(msg);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new DebugLogger(this, categoryName);
        }

        public void Dispose()
        {
        }
    }
}

