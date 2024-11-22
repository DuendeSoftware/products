using System;
using System.Collections.Generic;
using System.Security.Claims;
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
    public void number_of_tokens_issued_is_counted()
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

        _logger.Collector.GetSnapshot().Should()
            .ContainSingle(r =>
                r.Message ==
                $"IdentityServer has handled {_counter.Threshold + 1} protocol requests without a license. In future versions, unlicensed IdentityServer instances will shut down after {_counter.Threshold} protocol requests. Please contact sales to obtain a license. If you are running in a test environment, please use a test license");
    }

    [Fact]
    public void warning_is_logged_for_expired_license()
    {
        _timeProvider.SetUtcNow(_januaryFirst);
        _license.Current = CreateLicense(LicenseEdition.Enterprise, _januaryFirst.Subtract(TimeSpan.FromDays(1)));

        _counter.Increment();
        var expiration = _license.Current.Expiration?.ToString("yyyy-MM-dd");
        _logger.Collector.GetSnapshot().Should()
            .ContainSingle(r =>
                r.Message == $"Your license expired on {expiration}. You are required to obtain a new license. In a future version of IdentityServer, expired licenses will stop the server after 90 days.");
    }
    
    [Fact]
    public void no_expired_license_warning_for_redistribution_license()
    {
        _timeProvider.SetUtcNow(_januaryFirst);
        _license.Current = CreateLicense(LicenseEdition.Enterprise, _januaryFirst.Subtract(TimeSpan.FromDays(1)), redistribution: true);

        _license.Current.IsEnabled(LicenseFeature.Redistribution).Should().BeTrue();
        
        
        _counter.Increment();
        _logger.Collector.GetSnapshot().Should()
            .BeEmpty();
    }

    private static License CreateLicense(LicenseEdition edition, DateTimeOffset expiration, bool redistribution = false)
    {
        var claims = new List<Claim>
        {
            new Claim("exp", expiration.ToUnixTimeSeconds().ToString()),
            new Claim("edition", edition.ToString()),
        };
        if (redistribution)
        {
            claims.Add(new Claim("feature", "redistribution"));
        }
        return new(new ClaimsPrincipal(new ClaimsIdentity(claims)));
    }
}