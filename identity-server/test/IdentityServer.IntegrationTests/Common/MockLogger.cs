// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.IntegrationTests.Common;

public class MockLogger : ILogger
{
    public static MockLogger Create() => new MockLogger(new LoggerExternalScopeProvider());
    public MockLogger(LoggerExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    public readonly List<string> LogMessages = new();


    private readonly LoggerExternalScopeProvider _scopeProvider;


    public IDisposable BeginScope<TState>(TState state) where TState : notnull => _scopeProvider.Push(state);

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) => LogMessages.Add(formatter(state, exception));
}

public class MockLogger<T> : MockLogger, ILogger<T>
{
    public MockLogger(LoggerExternalScopeProvider scopeProvider) : base(scopeProvider)
    {
    }
}

public class MockLoggerProvider(MockLogger logger) : ILoggerProvider
{
    public void Dispose()
    {
    }

    public ILogger CreateLogger(string categoryName) => logger;
}
