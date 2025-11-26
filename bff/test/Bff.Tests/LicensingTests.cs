// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Licensing;
using Duende.Bff.Tests.TestInfra;
using Xunit.Abstractions;

namespace Duende.Bff.Tests;

public class LicensingTests(ITestOutputHelper output) : BffTestBase(output)
{
    [Fact]
    public async Task Given_no_license_then_error_log()
    {
        Bff.LicenseKey = null;

        await InitializeAsync();
        var bffLogMessages = Context.LogMessages.ToString().Split(Environment.NewLine).Where(x => x.StartsWith("bff"));
        bffLogMessages.ShouldContain(x =>
            x.Contains("[Error]")
            && x.Contains("You do not have a valid license key for the Duende software."));
    }

    [Fact]
    public async Task Given_expired_license_then_log_warning()
    {
        SetupExpiredLicense();

        await InitializeAsync();
        var bffLogMessages = Context.LogMessages.ToString().Split(Environment.NewLine).Where(x => x.StartsWith("bff"));
        bffLogMessages.ShouldContain(x =>
            x.Contains("[Warning]")
            && x.Contains("Your license for the Duende software has expired"));
    }

    [Fact]
    public async Task Given_valid_but_expired_license_then_no_valid_or_trial_mode_logs()
    {
        SetupExpiredLicense();

        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend());

        for (var i = 0; i <= LicenseValidator.MaximumAllowedSessionsInTrialMode; i++)
        {
            var subjectId = Guid.NewGuid().ToString();
            await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, subjectId);
            await Bff.BrowserClient.Login();
        }

        var bffLogMessages = Context.LogMessages.ToString().Split(Environment.NewLine).Where(x => x.StartsWith("bff"))
            .ToList();

        bffLogMessages.ShouldNotContain(x =>
            x.Contains("You do not have a valid license key for the Duende software."));

        bffLogMessages.ShouldContain(x => x.Contains("Your license for the Duende software has expired on "));

        bffLogMessages.ShouldNotContain(x =>
            x.Contains("BFF is running in trial mode. The maximum number of allowed authenticated sessions "));
    }

    [Fact]
    public async Task Should_not_log_error_when_below_trial_mode_authenticated_session_limit()
    {
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend());

        for (var i = 0; i < LicenseValidator.MaximumAllowedSessionsInTrialMode; i++)
        {
            var subjectId = Guid.NewGuid().ToString();
            await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, subjectId);
            await Bff.BrowserClient.Login();
        }

        var bffLogMessages = Context.LogMessages.ToString().Split(Environment.NewLine).Where(x => x.StartsWith("bff"))
            .ToList();
        bffLogMessages.ShouldNotContain(x =>
            x.Contains("[Error]")
            && x.Contains("BFF is running in trial mode. The maximum number of allowed authenticated sessions "));
    }

    [Fact]
    public async Task Should_log_error_when_trial_mode_authenticated_session_limit_exceeded()
    {
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend());

        for (var i = 0; i < LicenseValidator.MaximumAllowedSessionsInTrialMode + 6; i++)
        {
            var subjectId = Guid.NewGuid().ToString();
            await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, subjectId);
            await Bff.BrowserClient.Login();
        }

        var bffLogMessages = Context.LogMessages.ToString().Split(Environment.NewLine).Where(x => x.StartsWith("bff"))
            .ToList();

        var trialModeLogCount = bffLogMessages.Count(x =>
            x.Contains("[Error]")
            && x.Contains("BFF is running in trial mode. The maximum number of allowed authenticated sessions "));
        trialModeLogCount.ShouldBe(6);
    }

    private void SetupExpiredLicense() => Bff.LicenseKey =
        "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzA0MDY3MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJTdGFydGVyIiwiaWQiOiI3ODk2IiwiZmVhdHVyZSI6ImJmZiJ9.YcRGLlVuNBSqNuO1mdXk4GvvVEQFfQUNAnTkzs9W2iNKCxLXrZ5mDPuyTNsDSwEqsfXG8bUCVFxFGp1Bfkxs8hUIBiKuVXfeIB_lmpj5f-KueZ_XlWm0pYT-ROAzVbDdNgMR9YqCPAw8ANclk7HwRcXc0VnLNcKRFrZ0OOWNysFIanTmg7hRIQmDuMLNc2j8HCZSRJ06fijecS72lM4Vv9a6myJvAsASQhKnWTLzQvdzW7T99eobLy45qJu39LMTQkPkkJUS41YPmi2_kEmeMcRucgU4dQKHD5zT9KmzPVWJwsyowWIJ6U7lZ8FXZ8c9POsQeTeQEJY6FheJ2Ut-6Q";
}
