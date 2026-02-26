// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace UnitTests.Validation;

public class DefaultSamlServiceProviderConfigurationValidatorTests
{
    private readonly DefaultSamlServiceProviderConfigurationValidator _validator = new();

    private static SamlServiceProvider ValidSp() => new()
    {
        EntityId = "https://sp.example.com",
        AssertionConsumerServiceUrls = new HashSet<Uri> { new Uri("https://sp.example.com/acs") }
    };

    [Fact]
    public async Task Valid_sp_should_pass_validation()
    {
        var sp = ValidSp();
        var ctx = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Missing_entity_id_should_fail()
    {
        var sp = ValidSp();
        sp.EntityId = "";
        var ctx = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ShouldBe("EntityId is required.");
    }

    [Fact]
    public async Task Whitespace_entity_id_should_fail()
    {
        var sp = ValidSp();
        sp.EntityId = "   ";
        var ctx = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ShouldBe("EntityId is required.");
    }

    // Empty/null ACS URLs are NOT a configuration error — the endpoint handlers
    // are responsible for returning specific errors when no ACS URLs are configured.
    [Fact]
    public async Task Empty_acs_urls_should_pass()
    {
        var sp = ValidSp();
        sp.AssertionConsumerServiceUrls = new HashSet<Uri>();
        var ctx = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Null_acs_urls_should_pass()
    {
        var sp = ValidSp();
        sp.AssertionConsumerServiceUrls = null!;
        var ctx = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Http_acs_url_should_fail()
    {
        var sp = ValidSp();
        sp.AssertionConsumerServiceUrls = new HashSet<Uri> { new Uri("http://sp.example.com/acs") };
        var ctx = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ShouldBe("Assertion Consumer Service URL 'http://sp.example.com/acs' does not use HTTPS scheme.");
    }

    [Fact]
    public async Task Http_slo_url_should_fail()
    {
        var sp = ValidSp();
        sp.SingleLogoutServiceUrl = new SamlEndpointType
        {
            Location = new Uri("http://sp.example.com/slo"),
            Binding = SamlBinding.HttpPost
        };
        var ctx = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ShouldBe("Single Logout Service URL 'http://sp.example.com/slo' does not use HTTPS scheme.");
    }

    [Fact]
    public async Task Https_slo_url_should_pass()
    {
        var sp = ValidSp();
        sp.SingleLogoutServiceUrl = new SamlEndpointType
        {
            Location = new Uri("https://sp.example.com/slo"),
            Binding = SamlBinding.HttpPost
        };
        var ctx = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Null_slo_url_should_pass()
    {
        var sp = ValidSp();
        sp.SingleLogoutServiceUrl = null;
        var ctx = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeTrue();
    }

    // RequireSignedAuthnRequests without signing certs is NOT a configuration error —
    // the endpoint handlers are responsible for giving specific errors in this case.
    [Fact]
    public async Task RequireSignedAuthnRequests_without_signing_certs_should_pass()
    {
        var sp = ValidSp();
        sp.RequireSignedAuthnRequests = true;
        sp.SigningCertificates = null;
        var ctx = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task RequireSignedAuthnRequests_with_empty_signing_certs_should_pass()
    {
        var sp = ValidSp();
        sp.RequireSignedAuthnRequests = true;
        sp.SigningCertificates = new List<System.Security.Cryptography.X509Certificates.X509Certificate2>();
        var ctx = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task RequireSignedAuthnRequests_false_without_certs_should_pass()
    {
        var sp = ValidSp();
        sp.RequireSignedAuthnRequests = false;
        sp.SigningCertificates = null;
        var ctx = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EncryptAssertions_without_encryption_certs_should_fail()
    {
        var sp = ValidSp();
        sp.EncryptAssertions = true;
        sp.EncryptionCertificates = null;
        var ctx = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ShouldBe("Encryption certificates are required when EncryptAssertions is true.");
    }

    [Fact]
    public async Task EncryptAssertions_false_without_certs_should_pass()
    {
        var sp = ValidSp();
        sp.EncryptAssertions = false;
        sp.EncryptionCertificates = null;
        var ctx = new SamlServiceProviderConfigurationValidationContext(sp);

        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeTrue();
    }
}
