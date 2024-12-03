using System;
using Duende.IdentityServer.Licensing.v2;
using FluentAssertions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace IdentityServer.UnitTests.Licensing.v2;

public class ProtocolRequestCounterTests
{
    private readonly ProtocolRequestCounter _counter;
    private readonly TestLicenseAccessor _license;
    private readonly FakeLogger<ProtocolRequestCounter> _logger;
    private readonly FakeTimeProvider _timeProvider;

    // There is no particular significance to 2024-01-01, this is just an
    // arbitrary date that we will use as a base for expiration tests
    private readonly DateTimeOffset _januaryFirst = new DateTimeOffset(
        new DateOnly(2024, 1, 1),
        new TimeOnly(12, 00),
        TimeSpan.Zero); 
    
    public ProtocolRequestCounterTests()
    {
        _license = new TestLicenseAccessor();
        _timeProvider = new FakeTimeProvider();
        _logger = new FakeLogger<ProtocolRequestCounter>();
        _counter = new ProtocolRequestCounter(_license, _timeProvider, _logger);
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

    [Fact]
    public void warning_is_logged_for_expired_license()
    {
        _timeProvider.SetUtcNow(_januaryFirst);
        _license.Current = LicenseFactory.Create(LicenseEdition.Enterprise, _januaryFirst.Subtract(TimeSpan.FromDays(1)));

        _counter.Increment();
        var expiration = _license.Current.Expiration?.ToString("yyyy-MM-dd");
        // REMINDER - If this test needs to change because the log message was updated, so should no_warning_is_logged_for_unexpired_license
        _logger.Collector.GetSnapshot().Should()
            .ContainSingle(r =>
                r.Message == $"Your license expired on {expiration}. You are required to obtain a new license. In a future version of IdentityServer, expired licenses will stop the server after 90 days.");
    }

    [Fact]
    public void no_warning_is_logged_for_unexpired_license()
    {
        _timeProvider.SetUtcNow(_januaryFirst);
        _license.Current = LicenseFactory.Create(LicenseEdition.Enterprise, _januaryFirst);

        _counter.Increment();
        var expiration = _license.Current.Expiration?.ToString("yyyy-MM-dd");
        _logger.Collector.GetSnapshot().Should()
            .NotContain(r =>
                r.Message == $"Your license expired on {expiration}. You are required to obtain a new license. In a future version of IdentityServer, expired licenses will stop the server after 90 days.");
    }
    
    [Fact]
    public void no_expired_license_warning_for_redistribution_license()
    {
        _timeProvider.SetUtcNow(_januaryFirst);
        _license.Current = LicenseFactory.Create(LicenseEdition.Enterprise, _januaryFirst.Subtract(TimeSpan.FromDays(1)), redistribution: true);

        _license.Current.IsEnabled(LicenseFeature.Redistribution).Should().BeTrue();
        
        
        _counter.Increment();
        _logger.Collector.GetSnapshot().Should()
            .BeEmpty();
    }


}