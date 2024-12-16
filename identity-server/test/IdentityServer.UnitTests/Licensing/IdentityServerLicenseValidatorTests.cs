// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using FluentAssertions;
using System;
using System.Security.Claims;
using Xunit;
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
        subject.CompanyName.Should().Be("foo");
        subject.ContactInfo.Should().Be("bar");
    }

    [Fact]
    [Trait("Category", Category)]
    public void license_should_parse_expiration()
    {
        {
            var subject = new IdentityServerLicense(new Claim("edition", "enterprise"));
            subject.Expiration.Should().BeNull();
        }

        {
            var exp = new DateTimeOffset(2020, 1, 12, 13, 5, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            var subject = new IdentityServerLicense(
                new Claim("edition", "enterprise"),
                new Claim("exp", exp.ToString()));
            subject.Expiration.Should().Be(new DateTime(2020, 1, 12, 13, 5, 0, DateTimeKind.Utc));
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public void license_should_parse_edition_and_use_default_values()
    {
        // non-ISV
        {
            var subject = new IdentityServerLicense(new Claim("edition", "enterprise"));
            subject.Edition.Should().Be(LicenseEdition.Enterprise);
            subject.IsEnterpriseEdition.Should().BeTrue();
            subject.ClientLimit.Should().BeNull();
            subject.IssuerLimit.Should().BeNull();
            subject.KeyManagementFeature.Should().BeTrue();
            subject.ResourceIsolationFeature.Should().BeTrue();
            subject.DynamicProvidersFeature.Should().BeTrue();
            subject.ServerSideSessionsFeature.Should().BeTrue();
            //subject.ConfigApiFeature.Should().BeTrue();
            subject.DPoPFeature.Should().BeTrue();
            //subject.BffFeature.Should().BeTrue();
            subject.RedistributionFeature.Should().BeFalse();
            subject.CibaFeature.Should().BeTrue();
            subject.ParFeature.Should().BeTrue();
        }
        {
            var subject = new IdentityServerLicense(new Claim("edition", "business"));
            subject.Edition.Should().Be(LicenseEdition.Business);
            subject.IsBusinessEdition.Should().BeTrue();
            subject.ClientLimit.Should().Be(15);
            subject.IssuerLimit.Should().Be(1);
            subject.KeyManagementFeature.Should().BeTrue();
            subject.ResourceIsolationFeature.Should().BeFalse();
            subject.DynamicProvidersFeature.Should().BeFalse();
            subject.ServerSideSessionsFeature.Should().BeTrue();
            //subject.ConfigApiFeature.Should().BeTrue();
            subject.DPoPFeature.Should().BeFalse();
            //subject.BffFeature.Should().BeTrue();
            subject.RedistributionFeature.Should().BeFalse();
            subject.CibaFeature.Should().BeFalse();
            subject.ParFeature.Should().BeTrue();
        }
        {
            var subject = new IdentityServerLicense(new Claim("edition", "starter"));
            subject.Edition.Should().Be(LicenseEdition.Starter);
            subject.IsStarterEdition.Should().BeTrue();
            subject.ClientLimit.Should().Be(5);
            subject.IssuerLimit.Should().Be(1);
            subject.KeyManagementFeature.Should().BeFalse();
            subject.ResourceIsolationFeature.Should().BeFalse();
            subject.DynamicProvidersFeature.Should().BeFalse();
            subject.ServerSideSessionsFeature.Should().BeFalse();
            //subject.ConfigApiFeature.Should().BeFalse();
            subject.DPoPFeature.Should().BeFalse();
            //subject.BffFeature.Should().BeFalse();
            subject.RedistributionFeature.Should().BeFalse();
            subject.CibaFeature.Should().BeFalse();
            subject.ParFeature.Should().BeFalse();
        }
        {
            var subject = new IdentityServerLicense(new Claim("edition", "community"));
            subject.Edition.Should().Be(LicenseEdition.Community);
            subject.IsCommunityEdition.Should().BeTrue();
            subject.ClientLimit.Should().BeNull();
            subject.IssuerLimit.Should().BeNull();
            subject.KeyManagementFeature.Should().BeTrue();
            subject.ResourceIsolationFeature.Should().BeTrue();
            subject.DynamicProvidersFeature.Should().BeTrue();
            subject.ServerSideSessionsFeature.Should().BeTrue();
            //subject.ConfigApiFeature.Should().BeTrue();
            subject.DPoPFeature.Should().BeTrue();
            //subject.BffFeature.Should().BeTrue();
            subject.RedistributionFeature.Should().BeFalse();
            subject.CibaFeature.Should().BeTrue();
            subject.ParFeature.Should().BeTrue();
        }

        // BFF
        // TODO
        //{
        //    var subject = new IdentityServerLicense(new Claim("edition", "bff"));
        //    subject.Edition.Should().Be(LicenseEdition.Bff);
        //    subject.IsBffEdition.Should().BeTrue();
        //    subject.ServerSideSessionsFeature.Should().BeFalse();
        //    //subject.ConfigApiFeature.Should().BeFalse();
        //    subject.DPoPFeature.Should().BeFalse();
        //    //subject.BffFeature.Should().BeTrue();
        //    subject.ClientLimit.Should().Be(0);
        //    subject.IssuerLimit.Should().Be(0);
        //    subject.KeyManagementFeature.Should().BeFalse();
        //    subject.ResourceIsolationFeature.Should().BeFalse();
        //    subject.DynamicProvidersFeature.Should().BeFalse();
        //    subject.RedistributionFeature.Should().BeFalse();
        //    subject.CibaFeature.Should().BeFalse();
        //}

        // ISV
        {
            var subject = new IdentityServerLicense(new Claim("edition", "enterprise"), new Claim("feature", "isv"));
            subject.Edition.Should().Be(LicenseEdition.Enterprise);
            subject.IsEnterpriseEdition.Should().BeTrue();
            subject.ClientLimit.Should().Be(5);
            subject.IssuerLimit.Should().BeNull();
            subject.KeyManagementFeature.Should().BeTrue();
            subject.ResourceIsolationFeature.Should().BeTrue();
            subject.DynamicProvidersFeature.Should().BeTrue();
            subject.ServerSideSessionsFeature.Should().BeTrue();
            //subject.ConfigApiFeature.Should().BeTrue();
            subject.DPoPFeature.Should().BeTrue();
            //subject.BffFeature.Should().BeTrue();
            subject.RedistributionFeature.Should().BeTrue();
            subject.CibaFeature.Should().BeTrue();
        }
        {
            var subject = new IdentityServerLicense(new Claim("edition", "business"), new Claim("feature", "isv"));
            subject.Edition.Should().Be(LicenseEdition.Business);
            subject.IsBusinessEdition.Should().BeTrue();
            subject.ClientLimit.Should().Be(5);
            subject.IssuerLimit.Should().Be(1);
            subject.KeyManagementFeature.Should().BeTrue();
            subject.ResourceIsolationFeature.Should().BeFalse();
            subject.DynamicProvidersFeature.Should().BeFalse();
            subject.ServerSideSessionsFeature.Should().BeTrue();
            //subject.ConfigApiFeature.Should().BeTrue();
            subject.DPoPFeature.Should().BeFalse();
            //subject.BffFeature.Should().BeTrue();
            subject.RedistributionFeature.Should().BeTrue();
            subject.CibaFeature.Should().BeFalse();
        }
        {
            var subject = new IdentityServerLicense(new Claim("edition", "starter"), new Claim("feature", "isv"));
            subject.Edition.Should().Be(LicenseEdition.Starter);
            subject.IsStarterEdition.Should().BeTrue();
            subject.ClientLimit.Should().Be(5);
            subject.IssuerLimit.Should().Be(1);
            subject.KeyManagementFeature.Should().BeFalse();
            subject.ResourceIsolationFeature.Should().BeFalse();
            subject.DynamicProvidersFeature.Should().BeFalse();
            subject.ServerSideSessionsFeature.Should().BeFalse();
            //subject.ConfigApiFeature.Should().BeFalse();
            subject.DPoPFeature.Should().BeFalse();
            //subject.BffFeature.Should().BeFalse();
            subject.RedistributionFeature.Should().BeTrue();
            subject.CibaFeature.Should().BeFalse();
        }
        // TODO: these exceptions were moved to the validator
        //{
        //    Action a = () => new IdentityServerLicense(new Claim("edition", "community"), new Claim("feature", "isv"));
        //    a.Should().Throw<Exception>();
        //}
        //{
        //    Action a = () => new IdentityServerLicense(new Claim("edition", "bff"), new Claim("feature", "isv"));
        //    a.Should().Throw<Exception>();
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
            subject.ClientLimit.Should().BeNull();
            subject.IssuerLimit.Should().BeNull();
        }

        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "business"),
                new Claim("client_limit", "20"),
                new Claim("issuer_limit", "5"),
                new Claim("feature", "resource_isolation"),
                new Claim("feature", "ciba"),
                new Claim("feature", "dynamic_providers"));
            subject.ClientLimit.Should().Be(20);
            subject.IssuerLimit.Should().Be(5);
            subject.ResourceIsolationFeature.Should().BeTrue();
            subject.DynamicProvidersFeature.Should().BeTrue();
            subject.CibaFeature.Should().BeTrue();
        }
        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "business"),
                new Claim("client_limit", "20"),
                new Claim("feature", "unlimited_issuers"),
                new Claim("issuer_limit", "5"),
                new Claim("feature", "unlimited_clients"));
            subject.ClientLimit.Should().BeNull();
            subject.IssuerLimit.Should().BeNull();
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
            subject.ClientLimit.Should().Be(20);
            subject.IssuerLimit.Should().Be(5);
            subject.KeyManagementFeature.Should().BeTrue();
            subject.ResourceIsolationFeature.Should().BeTrue();
            subject.ServerSideSessionsFeature.Should().BeTrue();
            //subject.ConfigApiFeature.Should().BeTrue();
            subject.DPoPFeature.Should().BeTrue();
            //subject.BffFeature.Should().BeTrue();
            subject.DynamicProvidersFeature.Should().BeTrue();
            subject.RedistributionFeature.Should().BeTrue();
            subject.CibaFeature.Should().BeTrue();
            subject.ParFeature.Should().BeTrue();
        }
        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "starter"),
                new Claim("client_limit", "20"),
                new Claim("feature", "unlimited_issuers"),
                new Claim("issuer_limit", "5"),
                new Claim("feature", "unlimited_clients"));
            subject.ClientLimit.Should().BeNull();
            subject.IssuerLimit.Should().BeNull();
        }

        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "community"),
                new Claim("client_limit", "20"),
                new Claim("issuer_limit", "5"));
            subject.ClientLimit.Should().BeNull();
            subject.IssuerLimit.Should().BeNull();
        }

        // ISV
        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "enterprise"),
                new Claim("feature", "isv"),
                new Claim("client_limit", "20"));
            subject.ClientLimit.Should().Be(20);
        }
        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "business"),
                new Claim("feature", "isv"),
                new Claim("feature", "ciba"),
                new Claim("client_limit", "20"));
            subject.ClientLimit.Should().Be(20);
            subject.CibaFeature.Should().BeTrue();
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
            subject.ClientLimit.Should().Be(20);
            subject.ServerSideSessionsFeature.Should().BeTrue();
            //subject.ConfigApiFeature.Should().BeTrue();
            subject.DPoPFeature.Should().BeTrue();
            //subject.BffFeature.Should().BeTrue();
            subject.CibaFeature.Should().BeTrue();
        }

        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "enterprise"),
                new Claim("feature", "isv"),
                new Claim("feature", "unlimited_clients"),
                new Claim("client_limit", "20"));
            subject.ClientLimit.Should().BeNull();
        }
        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "business"),
                new Claim("feature", "isv"),
                new Claim("feature", "unlimited_clients"),
                new Claim("client_limit", "20"));
            subject.ClientLimit.Should().BeNull();
        }
        {
            var subject = new IdentityServerLicense(
                new Claim("edition", "starter"),
                new Claim("feature", "isv"),
                new Claim("feature", "unlimited_clients"),
                new Claim("client_limit", "20"));
            subject.ClientLimit.Should().BeNull();
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
        //    //subject.BffFeature.Should().BeTrue();
        //    subject.ClientLimit.Should().Be(0);
        //    subject.IssuerLimit.Should().Be(0);
        //    subject.KeyManagementFeature.Should().BeFalse();
        //    subject.ResourceIsolationFeature.Should().BeFalse();
        //    subject.DynamicProvidersFeature.Should().BeFalse();
        //    subject.CibaFeature.Should().BeFalse();
        //}
    }

    [Fact]
    [Trait("Category", Category)]
    public void invalid_edition_should_fail()
    {
        {
            Action func = () => new IdentityServerLicense(new Claim("edition", "invalid"));
            func.Should().Throw<Exception>();
        }
        {
            Action func = () => new IdentityServerLicense(new Claim("edition", ""));
            func.Should().Throw<Exception>();
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

    [Theory]
    [Trait("Category", Category)]
    [InlineData(false, 5)]
    [InlineData(true, 15)]
    public void client_count_exceeded_should_warn(bool hasLicense, int allowedClients)
    {
        var license = hasLicense ? new IdentityServerLicense(new Claim("edition", "business")) : null;
        var subject = new MockLicenseValidator();

        for (int i = 0; i < allowedClients; i++)
        {
            subject.ValidateClient("client" + i, license);
        }

        // Adding the allowed number of clients shouldn't log.
        subject.ErrorLogCount.Should().Be(0);
        subject.WarningLogCount.Should().Be(0);

        // Validating same client again shouldn't log.
        subject.ValidateClient("client3", license);
        subject.ErrorLogCount.Should().Be(0);
        subject.WarningLogCount.Should().Be(0);

        subject.ValidateClient("extra1", license);
        subject.ValidateClient("extra2", license);

        if (hasLicense)
        {
            subject.ErrorLogCount.Should().Be(2);
            subject.WarningLogCount.Should().Be(0);
        }
        else
        {
            subject.ErrorLogCount.Should().Be(0);
            subject.WarningLogCount.Should().Be(1);
        }
    }
}
