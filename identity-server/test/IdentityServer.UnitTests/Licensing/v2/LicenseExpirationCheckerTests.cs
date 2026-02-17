// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Licensing.V2;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using UnitTests.Common;

namespace IdentityServer.UnitTests.Licensing.V2;

public class LicenseExpirationCheckerTests
{
    private readonly IdentityServerOptions _options;
    private readonly LicenseAccessor _licenseAccessor;
    private readonly FakeTimeProvider _mockSystemClock;
    private readonly FakeLogger<LicenseExpirationChecker> _logger;
    private readonly LicenseExpirationChecker _expirationCheck;

    //Expiration of all the test licenses we use
    private readonly DateTimeOffset _licenseExpiration = new(
        new DateOnly(2024, 11, 15),
        new TimeOnly(12, 00),
        TimeSpan.Zero);

    public LicenseExpirationCheckerTests()
    {
        _options = new IdentityServerOptions();
        _licenseAccessor = new LicenseAccessor(_options, new NullLogger<LicenseAccessor>());
        _mockSystemClock = new FakeTimeProvider();
        _logger = new FakeLogger<LicenseExpirationChecker>();
        _expirationCheck = new LicenseExpirationChecker(_licenseAccessor, _mockSystemClock, new StubLoggerFactory(_logger));
    }

    [Theory]
    [MemberData(nameof(NonRedistributionLicenseKeys))]
    public void warning_is_logged_for_expired_license(string licenseKey)
    {
        _options.LicenseKey = licenseKey;
        _mockSystemClock.SetUtcNow(_licenseExpiration.AddDays(1));

        _expirationCheck.CheckExpiration();

        // REMINDER - If this test needs to change because the log message was updated, so should no_warning_is_logged_for_unexpired_license
        _logger.Collector.GetSnapshot().ShouldContain(r =>
                r.Message ==
                "Your IdentityServer license is expired. Please contact joe@duendesoftware.com from _test or start a conversation with us at https://duende.link/l/contact to renew your license as soon as possible. In a future version, license expiration will be enforced after a grace period. See https://duende.link/l/expired for more information.",
            1);
    }

    [Theory]
    [MemberData(nameof(NonRedistributionLicenseKeys))]
    public void no_warning_is_logged_for_unexpired_license(string licenseKey)
    {
        _options.LicenseKey = licenseKey;
        _mockSystemClock.SetUtcNow(_licenseExpiration.AddDays(-1));

        _expirationCheck.CheckExpiration();

        _logger.Collector.GetSnapshot().ShouldNotContain(r =>
                r.Message == "Your IdentityServer license is expired. Please contact joe@duendesoftware.com from _test or start a conversation with us at https://duende.link/l/contact to renew your license as soon as possible. In a future version, license expiration will be enforced after a grace period. See https://duende.link/l/expired for more information.");
    }

    [Theory]
    [MemberData(nameof(RedistributionLicenseKeys))]
    public void no_expired_license_warning_for_redistribution_license(string licenseKey)
    {
        _options.LicenseKey = licenseKey;
        _mockSystemClock.SetUtcNow(_licenseExpiration.AddDays(1));

        _licenseAccessor.Current.Redistribution.ShouldBeTrue();

        _expirationCheck.CheckExpiration();

        _logger.Collector.GetSnapshot().ShouldBeEmpty();
    }

