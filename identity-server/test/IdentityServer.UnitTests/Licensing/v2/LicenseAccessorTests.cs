// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Licensing.V2;
using Microsoft.Extensions.Logging.Testing;

namespace IdentityServer.UnitTests.Licensing.V2;

public class LicenseAccessorTests
{
    private readonly IdentityServerOptions _options;
    private readonly LicenseAccessor _licenseAccessor;
    private readonly FakeLogger<LicenseAccessor> _logger;

    public LicenseAccessorTests()
    {
        _options = new IdentityServerOptions();
        _logger = new FakeLogger<LicenseAccessor>();
        _licenseAccessor = new LicenseAccessor(_options, _logger);
    }

    [Theory]
    [MemberData(nameof(LicenseTestCases))]
    internal void license_set_in_options_is_parsed_correctly(int serialNumber, LicenseEdition edition, bool isRedistribution, string contact, bool addDynamicProviders, bool addKeyManagement, int? allowedClients, int? allowedIssuers, string key)
    {
        _options.LicenseKey = key;

        var l = _licenseAccessor.Current;

        l.IsConfigured.ShouldBeTrue();
        l.Edition.ShouldBe(edition);
        l.Extras.ShouldBeEmpty();
        l.CompanyName.ShouldBe("_test");
        l.ContactInfo.ShouldBe(contact);
        l.SerialNumber.ShouldBe(serialNumber);
        l.Expiration!.Value.Date.ShouldBe(new DateTime(2024, 11, 15));
        l.Redistribution.ShouldBe(isRedistribution);
        l.ClientLimit.ShouldBe(allowedClients);
        l.IssuerLimit.ShouldBe(allowedIssuers);

        var enterpriseFeaturesEnabled = edition == LicenseEdition.Enterprise || edition == LicenseEdition.Community;
        var businessFeaturesEnabled = enterpriseFeaturesEnabled || edition == LicenseEdition.Business;

        _licenseAccessor.Current.IsEnabled(LicenseFeature.DynamicProviders).ShouldBe(enterpriseFeaturesEnabled || addDynamicProviders);
        _licenseAccessor.Current.IsEnabled(LicenseFeature.ResourceIsolation).ShouldBe(enterpriseFeaturesEnabled);
        _licenseAccessor.Current.IsEnabled(LicenseFeature.DPoP).ShouldBe(enterpriseFeaturesEnabled);
        _licenseAccessor.Current.IsEnabled(LicenseFeature.CIBA).ShouldBe(enterpriseFeaturesEnabled);

        _licenseAccessor.Current.IsEnabled(LicenseFeature.KeyManagement).ShouldBe(businessFeaturesEnabled || addKeyManagement);
        _licenseAccessor.Current.IsEnabled(LicenseFeature.PAR).ShouldBe(businessFeaturesEnabled);
        _licenseAccessor.Current.IsEnabled(LicenseFeature.DCR).ShouldBe(businessFeaturesEnabled);
        _licenseAccessor.Current.IsEnabled(LicenseFeature.ServerSideSessions).ShouldBe(businessFeaturesEnabled);
    }


