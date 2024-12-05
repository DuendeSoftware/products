using System;
using Duende.IdentityServer.Licensing.v2;
using FluentAssertions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace IdentityServer.UnitTests.Licensing.v2;

public class LicenseExpirationCheckerTests
{

    private readonly LicenseExpirationChecker _expirationCheck;
    private readonly TestLicenseAccessor _license;
    private readonly FakeLogger<LicenseExpirationChecker> _logger;
    private readonly FakeTimeProvider _timeProvider;

    // There is no particular significance to 2024-01-01, this is just an
    // arbitrary date that we will use as a base for expiration tests
    private readonly DateTimeOffset _januaryFirst = new DateTimeOffset(
        new DateOnly(2024, 1, 1),
        new TimeOnly(12, 00),
        TimeSpan.Zero); 
    
    public LicenseExpirationCheckerTests()
    {
        _license = new TestLicenseAccessor();
        _timeProvider = new FakeTimeProvider();
        _logger = new FakeLogger<LicenseExpirationChecker>();
        _expirationCheck = new LicenseExpirationChecker(_license, _timeProvider, _logger);
    }

    [Fact]
    public void warning_is_logged_for_expired_license()
    {
        _timeProvider.SetUtcNow(_januaryFirst);
        _license.Current = LicenseFactory.Create(LicenseEdition.Enterprise, _januaryFirst.Subtract(TimeSpan.FromDays(1)));

        _expirationCheck.CheckExpiration();
        var expiration = _license.Current.Expiration?.ToString("yyyy-MM-dd");
        // REMINDER - If this test needs to change because the log message was updated, so should no_warning_is_logged_for_unexpired_license
        _logger.Collector.GetSnapshot().Should()
            .ContainSingle(r =>
                r.Message == $"In a future version of IdentityServer, expired licenses will stop the server after 90 days.");
    }

    [Fact]
    public void no_warning_is_logged_for_unexpired_license()
    {
        _timeProvider.SetUtcNow(_januaryFirst);
        _license.Current = LicenseFactory.Create(LicenseEdition.Enterprise, _januaryFirst);

        _expirationCheck.CheckExpiration();
        var expiration = _license.Current.Expiration?.ToString("yyyy-MM-dd");
        _logger.Collector.GetSnapshot().Should()
            .NotContain(r =>
                r.Message == $"In a future version of IdentityServer, expired licenses will stop the server after 90 days.");
    }
    
    [Fact]
    public void no_expired_license_warning_for_redistribution_license()
    {
        _timeProvider.SetUtcNow(_januaryFirst);
        _license.Current = LicenseFactory.Create(LicenseEdition.Enterprise, _januaryFirst.Subtract(TimeSpan.FromDays(1)), redistribution: true);

        _license.Current.IsEnabled(LicenseFeature.Redistribution).Should().BeTrue();
        
        
        _expirationCheck.CheckExpiration();
        _logger.Collector.GetSnapshot().Should()
            .BeEmpty();
    }
}