    public static TheoryData<string> NonRedistributionLicenseKeys =>
    [
        //Starter Standard
        "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJTdGFydGVyIiwiaWQiOiI2Njc3In0.WEEZFmwoSmJYVJ9geeSKvpB5GaJKQBUUFfABeeQEwh3Tkdg4gnjEme9WJS03MZkxMPj7nEfv8i0Tl1xwTC4gWpV2bfqDzj3R3eKCvz6BZflcmr14j4fbhbc7jDD26b5wAdyiD3krvkd2VsvVnYTTRCilK1UKr6ZVhmSgU8oXgth8JjQ2wIQ80p9D2nurHuWq6UdFdNqbO8aDu6C2eOQuAVmp6gKo7zBbFTbO1G1J1rGyWX8kXYBZMN0Rj_Xp_sdj34uwvzFsJN0i1EwhFATFS6vf6na_xpNz9giBNL04ulDRR95ZSE1vmRoCuP96fsgK7aYCJV1WSRBHXIrwfJhd7A",
        //Business Standard
        "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJCdXNpbmVzcyIsImlkIjoiNjY3OCJ9.qps2bV5C9TXG-U9hLM7hMrdm8PVMxqDFjAVHSkdvDs7fb03ejOE_8_D1RAUIJtNlZQw2zO1aCgWMEC8O2so8HzwxG4ic9tTZj-Nccn6azkRJ-R412LEl8jpRS64Y0FXqrv2cmhpd82dLEneK8IikoqryJjF0f12Fsqadpveuz_IMAPixOX1X0thXyblDH58FJnP6N0sSp94yT8Gr4V8G5wfX7fM3vBu_Aa9YZIaUxDLtW7eujHFkRoqfCOwxpa_4gqBg5Q8XwvU9fLJrHkQvtpqEbJWydixRstKF4XBwzyCKfXCegas8OH6yW8FN3V-tgaj-WeCpmgDkE5ngTCuV6g",
        //Starter Standard with Key Management feature
        "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJTdGFydGVyIiwiaWQiOiI2NjgwIiwiZmVhdHVyZSI6ImtleV9tYW5hZ2VtZW50In0.kmArT0vjFE4nhRNg_kchOh_uklaqm3KeworQ9up_4jIBOinbZtVv3NkXtJoHX_lzjs1ftp0eNMSyGg6E29GR7ZZ2hx3SQdQrSdrH4v_sNSFcRZrwzipXBkANssH-0hMQ0s3kdfXdwfmN_8IfCkPCugeMemwUWwbC7QHBdCa6Fr7ZExuMNLpml932D72LMzhlLf780BSic9PKn6odvzGikYK9e2WhYL1zL0REdNHzgwrrUZHesZF98u-gel7skS1Frg6cBcPl_QSSP5KhxmfdPw0b2FUM_B0Tpi-gN54efz0stzccjr9PgcpAfXO82y3vOBB7f44cdv6DG67YwAvv0A",
        //Business Standard with Dynamic Providers feature
        "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJCdXNpbmVzcyIsImlkIjoiNjY4MSIsImZlYXR1cmUiOiJkeW5hbWljX3Byb3ZpZGVycyJ9.HeCNt4O1cXsw4Ujkn2W_sDRmWUDstYtLPQ7UhYvneUgxed7auFyroBJojkwh9RwflWD1HphHYx4KRuZML_OO0BYzGr865gWI55x6KxHM5mxY5hpVJMTLottSgIv-hyXdNxTWCxP1jluzs1b4JgWmXnU83AuRtAenMpZpZcOY7Pldkd84JA1BXE5gEM6v2U8HCTgydY1QmTd_RjYlicGqmDOkKALiHOxREyXLsRgy4pmQfG6gs99heXdzs2k4jRLLXsTFHP7UxupRTYDPCgXT19ub6l4KG95rPBSMV_vXEwydcFGJe1uFQdd1btUSVe50XX1hmZx4P4SymlX0iuimMg",
        //Enterprise Standard
        "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJFbnRlcnByaXNlIiwiaWQiOiI2Njg1In0.UgguIFVBciR8lpTF5RuM3FNcIm8m8wGR4Mt0xOCgo-XknFwXBpxOfr0zVjciGboteOl9AFtrqZLopEjsYXGFh2dkl5AzRyq--Ai5y7aezszlMpq8SkjRRCeBUYLNnEO41_YnfjYhNrcmb0Jx9wMomCv74vU3f8Hulz1ppWtoL-MVcGq0fhv_KOCP49aImCgiawPJ6a_bfs2C1QLpj-GG411OhdyrO9QLIH_We4BEvRUyajraisljB1VQzC8Q6188Mm_BLwl4ZENPaoNE4egiqTAuoTS5tb1l732-CGZwpGuU80NSpJbrUc6jd3rVi_pNf_1rH-O4Xt0HRCWiNCDYgg",
        //Community
        "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzAwODcwNDAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJDb21tdW5pdHkiLCJpZCI6IjY3MDMifQ.UxC5uefxXA9sC2sdIEP6FCUEeBhl1EQuVsjL9kpXpPhvy3xCj0sSKcxkw1QshpM-u23hOEj0enzAgwUPNrkC8QH5xIigpI2FknFKSNjKl2dD5s_fqPTr7re_fefbE3WeImogpKOcwMHETr_BeUbvUbrvCw_5sZcYtsUy15d8wf6ZZlVqUCL027qNB5Ssg-fTq3j_8QNRoJCFEnl2Q6MIVY4wyeb2fxF63V2vpNFh8zDUJlLerDhCIvWngLOc8VyArTrjmrIsHSP2xFSFrMZfen_vOjo9-Oo8BU7JUw1PWdAMIqTO0CWK5DrZfTsEWDPCdHzmbzMSmyjKxoejfAH1og"
    ];

