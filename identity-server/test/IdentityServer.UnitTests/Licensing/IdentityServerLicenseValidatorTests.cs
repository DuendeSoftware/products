// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using static Duende.License;

namespace UnitTests.Licensing;

public class IdentityServerLicenseValidatorTests(ITestOutputHelper output)
{
    private const string Category = "License validator tests";

    [Fact]
    [Trait("Category", Category)]
    public void When_calling_validate_issuer_and_license_doesnt_include_enough_issuers_then_error_is_logged()
    {
        var licenseValidator = new IdentityServerLicenseValidator();
        var mockLogger = MockLogger.Create();
        var identityServerOptions = new IdentityServerOptions
        {
            LicenseKey = TestLicenses.RedistributionBusinessLicense
        };
        licenseValidator.Initialize(new LoggerFactory([new MockLoggerProvider(mockLogger)]), identityServerOptions);
        licenseValidator.ValidateIssuer("c1");

        licenseValidator.ValidateIssuer("c2");

        var logMessages = string.Join(Environment.NewLine, mockLogger.LogMessages);
        output.WriteLine(logMessages);
        mockLogger.LogMessages.ShouldContain("Your license for IdentityServer includes 1 issuers but you have processed requests for 2 issuers. This indicates that requests for each issuer are being sent to this instance of IdentityServer, which may be due to a network infrastructure configuration issue. If you intend to use multiple issuers, please contact contact@duendesoftware.com at _test or start a conversation with us at https://duende.link/l/contact to upgrade your license as soon as possible. In a future version, this limit will be enforced after a threshold is exceeded. The issuers used were c1, c2.");
    }

    [Fact]
    [Trait("Category", Category)]
    public void When_calling_validate_client_and_license_doesnt_include_enough_clients_then_error_is_logged()
    {
        var licenseValidator = new IdentityServerLicenseValidator();
        var mockLogger = MockLogger.Create();
        var identityServerOptions = new IdentityServerOptions
        {
            LicenseKey = TestLicenses.RedistributionBusinessLicense
        };
        licenseValidator.Initialize(new LoggerFactory([new MockLoggerProvider(mockLogger)]), identityServerOptions);
        licenseValidator.ValidateClient("c1");
        licenseValidator.ValidateClient("c2");
        licenseValidator.ValidateClient("c3");
        licenseValidator.ValidateClient("c4");
        licenseValidator.ValidateClient("c5");

        licenseValidator.ValidateClient("c6");

        var logMessages = string.Join(Environment.NewLine, mockLogger.LogMessages);
        output.WriteLine(logMessages);
        mockLogger.LogMessages.ShouldContain("Your license for IdentityServer includes 5 clients but you have processed requests for 6 clients. Please contact contact@duendesoftware.com at _test or start a conversation with us at https://duende.link/l/contact to upgrade your license as soon as possible. In a future version, this limit will be enforced after a threshold is exceeded. The clients used were: c1, c2, c3, c4, c5, c6.");
    }

    private static class TestLicenses
    {
        // Redistribution licenses
        public const string RedistributionBusinessLicense =
            "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzMwNDE5MjAwLCJleHAiOjE3MzE2Mjg4MDAsImNvbXBhbnlfbmFtZSI6Il90ZXN0IiwiY29udGFjdF9pbmZvIjoiY29udGFjdEBkdWVuZGVzb2Z0d2FyZS5jb20iLCJlZGl0aW9uIjoiQnVzaW5lc3MiLCJpZCI6IjY2ODMiLCJmZWF0dXJlIjoiaXN2IiwicHJvZHVjdCI6IlRCRCJ9.rYDrY6UUKgZfnfx7GA1PILYj9XICIjC9aS06P8rUAuXYjxiagEIEkacKt3GcccJI6k0lMb6qbd3Hv-Q9rDDyDSxUZxwvGzVlhRrIditOI38FoN3trUd5RU6S7A_RSDd4uV0L1T8NKUKGlOvu8_7egcIy-E8q34GA5BNU2lV2Gsaa7yWAyTKZh7YPIP4y_TwLxOcw2GRn6dQq73-O_XaAIf0AxFowW1GsiBrirzE_TKwJ8VkbvN3O-yVT-ntPvoK0tHRKoG5yh8GPuDORQtlis_5bZHHFzazXVMul1rkYWSU9OhIdixvI44q1q1_5VGoGJ3SLFIFsdWM0ZvnPx7_Bqg";
    }

