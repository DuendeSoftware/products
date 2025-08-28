// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Licensing;
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
        Context.LogMessages.ToString().ShouldContain("You do not have a valid license key for the Duende BFF security framework");
    }

    [Fact]
    public async Task Given_no_license_then_number_of_active_sessions_is_limited()
    {
        Bff.LicenseKey = null;


        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend());

        var activeSessions = new List<BffHttpClient>();

        for (int i = 0; i < Constants.LicenseEnforcement.MaximumNumberOfActiveSessionsInTrialMode; i++)
        {
            var client = Bff.BuildBrowserClient(Bff.Url());
            activeSessions.Add(client);
            await client.Login();
        }

        bool isLoggedIn = false;
        foreach (var activeSession in activeSessions)
        {
            isLoggedIn = await activeSession.GetIsUserLoggedInAsync();
            isLoggedIn.ShouldBeTrue();
        }

        await Bff.BrowserClient.Login();
        isLoggedIn = await Bff.BrowserClient.GetIsUserLoggedInAsync();
        isLoggedIn.ShouldBeTrue();

        isLoggedIn = await activeSessions.First().GetIsUserLoggedInAsync();
        isLoggedIn.ShouldBeFalse();

        isLoggedIn = await activeSessions.First().GetIsUserLoggedInAsync();
        isLoggedIn.ShouldBeFalse();

    }

    [Fact]
    public async Task Given_expired_license_then_log_error()
    {

        Bff.LicenseKey = "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzA0MDY3MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJTdGFydGVyIiwiaWQiOiI3ODk2IiwiZmVhdHVyZSI6ImJmZiJ9.YcRGLlVuNBSqNuO1mdXk4GvvVEQFfQUNAnTkzs9W2iNKCxLXrZ5mDPuyTNsDSwEqsfXG8bUCVFxFGp1Bfkxs8hUIBiKuVXfeIB_lmpj5f-KueZ_XlWm0pYT-ROAzVbDdNgMR9YqCPAw8ANclk7HwRcXc0VnLNcKRFrZ0OOWNysFIanTmg7hRIQmDuMLNc2j8HCZSRJ06fijecS72lM4Vv9a6myJvAsASQhKnWTLzQvdzW7T99eobLy45qJu39LMTQkPkkJUS41YPmi2_kEmeMcRucgU4dQKHD5zT9KmzPVWJwsyowWIJ6U7lZ8FXZ8c9POsQeTeQEJY6FheJ2Ut-6Q";

        await InitializeAsync();
        Context.LogMessages.ToString().ShouldContain("Your license for Duende BFF security framework has expired on");
    }

    [Fact]
    public async Task Given_valid_license_then_details()
    {

        SetupValidLicenseWithoutFrontends();

        await InitializeAsync();

        var log = Context.LogMessages.ToString();
        log.ShouldContain("Duende BFF security Framework License information:");
        log.ShouldContain("- Edition: Starter");
        log.ShouldContain("- Expiration: 11/15/2024 00:00:00 +00:00");
        log.ShouldContain("- LicenseContact: joe@duendesoftware.com");
        log.ShouldContain("- LicenseContact: joe@duendesoftware.com");
        log.ShouldContain("- Number of frontends licensed: not licensed for multi-frontend feature");
    }

    private void SetupValidLicenseWithoutFrontends()
    {
        The.Clock = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 1, 1, 1, TimeSpan.Zero));
        Bff.LicenseKey = "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzA0MDY3MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJTdGFydGVyIiwiaWQiOiI3ODk2IiwiZmVhdHVyZSI6ImJmZiJ9.YcRGLlVuNBSqNuO1mdXk4GvvVEQFfQUNAnTkzs9W2iNKCxLXrZ5mDPuyTNsDSwEqsfXG8bUCVFxFGp1Bfkxs8hUIBiKuVXfeIB_lmpj5f-KueZ_XlWm0pYT-ROAzVbDdNgMR9YqCPAw8ANclk7HwRcXc0VnLNcKRFrZ0OOWNysFIanTmg7hRIQmDuMLNc2j8HCZSRJ06fijecS72lM4Vv9a6myJvAsASQhKnWTLzQvdzW7T99eobLy45qJu39LMTQkPkkJUS41YPmi2_kEmeMcRucgU4dQKHD5zT9KmzPVWJwsyowWIJ6U7lZ8FXZ8c9POsQeTeQEJY6FheJ2Ut-6Q";
    }

    [Fact]
    public async Task When_not_licence_for_multi_frontends_then_warns()
    {
        SetupValidLicenseWithoutFrontends();
        await InitializeAsync();

        Bff.AddOrUpdateFrontend(Some.BffFrontend());
        var log = Context.LogMessages.ToString();
        log.ShouldContain($"Frontend {The.FrontendName} was added. However, your current license does not support multiple frontends");

    }

    [Fact]
    public async Task When_licenced_for_frontends_then_info()
    {
        SetupValidLicenseWithoutFrontends();

        Bff.OnConfigureServices += services =>
        {
            Claim[] claims = [
                new Claim("feature", "bff"),
                new Claim("bff_frontend_limit", "10"),
            ];

            services.AddSingleton<LicenseValidator>(sp =>
            {
                return new LicenseValidator(
                    logger: sp.GetRequiredService<ILogger<LicenseValidator>>(),
                    claims: new ClaimsPrincipal(new ClaimsIdentity(claims)),
                    timeProvider: The.Clock);
            });
        };
        await InitializeAsync();

        Bff.AddOrUpdateFrontend(Some.BffFrontend());
        var log = Context.LogMessages.ToString();
        log.ShouldContain($"Frontend {The.FrontendName} was added. Currently using 1 of 10 in the BFF License");

    }

    [Fact]
    public async Task When_exceeding_number_of_licensed_frontends_then_warning()
    {
        SetupValidLicenseWithoutFrontends();

        Bff.OnConfigureServices += services =>
        {
            Claim[] claims = [
                new Claim("feature", "bff"),
                new Claim("bff_frontend_limit", "1"),
            ];

            services.AddSingleton<LicenseValidator>(sp =>
            {
                return new LicenseValidator(
                    logger: sp.GetRequiredService<ILogger<LicenseValidator>>(),
                    claims: new ClaimsPrincipal(new ClaimsIdentity(claims)),
                    timeProvider: The.Clock);
            });
        };
        await InitializeAsync();

        Bff.AddOrUpdateFrontend(Some.BffFrontend());
        Bff.AddOrUpdateFrontend(Some.BffFrontend() with
        {
            Name = BffFrontendName.Parse("second")
        });
        var log = Context.LogMessages.ToString();
        log.ShouldContain($"Frontend {The.FrontendName} was added. Currently using 1 of 1 in the BFF License");
        log.ShouldContain($"Frontend second was added. This exceeds the maximum number of frontends allowed by your license");

    }
}