    public static TheoryData<string> RedistributionLicenseKeys =>
    [
        //Starter Redistribution
        "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiY29udGFjdEBkdWVuZGVzb2Z0d2FyZS5jb20iLCJlZGl0aW9uIjoiU3RhcnRlciIsImlkIjoiNjY4MiIsImZlYXR1cmUiOiJpc3YiLCJwcm9kdWN0IjoiVEJEIn0.Ag4HLR1TVJ2VYgW1MJbpIHvAerx7zaHoM4CLu7baipsZVwc82ZkmLUeO_yB3CqN7N6XepofwZ-RcloxN8UGZ6qPRGQPE1cOMrp8YqxLOI38gJbxALOBG5BB6YTCMf_TKciXn1c3XhrsxVDayMGxAU68fKDCg1rnamBehZfXr2uENipNPkGDh_iuRw2MUgeGY96CGvwCC5R0E6UnvGZbjQ7dFYV-CkAHuE8dEAr0pX_gD77YsYcSxq5rNUavcNnWV7-3knFwozNqi02wTDpcKtqaL2mAr0nRof1E8Df9C8RwCTWXSaWhr9_47W2I1r_IhLYS2Jnq6m_3BgAIvWL4cjQ",
        //Business Redistribution
        "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiY29udGFjdEBkdWVuZGVzb2Z0d2FyZS5jb20iLCJlZGl0aW9uIjoiQnVzaW5lc3MiLCJpZCI6IjY2ODMiLCJmZWF0dXJlIjoiaXN2IiwicHJvZHVjdCI6IlRCRCJ9.rYDrY6UUKgZfnfx7GA1PILYj9XICIjC9aS06P8rUAuXYjxiagEIEkacKt3GcccJI6k0lMb6qbd3Hv-Q9rDDyDSxUZxwvGzVlhRrIditOI38FoN3trUd5RU6S7A_RSDd4uV0L1T8NKUKGlOvu8_7egcIy-E8q34GA5BNU2lV2Gsaa7yWAyTKZh7YPIP4y_TwLxOcw2GRn6dQq73-O_XaAIf0AxFowW1GsiBrirzE_TKwJ8VkbvN3O-yVT-ntPvoK0tHRKoG5yh8GPuDORQtlis_5bZHHFzazXVMul1rkYWSU9OhIdixvI44q1q1_5VGoGJ3SLFIFsdWM0ZvnPx7_Bqg",
        //Enterprise Redistribution
        "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiY29udGFjdEBkdWVuZGVzb2Z0d2FyZS5jb20iLCJlZGl0aW9uIjoiRW50ZXJwcmlzZSIsImlkIjoiNjY4NCIsImZlYXR1cmUiOiJpc3YiLCJwcm9kdWN0IjoiVEJEIn0.Y-bbdSsdHHzrJs40CpEIsgi7ugc8ScTa2ArCuL-wM__O6znygAUTGOLrzhFaeRibud5lNXSYaA0vkkF1UFQS4HJF_wTMe5pYH4DT1vVYaVXd9Xyqn-klQvBLcoo4JAoFNau0Az-czbo6UBkejKn-7QDnJunFcHaYenDpzgsXHiaK4mkIMRI_OnBYKegNa_xvYRRzorKkT3x8q1n7vUnx80-b6Jf2Y0u6fPsLwE2Or-VBXRpTGL20MBtcPS56wQDDdl4eKkW716lHS-Iyh5KW3K5HVKRxd86ot18MY6Bd3PPUQocFYXd5KhTH_YKvwVqAUkc0MhHYJLFV_5Q8qSRECA"
    ];
}
