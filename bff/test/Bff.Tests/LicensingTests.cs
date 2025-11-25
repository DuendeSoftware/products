// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestInfra;
using Microsoft.Extensions.Time.Testing;
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
        bffLogMessages.ShouldContain(x => x.Contains("You do not have a valid license key for the Duende software."));
    }

    [Fact]
    public async Task Given_expired_license_then_log_error()
    {
        Bff.LicenseKey =
            "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzA0MDY3MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJTdGFydGVyIiwiaWQiOiI3ODk2IiwiZmVhdHVyZSI6ImJmZiJ9.YcRGLlVuNBSqNuO1mdXk4GvvVEQFfQUNAnTkzs9W2iNKCxLXrZ5mDPuyTNsDSwEqsfXG8bUCVFxFGp1Bfkxs8hUIBiKuVXfeIB_lmpj5f-KueZ_XlWm0pYT-ROAzVbDdNgMR9YqCPAw8ANclk7HwRcXc0VnLNcKRFrZ0OOWNysFIanTmg7hRIQmDuMLNc2j8HCZSRJ06fijecS72lM4Vv9a6myJvAsASQhKnWTLzQvdzW7T99eobLy45qJu39LMTQkPkkJUS41YPmi2_kEmeMcRucgU4dQKHD5zT9KmzPVWJwsyowWIJ6U7lZ8FXZ8c9POsQeTeQEJY6FheJ2Ut-6Q";

        await InitializeAsync();
        var bffLogMessages = Context.LogMessages.ToString().Split(Environment.NewLine).Where(x => x.StartsWith("bff"));
        bffLogMessages.ShouldContain(x => x.Contains("Your license for the Duende Software has expired on "));
    }

    [Fact]
    public async Task Given_valid_license_then_details()
    {
        SetupValidLicenseWithoutFrontends();

        await InitializeAsync();

        var bffLogMessages = Context.LogMessages.ToString().Split(Environment.NewLine).Where(x => x.StartsWith("bff"))
            .ToList();
        bffLogMessages.ShouldNotContain(x => x.Contains("You do not have a valid license key for the Duende software."));
        bffLogMessages.ShouldNotContain(x => x.Contains("Your license for the Duende Software has expired on "));
    }

    private void SetupValidLicenseWithoutFrontends()
    {
        The.Clock = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 1, 1, 1, TimeSpan.Zero));
        Bff.LicenseKey =
            "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzA0MDY3MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJTdGFydGVyIiwiaWQiOiI3ODk2IiwiZmVhdHVyZSI6ImJmZiJ9.YcRGLlVuNBSqNuO1mdXk4GvvVEQFfQUNAnTkzs9W2iNKCxLXrZ5mDPuyTNsDSwEqsfXG8bUCVFxFGp1Bfkxs8hUIBiKuVXfeIB_lmpj5f-KueZ_XlWm0pYT-ROAzVbDdNgMR9YqCPAw8ANclk7HwRcXc0VnLNcKRFrZ0OOWNysFIanTmg7hRIQmDuMLNc2j8HCZSRJ06fijecS72lM4Vv9a6myJvAsASQhKnWTLzQvdzW7T99eobLy45qJu39LMTQkPkkJUS41YPmi2_kEmeMcRucgU4dQKHD5zT9KmzPVWJwsyowWIJ6U7lZ8FXZ8c9POsQeTeQEJY6FheJ2Ut-6Q";
    }
}