    public class MockLogger : ILogger
    {
        public static MockLogger Create() => new MockLogger(new LoggerExternalScopeProvider());
        public MockLogger(LoggerExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

        public readonly List<string> LogMessages = new();


        private readonly LoggerExternalScopeProvider _scopeProvider;


        public IDisposable BeginScope<TState>(TState state) where TState : notnull => _scopeProvider.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception, string> formatter) => LogMessages.Add(formatter(state, exception!));
    }

    public class MockLoggerProvider(MockLogger logger) : ILoggerProvider
    {
        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName) => logger;
    }

    [Fact]
    [Trait("Category", Category)]
    public void license_should_parse_company_data()
    {
        var subject = new IdentityServerLicense(
            new Claim("edition", "enterprise"),
            new Claim("company_name", "foo"),
            new Claim("contact_info", "bar"));
        subject.CompanyName.ShouldBe("foo");
        subject.ContactInfo.ShouldBe("bar");
    }

    [Fact]
    [Trait("Category", Category)]
    public void license_should_parse_expiration()
    {
        {
            var subject = new IdentityServerLicense(new Claim("edition", "enterprise"));
            subject.Expiration.ShouldBeNull();
        }

        {
            var exp = new DateTimeOffset(2020, 1, 12, 13, 5, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            var subject = new IdentityServerLicense(
                new Claim("edition", "enterprise"),
                new Claim("exp", exp.ToString()));
            subject.Expiration.ShouldBe(new DateTime(2020, 1, 12, 13, 5, 0, DateTimeKind.Utc));
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public void license_should_parse_edition_and_use_default_values()
    {
        // non-ISV
        {
            var subject = new IdentityServerLicense(new Claim("edition", "enterprise"));
            subject.Edition.ShouldBe(LicenseEdition.Enterprise);
            subject.IsEnterpriseEdition.ShouldBeTrue();
            subject.ClientLimit.ShouldBeNull();
            subject.IssuerLimit.ShouldBeNull();
            subject.KeyManagementFeature.ShouldBeTrue();
            subject.ResourceIsolationFeature.ShouldBeTrue();
            subject.DynamicProvidersFeature.ShouldBeTrue();
            subject.ServerSideSessionsFeature.ShouldBeTrue();
            //subject.ConfigApiFeature.ShouldBeTrue();
            subject.DPoPFeature.ShouldBeTrue();
            //subject.BffFeature.ShouldBeTrue();
            subject.RedistributionFeature.ShouldBeFalse();
            subject.CibaFeature.ShouldBeTrue();
            subject.ParFeature.ShouldBeTrue();
        }
        {
            var subject = new IdentityServerLicense(new Claim("edition", "business"));
            subject.Edition.ShouldBe(LicenseEdition.Business);
            subject.IsBusinessEdition.ShouldBeTrue();
            subject.ClientLimit.ShouldBe(15);
            subject.IssuerLimit.ShouldBe(1);
            subject.KeyManagementFeature.ShouldBeTrue();
            subject.ResourceIsolationFeature.ShouldBeFalse();
            subject.DynamicProvidersFeature.ShouldBeFalse();
            subject.ServerSideSessionsFeature.ShouldBeTrue();
            //subject.ConfigApiFeature.ShouldBeTrue();
            subject.DPoPFeature.ShouldBeFalse();
            //subject.BffFeature.ShouldBeTrue();
            subject.RedistributionFeature.ShouldBeFalse();
            subject.CibaFeature.ShouldBeFalse();
            subject.ParFeature.ShouldBeTrue();
        }
        {
            var subject = new IdentityServerLicense(new Claim("edition", "starter"));
            subject.Edition.ShouldBe(LicenseEdition.Starter);
            subject.IsStarterEdition.ShouldBeTrue();
            subject.ClientLimit.ShouldBe(5);
            subject.IssuerLimit.ShouldBe(1);
            subject.KeyManagementFeature.ShouldBeFalse();
            subject.ResourceIsolationFeature.ShouldBeFalse();
            subject.DynamicProvidersFeature.ShouldBeFalse();
            subject.ServerSideSessionsFeature.ShouldBeFalse();
            //subject.ConfigApiFeature.ShouldBeFalse();
            subject.DPoPFeature.ShouldBeFalse();
            //subject.BffFeature.ShouldBeFalse();
            subject.RedistributionFeature.ShouldBeFalse();
            subject.CibaFeature.ShouldBeFalse();
            subject.ParFeature.ShouldBeFalse();
        }
        {
            var subject = new IdentityServerLicense(new Claim("edition", "community"));
            subject.Edition.ShouldBe(LicenseEdition.Community);
            subject.IsCommunityEdition.ShouldBeTrue();
            subject.ClientLimit.ShouldBeNull();
            subject.IssuerLimit.ShouldBeNull();
            subject.KeyManagementFeature.ShouldBeTrue();
            subject.ResourceIsolationFeature.ShouldBeTrue();
            subject.DynamicProvidersFeature.ShouldBeTrue();
            subject.ServerSideSessionsFeature.ShouldBeTrue();
            //subject.ConfigApiFeature.ShouldBeTrue();
            subject.DPoPFeature.ShouldBeTrue();
            //subject.BffFeature.ShouldBeTrue();
            subject.RedistributionFeature.ShouldBeFalse();
            subject.CibaFeature.ShouldBeTrue();
            subject.ParFeature.ShouldBeTrue();
        }

        // BFF
        // TODO
        //{
        //    var subject = new IdentityServerLicense(new Claim("edition", "bff"));
        //    subject.Edition.ShouldBe(LicenseEdition.Bff);
        //    subject.IsBffEdition.ShouldBeTrue();
        //    subject.ServerSideSessionsFeature.ShouldBeFalse();
        //    //subject.ConfigApiFeature.ShouldBeFalse();
        //    subject.DPoPFeature.ShouldBeFalse();
        //    //subject.BffFeature.ShouldBeTrue();
        //    subject.ClientLimit.ShouldBe(0);
        //    subject.IssuerLimit.ShouldBe(0);
        //    subject.KeyManagementFeature.ShouldBeFalse();
        //    subject.ResourceIsolationFeature.ShouldBeFalse();
        //    subject.DynamicProvidersFeature.ShouldBeFalse();
        //    subject.RedistributionFeature.ShouldBeFalse();
        //    subject.CibaFeature.ShouldBeFalse();
        //}

        // ISV
        {
            var subject = new IdentityServerLicense(new Claim("edition", "enterprise"), new Claim("feature", "isv"));
            subject.Edition.ShouldBe(LicenseEdition.Enterprise);
            subject.IsEnterpriseEdition.ShouldBeTrue();
            subject.ClientLimit.ShouldBe(5);
            subject.IssuerLimit.ShouldBeNull();
            subject.KeyManagementFeature.ShouldBeTrue();
            subject.ResourceIsolationFeature.ShouldBeTrue();
            subject.DynamicProvidersFeature.ShouldBeTrue();
            subject.ServerSideSessionsFeature.ShouldBeTrue();
            //subject.ConfigApiFeature.ShouldBeTrue();
            subject.DPoPFeature.ShouldBeTrue();
            //subject.BffFeature.ShouldBeTrue();
            subject.RedistributionFeature.ShouldBeTrue();
            subject.CibaFeature.ShouldBeTrue();
        }
        {
            var subject = new IdentityServerLicense(new Claim("edition", "business"), new Claim("feature", "isv"));
            subject.Edition.ShouldBe(LicenseEdition.Business);
            subject.IsBusinessEdition.ShouldBeTrue();
            subject.ClientLimit.ShouldBe(5);
            subject.IssuerLimit.ShouldBe(1);
            subject.KeyManagementFeature.ShouldBeTrue();
            subject.ResourceIsolationFeature.ShouldBeFalse();
            subject.DynamicProvidersFeature.ShouldBeFalse();
            subject.ServerSideSessionsFeature.ShouldBeTrue();
            //subject.ConfigApiFeature.ShouldBeTrue();
            subject.DPoPFeature.ShouldBeFalse();
            //subject.BffFeature.ShouldBeTrue();
            subject.RedistributionFeature.ShouldBeTrue();
            subject.CibaFeature.ShouldBeFalse();
        }
        {
            var subject = new IdentityServerLicense(new Claim("edition", "starter"), new Claim("feature", "isv"));
            subject.Edition.ShouldBe(LicenseEdition.Starter);
            subject.IsStarterEdition.ShouldBeTrue();
            subject.ClientLimit.ShouldBe(5);
            subject.IssuerLimit.ShouldBe(1);
            subject.KeyManagementFeature.ShouldBeFalse();
            subject.ResourceIsolationFeature.ShouldBeFalse();
            subject.DynamicProvidersFeature.ShouldBeFalse();
            subject.ServerSideSessionsFeature.ShouldBeFalse();
            //subject.ConfigApiFeature.ShouldBeFalse();
            subject.DPoPFeature.ShouldBeFalse();
            //subject.BffFeature.ShouldBeFalse();
            subject.RedistributionFeature.ShouldBeTrue();
            subject.CibaFeature.ShouldBeFalse();
        }
        // TODO: these exceptions were moved to the validator
        //{
        //    Action a = () => new IdentityServerLicense(new Claim("edition", "community"), new Claim("feature", "isv"));
        //    a.ShouldThrow<Exception>();
        //}
        //{
        //    Action a = () => new IdentityServerLicense(new Claim("edition", "bff"), new Claim("feature", "isv"));
        //    a.ShouldThrow<Exception>();
        //}
    }

    [Fact]
    [Trait("Category", Category)]
    public void license_should_handle_overrides_for_default_edition_values()
    {
        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "enterprise"),
                new Claim("client_limit", "20"),
                new Claim("issuer_limit", "5"));
            subject.ClientLimit.ShouldBeNull();
            subject.IssuerLimit.ShouldBeNull();
        }

        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "business"),
                new Claim("client_limit", "20"),
                new Claim("issuer_limit", "5"),
                new Claim("feature", "resource_isolation"),
                new Claim("feature", "ciba"),
                new Claim("feature", "dynamic_providers"));
            subject.ClientLimit.ShouldBe(20);
            subject.IssuerLimit.ShouldBe(5);
            subject.ResourceIsolationFeature.ShouldBeTrue();
            subject.DynamicProvidersFeature.ShouldBeTrue();
            subject.CibaFeature.ShouldBeTrue();
        }
        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "business"),
                new Claim("client_limit", "20"),
                new Claim("feature", "unlimited_issuers"),
                new Claim("issuer_limit", "5"),
                new Claim("feature", "unlimited_clients"));
            subject.ClientLimit.ShouldBeNull();
            subject.IssuerLimit.ShouldBeNull();
        }

        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "starter"),
                new Claim("client_limit", "20"),
                new Claim("issuer_limit", "5"),
                new Claim("feature", "key_management"),
                new Claim("feature", "isv"),
                new Claim("feature", "resource_isolation"),
                new Claim("feature", "server_side_sessions"),
                new Claim("feature", "config_api"),
                new Claim("feature", "dpop"),
                new Claim("feature", "bff"),
                new Claim("feature", "ciba"),
                new Claim("feature", "dynamic_providers"),
                new Claim("feature", "par"));
            subject.ClientLimit.ShouldBe(20);
            subject.IssuerLimit.ShouldBe(5);
            subject.KeyManagementFeature.ShouldBeTrue();
            subject.ResourceIsolationFeature.ShouldBeTrue();
            subject.ServerSideSessionsFeature.ShouldBeTrue();
            //subject.ConfigApiFeature.ShouldBeTrue();
            subject.DPoPFeature.ShouldBeTrue();
            //subject.BffFeature.ShouldBeTrue();
            subject.DynamicProvidersFeature.ShouldBeTrue();
            subject.RedistributionFeature.ShouldBeTrue();
            subject.CibaFeature.ShouldBeTrue();
            subject.ParFeature.ShouldBeTrue();
        }
        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "starter"),
                new Claim("client_limit", "20"),
                new Claim("feature", "unlimited_issuers"),
                new Claim("issuer_limit", "5"),
                new Claim("feature", "unlimited_clients"));
            subject.ClientLimit.ShouldBeNull();
            subject.IssuerLimit.ShouldBeNull();
        }

        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "community"),
                new Claim("client_limit", "20"),
                new Claim("issuer_limit", "5"));
            subject.ClientLimit.ShouldBeNull();
            subject.IssuerLimit.ShouldBeNull();
        }

        // ISV
        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "enterprise"),
                new Claim("feature", "isv"),
                new Claim("client_limit", "20"));
            subject.ClientLimit.ShouldBe(20);
        }
        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "business"),
                new Claim("feature", "isv"),
                new Claim("feature", "ciba"),
                new Claim("client_limit", "20"));
            subject.ClientLimit.ShouldBe(20);
            subject.CibaFeature.ShouldBeTrue();
        }
        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "starter"),
                new Claim("feature", "isv"),
                new Claim("feature", "server_side_sessions"),
                new Claim("feature", "config_api"),
                new Claim("feature", "dpop"),
                new Claim("feature", "bff"),
                new Claim("feature", "ciba"),
                new Claim("client_limit", "20"));
            subject.ClientLimit.ShouldBe(20);
            subject.ServerSideSessionsFeature.ShouldBeTrue();
            //subject.ConfigApiFeature.ShouldBeTrue();
            subject.DPoPFeature.ShouldBeTrue();
            //subject.BffFeature.ShouldBeTrue();
            subject.CibaFeature.ShouldBeTrue();
        }

        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "enterprise"),
                new Claim("feature", "isv"),
                new Claim("feature", "unlimited_clients"),
                new Claim("client_limit", "20"));
            subject.ClientLimit.ShouldBeNull();
        }
        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "business"),
                new Claim("feature", "isv"),
                new Claim("feature", "unlimited_clients"),
                new Claim("client_limit", "20"));
            subject.ClientLimit.ShouldBeNull();
        }
        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "starter"),
                new Claim("feature", "isv"),
                new Claim("feature", "unlimited_clients"),
                new Claim("client_limit", "20"));
            subject.ClientLimit.ShouldBeNull();
        }

        // BFF
        // TODO: validate BFF initialize
        //{
        //    var subject = new IdentityServerLicense(
        //        new Claim("edition", "bff"),
        //        new Claim("client_limit", "20"),
        //        new Claim("issuer_limit", "10"),
        //        new Claim("feature", "resource_isolation"),
        //        new Claim("feature", "dynamic_providers"),
        //        new Claim("feature", "ciba"),
        //        new Claim("feature", "key_management")
        //    );
        //    //subject.BffFeature.ShouldBeTrue();
        //    subject.ClientLimit.ShouldBe(0);
        //    subject.IssuerLimit.ShouldBe(0);
        //    subject.KeyManagementFeature.ShouldBeFalse();
        //    subject.ResourceIsolationFeature.ShouldBeFalse();
        //    subject.DynamicProvidersFeature.ShouldBeFalse();
        //    subject.CibaFeature.ShouldBeFalse();
        //}
    }

    [Fact]
    [Trait("Category", Category)]
    public void invalid_edition_should_fail()
    {
        {
            Action func = () => new IdentityServerLicense(new Claim("edition", "invalid"));
            func.ShouldThrow<Exception>();
        }
        {
            Action func = () => new IdentityServerLicense(new Claim("edition", ""));
            func.ShouldThrow<Exception>();
        }
    }

    private class MockLicenseValidator : IdentityServerLicenseValidator
    {
        public MockLicenseValidator()
        {
            ErrorLog = (str, obj) => { ErrorLogCount++; };
            WarningLog = (str, obj) => { WarningLogCount++; };
        }

        public int ErrorLogCount { get; set; }
        public int WarningLogCount { get; set; }
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_count_exceeded_should_warn_for_redist_license()
    {
        var license = new IdentityServerLicense(new Claim("edition", "starter"), new Claim("feature", "redistribution"));
        var subject = new MockLicenseValidator();

        for (var i = 0; i < 5; i++)
        {
            subject.ValidateClient("client" + i, license);
        }

        // Adding the allowed number of clients shouldn't log.
        subject.ErrorLogCount.ShouldBe(0);
        subject.WarningLogCount.ShouldBe(0);

        // Validating same client again shouldn't log.
        subject.ValidateClient("client3", license);
        subject.ErrorLogCount.ShouldBe(0);
        subject.WarningLogCount.ShouldBe(0);

        subject.ValidateClient("extra1", license);
        subject.ValidateClient("extra2", license);

        subject.ErrorLogCount.ShouldBe(2);
        subject.WarningLogCount.ShouldBe(0);
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_count_exceeded_should_not_warn_for_non_redist_license()
    {
        var license = new IdentityServerLicense(new Claim("edition", "starter"));
        var subject = new MockLicenseValidator();

        for (var i = 0; i < 5; i++)
        {
            subject.ValidateClient("client" + i, license);
        }

        // Adding the allowed number of clients shouldn't log.
        subject.ErrorLogCount.ShouldBe(0);
        subject.WarningLogCount.ShouldBe(0);

        // Validating same client again shouldn't log.
        subject.ValidateClient("client3", license);
        subject.ErrorLogCount.ShouldBe(0);
        subject.WarningLogCount.ShouldBe(0);

        subject.ValidateClient("extra1", license);
        subject.ValidateClient("extra2", license);

        subject.ErrorLogCount.ShouldBe(0);
        subject.WarningLogCount.ShouldBe(0);
    }

    [Fact]
    [Trait("Category", Category)]
    public void issuer_count_exceeded_should_warn_for_redist_license()
    {
        var license = new IdentityServerLicense(new Claim("edition", "starter"), new Claim("feature", "redistribution"));
        var subject = new MockLicenseValidator();

        subject.ValidateIssuer("issuer", license);

        // Adding the allowed number of issuers shouldn't log.
        subject.ErrorLogCount.ShouldBe(0);
        subject.WarningLogCount.ShouldBe(0);

        // Validating same issuer again shouldn't log.
        subject.ValidateIssuer("issuer", license);
        subject.ErrorLogCount.ShouldBe(0);
        subject.WarningLogCount.ShouldBe(0);

        subject.ValidateIssuer("extra1", license);
        subject.ValidateIssuer("extra2", license);

        subject.ErrorLogCount.ShouldBe(2);
        subject.WarningLogCount.ShouldBe(0);
    }

    [Fact]
    [Trait("Category", Category)]
    public void issuer_count_exceeded_should_not_warn_for_non_redist_license()
    {
        var license = new IdentityServerLicense(new Claim("edition", "starter"));
        var subject = new MockLicenseValidator();

        subject.ValidateIssuer("issuer", license);

        // Adding the allowed number of issuers shouldn't log.
        subject.ErrorLogCount.ShouldBe(0);
        subject.WarningLogCount.ShouldBe(0);

        // Validating same issuer again shouldn't log.
        subject.ValidateClient("issuer", license);
        subject.ErrorLogCount.ShouldBe(0);
        subject.WarningLogCount.ShouldBe(0);

        subject.ValidateIssuer("extra1", license);
        subject.ValidateIssuer("extra2", license);

        subject.ErrorLogCount.ShouldBe(0);
        subject.WarningLogCount.ShouldBe(0);
    }
}
