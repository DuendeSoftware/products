using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Licensing.v2;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace IdentityServer.UnitTests.Licensing.v2;

public class ProtocolRequestCounterTests
{
    private readonly ProtocolRequestCounter _counter;
    private readonly FakeLogger<ProtocolRequestCounter> _logger;

    public ProtocolRequestCounterTests()
    {
        var licenseAccessor = new LicenseAccessor(new IdentityServerOptions(), NullLogger<LicenseAccessor>.Instance);
        _logger = new FakeLogger<ProtocolRequestCounter>();
        _counter = new ProtocolRequestCounter(licenseAccessor, new StubLoggerFactory(_logger));
    }

    [Fact]
    public void number_of_protocol_requests_is_counted()
    {
        for (uint i = 0; i < 10; i++)
        {
            _counter.Increment();
            _counter.RequestCount.Should().Be(i + 1);
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
        _logger.Collector.GetSnapshot().Should()
            .ContainSingle(r =>
                r.Message ==
                $"IdentityServer has handled {_counter.Threshold + 1} protocol requests without a license. In future versions, unlicensed IdentityServer instances will shut down after {_counter.Threshold} protocol requests. Please contact sales to obtain a license. If you are running in a test environment, please use a test license");
    }

    [Fact]
    public void warning_is_not_logged_before_too_many_protocol_requests_are_handled()
    {
        _counter.Threshold = 10;
        for (uint i = 0; i < _counter.Threshold; i++)
        {
            _counter.Increment();
        }

        _logger.Collector.GetSnapshot().Should()
            .NotContain(r =>
                r.Message ==
                $"IdentityServer has handled {_counter.Threshold + 1} protocol requests without a license. In future versions, unlicensed IdentityServer instances will shut down after {_counter.Threshold} protocol requests. Please contact sales to obtain a license. If you are running in a test environment, please use a test license");
    }
}
