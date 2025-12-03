// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer;
using static Duende.License;

namespace UnitTests.Licensing;

public class IdentityServerLicenseValidatorTests
{
    private const string Category = "License validator tests";

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
        public MockLicenseValidator() : base(TimeProvider.System)
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
