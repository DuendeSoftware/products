// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.AspNetCore.TestFramework;

public class MockLogger : ILogger
{
    public static MockLogger Create() => new MockLogger(new LoggerExternalScopeProvider());
    public MockLogger(LoggerExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    public readonly List<string> LogMessages = new();


    private readonly LoggerExternalScopeProvider _scopeProvider;


    public IDisposable BeginScope<TState>(TState state) where TState : notnull => _scopeProvider.Push(state);

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => LogMessages.Add(formatter(state, exception));

    /// <summary>
    /// Creates a strongly-typed ILogger&lt;T&gt; wrapper that shares the same log messages collection.
    /// </summary>
    public ILogger<T> For<T>() => new MockLogger<T>(this);
}

public class MockLogger<T> : ILogger<T>
{
    private readonly MockLogger _inner;

    internal MockLogger(MockLogger inner) => _inner = inner;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _inner.Log(logLevel, eventId, state, exception, formatter);
}

public class MockLoggerProvider(MockLogger logger) : ILoggerProvider
{
    public void Dispose()
    {
    }

    public ILogger CreateLogger(string categoryName) => logger;
}
