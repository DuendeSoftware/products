// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Licensing.V2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace IdentityServer.UnitTests.Licensing.V2;

public class LicenseLoggingAdapterTests
{
    [Theory]
    [InlineData(true, LogLevel.Trace)]
    [InlineData(false, LogLevel.Error)]
    public void LogError_logs_to_correct_level(
        bool isRedistribution,
        LogLevel expectedLogLevel)
    {
        var logMessage = "Test Message";
        var logger = new FakeLogger();
        var adapter = new LicenseLoggingAdapter(isRedistribution, logger);

        adapter.LogError(logMessage);

        logger.Collector.GetSnapshot()
            .SingleOrDefault(log => log.Level == expectedLogLevel && log.Message == logMessage).ShouldNotBeNull();
    }
}
