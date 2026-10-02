// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Globalization;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;

namespace IdentityServer.UnitTests.Logging;

public sealed class RefreshTokenLoggingTests
{
    [Fact]
    public void lifetime_logs_preserve_invariant_messages_and_numeric_state()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "~";
        CultureInfo.CurrentCulture = culture;

        try
        {
            var logger = new LifetimeLogger(true);

            logger.CurrentLifetimeCurrentLifetime(-1234);
            AssertEntry(logger, "Current lifetime: -1234", "CurrentLifetime", -1234);

            logger.NewLifetimeSlidingLifetime(2345);
            AssertEntry(logger, "New lifetime: 2345", "SlidingLifetime", 2345);

            logger.NewLifetimeExceedsAbsoluteLifetimeCappingItTo(2000);
            AssertEntry(logger, "New lifetime exceeds absolute lifetime, capping it to 2000", "NewLifetime", 2000);

            logger.Count.ShouldBe(3);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void disabled_lifetime_logs_do_not_allocate_or_write()
    {
        var logger = new LifetimeLogger(false);
        WriteLifetimeLogs(logger);

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            WriteLifetimeLogs(logger);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        allocated.ShouldBe(0);
        logger.Count.ShouldBe(0);
    }

    private static void WriteLifetimeLogs(ILogger logger)
    {
        logger.CurrentLifetimeCurrentLifetime(1234);
        logger.NewLifetimeSlidingLifetime(2345);
        logger.NewLifetimeExceedsAbsoluteLifetimeCappingItTo(2000);
    }

    private static void AssertEntry(LifetimeLogger logger, string message, string propertyName, int value)
    {
        logger.Level.ShouldBe(LogLevel.Debug);
        logger.Exception.ShouldBeNull();
        logger.Message.ShouldBe(message);
        logger.State.Single(x => x.Key == propertyName).Value.ShouldBeOfType<int>().ShouldBe(value);
    }

    private sealed class LifetimeLogger(bool enabled) : ILogger
    {
        public int Count { get; private set; }
        public LogLevel Level { get; private set; }
        public Exception? Exception { get; private set; }
        public string? Message { get; private set; }
        public IReadOnlyList<KeyValuePair<string, object?>> State { get; private set; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => enabled && logLevel == LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Count++;
            Level = logLevel;
            Exception = exception;
            Message = formatter(state, exception);
            State = (IReadOnlyList<KeyValuePair<string, object?>>)(object)state!;
        }
    }
}