    public static IEnumerable<object[]> LicenseTestCases() =>
    [
        // Order of parameters is: int serialNumber, LicenseEdition edition, bool isRedistribution, string contact, int? allowedClients, int? allowedIssuers, string key
        
        // Standard licenses
        [6685, LicenseEdition.Enterprise, false, "joe@duendesoftware.com", false, false, 5, null, "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJFbnRlcnByaXNlIiwiaWQiOiI2Njg1In0.UgguIFVBciR8lpTF5RuM3FNcIm8m8wGR4Mt0xOCgo-XknFwXBpxOfr0zVjciGboteOl9AFtrqZLopEjsYXGFh2dkl5AzRyq--Ai5y7aezszlMpq8SkjRRCeBUYLNnEO41_YnfjYhNrcmb0Jx9wMomCv74vU3f8Hulz1ppWtoL-MVcGq0fhv_KOCP49aImCgiawPJ6a_bfs2C1QLpj-GG411OhdyrO9QLIH_We4BEvRUyajraisljB1VQzC8Q6188Mm_BLwl4ZENPaoNE4egiqTAuoTS5tb1l732-CGZwpGuU80NSpJbrUc6jd3rVi_pNf_1rH-O4Xt0HRCWiNCDYgg"],
        [6678, LicenseEdition.Business, false, "joe@duendesoftware.com", false, false, 5, 1, "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJCdXNpbmVzcyIsImlkIjoiNjY3OCJ9.qps2bV5C9TXG-U9hLM7hMrdm8PVMxqDFjAVHSkdvDs7fb03ejOE_8_D1RAUIJtNlZQw2zO1aCgWMEC8O2so8HzwxG4ic9tTZj-Nccn6azkRJ-R412LEl8jpRS64Y0FXqrv2cmhpd82dLEneK8IikoqryJjF0f12Fsqadpveuz_IMAPixOX1X0thXyblDH58FJnP6N0sSp94yT8Gr4V8G5wfX7fM3vBu_Aa9YZIaUxDLtW7eujHFkRoqfCOwxpa_4gqBg5Q8XwvU9fLJrHkQvtpqEbJWydixRstKF4XBwzyCKfXCegas8OH6yW8FN3V-tgaj-WeCpmgDkE5ngTCuV6g"],
        [6677, LicenseEdition.Starter, false, "joe@duendesoftware.com", false, false, 5, 1, "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJTdGFydGVyIiwiaWQiOiI2Njc3In0.WEEZFmwoSmJYVJ9geeSKvpB5GaJKQBUUFfABeeQEwh3Tkdg4gnjEme9WJS03MZkxMPj7nEfv8i0Tl1xwTC4gWpV2bfqDzj3R3eKCvz6BZflcmr14j4fbhbc7jDD26b5wAdyiD3krvkd2VsvVnYTTRCilK1UKr6ZVhmSgU8oXgth8JjQ2wIQ80p9D2nurHuWq6UdFdNqbO8aDu6C2eOQuAVmp6gKo7zBbFTbO1G1J1rGyWX8kXYBZMN0Rj_Xp_sdj34uwvzFsJN0i1EwhFATFS6vf6na_xpNz9giBNL04ulDRR95ZSE1vmRoCuP96fsgK7aYCJV1WSRBHXIrwfJhd7A"],
        [6679, LicenseEdition.Bff, false, "joe@duendesoftware.com", false, false, 5, 1, "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJCZmYiLCJpZCI6IjY2NzkifQ.kZFlPuSZRG-p_S5M5inZjHEFB2mGjRri8ogSXj-CnyUmHcoNOaRzQFIC6YqZQjBNmd8-BWRhfyCDj_Ux8hJpPBPKzfYfpd__YvmF3gdRCgZJVSgwCsETH5b0neoPh1SixVxKtpYPHzQF3t-MRfoej50omwCpMBa7phfqJ7aMQnQxQnFe9yVaTJC63HFsOnaXLkGpGGz_Xm-J14uqwXmJsi2qyJV_9elk6Ip_6hK2tcZBanMsLcyPDXVdciCuUZ8hbzmcfeuNSD-UYsMb2NTtrFEBEuAQ5_JIXZ4ZzRfeXFJOOYW43UyPxwjC1XjmN9ruUSAK_ouYiaOScBhH00kLlg"],
        [6703, LicenseEdition.Community, false, "joe@duendesoftware.com", false, false, 5, null, "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzAwODcwNDAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJDb21tdW5pdHkiLCJpZCI6IjY3MDMifQ.UxC5uefxXA9sC2sdIEP6FCUEeBhl1EQuVsjL9kpXpPhvy3xCj0sSKcxkw1QshpM-u23hOEj0enzAgwUPNrkC8QH5xIigpI2FknFKSNjKl2dD5s_fqPTr7re_fefbE3WeImogpKOcwMHETr_BeUbvUbrvCw_5sZcYtsUy15d8wf6ZZlVqUCL027qNB5Ssg-fTq3j_8QNRoJCFEnl2Q6MIVY4wyeb2fxF63V2vpNFh8zDUJlLerDhCIvWngLOc8VyArTrjmrIsHSP2xFSFrMZfen_vOjo9-Oo8BU7JUw1PWdAMIqTO0CWK5DrZfTsEWDPCdHzmbzMSmyjKxoejfAH1og"],

        // Redistribution licenses
        [6684, LicenseEdition.Enterprise, true, "contact@duendesoftware.com", false, false, 5, null, "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiY29udGFjdEBkdWVuZGVzb2Z0d2FyZS5jb20iLCJlZGl0aW9uIjoiRW50ZXJwcmlzZSIsImlkIjoiNjY4NCIsImZlYXR1cmUiOiJpc3YiLCJwcm9kdWN0IjoiVEJEIn0.Y-bbdSsdHHzrJs40CpEIsgi7ugc8ScTa2ArCuL-wM__O6znygAUTGOLrzhFaeRibud5lNXSYaA0vkkF1UFQS4HJF_wTMe5pYH4DT1vVYaVXd9Xyqn-klQvBLcoo4JAoFNau0Az-czbo6UBkejKn-7QDnJunFcHaYenDpzgsXHiaK4mkIMRI_OnBYKegNa_xvYRRzorKkT3x8q1n7vUnx80-b6Jf2Y0u6fPsLwE2Or-VBXRpTGL20MBtcPS56wQDDdl4eKkW716lHS-Iyh5KW3K5HVKRxd86ot18MY6Bd3PPUQocFYXd5KhTH_YKvwVqAUkc0MhHYJLFV_5Q8qSRECA"],
        [6683, LicenseEdition.Business, true, "contact@duendesoftware.com", false, false, 5, 1, "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiY29udGFjdEBkdWVuZGVzb2Z0d2FyZS5jb20iLCJlZGl0aW9uIjoiQnVzaW5lc3MiLCJpZCI6IjY2ODMiLCJmZWF0dXJlIjoiaXN2IiwicHJvZHVjdCI6IlRCRCJ9.rYDrY6UUKgZfnfx7GA1PILYj9XICIjC9aS06P8rUAuXYjxiagEIEkacKt3GcccJI6k0lMb6qbd3Hv-Q9rDDyDSxUZxwvGzVlhRrIditOI38FoN3trUd5RU6S7A_RSDd4uV0L1T8NKUKGlOvu8_7egcIy-E8q34GA5BNU2lV2Gsaa7yWAyTKZh7YPIP4y_TwLxOcw2GRn6dQq73-O_XaAIf0AxFowW1GsiBrirzE_TKwJ8VkbvN3O-yVT-ntPvoK0tHRKoG5yh8GPuDORQtlis_5bZHHFzazXVMul1rkYWSU9OhIdixvI44q1q1_5VGoGJ3SLFIFsdWM0ZvnPx7_Bqg"],
        [6682, LicenseEdition.Starter, true, "contact@duendesoftware.com", false, false, 5, 1, "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiY29udGFjdEBkdWVuZGVzb2Z0d2FyZS5jb20iLCJlZGl0aW9uIjoiU3RhcnRlciIsImlkIjoiNjY4MiIsImZlYXR1cmUiOiJpc3YiLCJwcm9kdWN0IjoiVEJEIn0.Ag4HLR1TVJ2VYgW1MJbpIHvAerx7zaHoM4CLu7baipsZVwc82ZkmLUeO_yB3CqN7N6XepofwZ-RcloxN8UGZ6qPRGQPE1cOMrp8YqxLOI38gJbxALOBG5BB6YTCMf_TKciXn1c3XhrsxVDayMGxAU68fKDCg1rnamBehZfXr2uENipNPkGDh_iuRw2MUgeGY96CGvwCC5R0E6UnvGZbjQ7dFYV-CkAHuE8dEAr0pX_gD77YsYcSxq5rNUavcNnWV7-3knFwozNqi02wTDpcKtqaL2mAr0nRof1E8Df9C8RwCTWXSaWhr9_47W2I1r_IhLYS2Jnq6m_3BgAIvWL4cjQ"],
        
        // Licenses with extra features
        [6681, LicenseEdition.Business, false, "joe@duendesoftware.com", true, false, 5, 1, "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJCdXNpbmVzcyIsImlkIjoiNjY4MSIsImZlYXR1cmUiOiJkeW5hbWljX3Byb3ZpZGVycyJ9.HeCNt4O1cXsw4Ujkn2W_sDRmWUDstYtLPQ7UhYvneUgxed7auFyroBJojkwh9RwflWD1HphHYx4KRuZML_OO0BYzGr865gWI55x6KxHM5mxY5hpVJMTLottSgIv-hyXdNxTWCxP1jluzs1b4JgWmXnU83AuRtAenMpZpZcOY7Pldkd84JA1BXE5gEM6v2U8HCTgydY1QmTd_RjYlicGqmDOkKALiHOxREyXLsRgy4pmQfG6gs99heXdzs2k4jRLLXsTFHP7UxupRTYDPCgXT19ub6l4KG95rPBSMV_vXEwydcFGJe1uFQdd1btUSVe50XX1hmZx4P4SymlX0iuimMg"],
        [6680, LicenseEdition.Starter, false, "joe@duendesoftware.com", false, true, 5, 1, "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiam9lQGR1ZW5kZXNvZnR3YXJlLmNvbSIsImVkaXRpb24iOiJTdGFydGVyIiwiaWQiOiI2NjgwIiwiZmVhdHVyZSI6ImtleV9tYW5hZ2VtZW50In0.kmArT0vjFE4nhRNg_kchOh_uklaqm3KeworQ9up_4jIBOinbZtVv3NkXtJoHX_lzjs1ftp0eNMSyGg6E29GR7ZZ2hx3SQdQrSdrH4v_sNSFcRZrwzipXBkANssH-0hMQ0s3kdfXdwfmN_8IfCkPCugeMemwUWwbC7QHBdCa6Fr7ZExuMNLpml932D72LMzhlLf780BSic9PKn6odvzGikYK9e2WhYL1zL0REdNHzgwrrUZHesZF98u-gel7skS1Frg6cBcPl_QSSP5KhxmfdPw0b2FUM_B0Tpi-gN54efz0stzccjr9PgcpAfXO82y3vOBB7f44cdv6DG67YwAvv0A"]
    ];



    [Fact]
    public void keys_that_cannot_be_parsed_are_treated_the_same_as_an_absent_license()
    {
        _options.LicenseKey = "invalid key";
        _licenseAccessor.Current.IsConfigured.ShouldBeFalse();
        _logger.Collector.GetSnapshot().ShouldContain(r =>
            r.Message == "Error validating the Duende software license key");
    }
}
