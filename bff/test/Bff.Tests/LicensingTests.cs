// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestInfra;

namespace Duende.Bff.Tests;

public class LicensingTests : BffTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_expired_license_then_error_log()
    {
        var (validator, logs) = TestLicense.CreateValidator(Now.AddDays(-10), new FakeTimeProvider(Now));

        validator.ValidateLicense();

        var entries = logs.GetSnapshot();
        entries.ShouldContain(entry =>
            entry.Level == LogLevel.Error
            && entry.Message.StartsWith("Your Duende license expired"));
    }

    [Fact]
    public void Given_valid_license_then_no_expired_error_log()
    {
        var (validator, logs) = TestLicense.CreateValidator(Now.AddYears(1), new FakeTimeProvider(Now));

        validator.ValidateLicense();

        var entries = logs.GetSnapshot();
        entries.ShouldNotContain(entry => entry.Level == LogLevel.Error);
        entries.ShouldContain(entry =>
            entry.Level == LogLevel.Information
            && entry.Message.StartsWith("Your Duende license expires on"));
    }

    [Fact]
    public void Given_no_license_then_error_log()
    {
        var (validator, logs) = TestLicense.CreateValidatorWithoutLicense();

        validator.ValidateLicense();

        var entries = logs.GetSnapshot();
        entries.ShouldContain(entry =>
            entry.Level == LogLevel.Error
            && entry.Message.Contains("You do not have a valid license key for the Duende software."));
    }

    [Fact]
    public async Task Given_valid_v1_license_key_in_options_then_license_is_read_without_error_logs()
    {
        The.Clock = new FakeTimeProvider(Now);
        Bff.LicenseKey = ValidV1ButExpiredLicenseKey;

        await InitializeAsync();

        var bffLogMessages = Context.LogMessages.ToString().Split(Environment.NewLine).Where(x => x.StartsWith("bff")).ToList();
        bffLogMessages.ShouldContain(x => x.Contains("Your Duende license expired"));
    }

    private const string ValidV1ButExpiredLicenseKey =
        "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzA0MDY3MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJTdGFydGVyIiwiaWQiOiI3ODk2IiwiZmVhdHVyZSI6ImJmZiJ9.YcRGLlVuNBSqNuO1mdXk4GvvVEQFfQUNAnTkzs9W2iNKCxLXrZ5mDPuyTNsDSwEqsfXG8bUCVFxFGp1Bfkxs8hUIBiKuVXfeIB_lmpj5f-KueZ_XlWm0pYT-ROAzVbDdNgMR9YqCPAw8ANclk7HwRcXc0VnLNcKRFrZ0OOWNysFIanTmg7hRIQmDuMLNc2j8HCZSRJ06fijecS72lM4Vv9a6myJvAsASQhKnWTLzQvdzW7T99eobLy45qJu39LMTQkPkkJUS41YPmi2_kEmeMcRucgU4dQKHD5zT9KmzPVWJwsyowWIJ6U7lZ8FXZ8c9POsQeTeQEJY6FheJ2Ut-6Q";
}
