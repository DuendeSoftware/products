// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Licensing.V2;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace IdentityServer.UnitTests.Licensing.V2;

public class ProtocolRequestCounterTests
{
    private readonly ProtocolRequestCounter _counter;
    private readonly FakeLogger<ProtocolRequestCounter> _logger;

    public ProtocolRequestCounterTests()
    {
        var licenseAccessor = new LicenseAccessor(() => null, NullLogger<LicenseAccessor>.Instance);
        _logger = new FakeLogger<ProtocolRequestCounter>();
        _counter = new ProtocolRequestCounter(licenseAccessor, new StubLoggerFactory(_logger));
    }

    [Fact]
    public void number_of_protocol_requests_is_counted()
    {
        for (uint i = 0; i < 10; i++)
        {
            _counter.Increment();
            _counter.RequestCount.ShouldBe(i + 1);
        }
    }

    [Fact]
    public void warning_is_logged_once_after_too_many_protocol_requests_are_handled()
    {
        _counter.Threshold = 10;
        for (uint i = 0; i < _counter.Threshold * 10; i++)
        {
            _counter.Increment();
        }

        // REMINDER - If this test needs to change because the log message was updated, so should warning_is_not_logged_before_too_many_protocol_requests_are_handled
        var logRecord = _logger.Collector.GetSnapshot().Single();
        logRecord.Message.ShouldBe(
            $"You are using IdentityServer in trial mode and have exceeded the trial threshold of {_counter.Threshold} requests handled by IdentityServer. In a future version, you will need to restart the server or configure a license key to continue testing. See https://duende.link/l/trial for more information.");
    }

    [Fact]
    public void warning_is_not_logged_before_too_many_protocol_requests_are_handled()
    {
        _counter.Threshold = 10;
        for (uint i = 0; i < _counter.Threshold; i++)
        {
            _counter.Increment();
        }

        var logRecords = _logger.Collector.GetSnapshot().Select(l => l.Message);
        logRecords.ShouldNotContain(
            $"You are using IdentityServer in trial mode and have exceeded the trial threshold of {_counter.Threshold} requests handled by IdentityServer. In a future version, you will need to restart the server or configure a license key to continue testing. See https://duende.link/l/trial for more information.");
    }
}
