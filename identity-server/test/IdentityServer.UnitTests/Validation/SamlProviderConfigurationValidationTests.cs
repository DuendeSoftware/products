// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Internal.Saml.Sp.AspNetCore;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace UnitTests.Validation;

public class SamlProviderConfigurationValidationTests
{
    private const string Category = "SamlProvider Configuration Validation Tests";
    private readonly IIdentityProviderConfigurationValidator _validator;

    public SamlProviderConfigurationValidationTests()
    {
        var options = new IdentityServerOptions();
        options.DynamicProviders.AddProviderType<Saml2Handler, Saml2Options, SamlProvider>("saml");
        _validator = new DefaultIdentityProviderConfigurationValidator(options);
    }

    private static SamlProvider ValidProvider() => new()
    {
        Scheme = "test-saml",
        IdpEntityId = "https://idp.example.com",
        SingleSignOnServiceUrl = "https://idp.example.com/sso",
        BindingType = "redirect",
    };

    private static string CreateSelfSignedCertBase64()
    {
        using var key = RSA.Create(2048);
        var req = new CertificateRequest("CN=Test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
        return Convert.ToBase64String(cert.Export(X509ContentType.Cert));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_saml_provider_should_succeed()
    {
        var ctx = new IdentityProviderConfigurationValidationContext(ValidProvider());
        await _validator.ValidateAsync(ctx, default);
        ctx.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_scheme_should_fail()
    {
        var provider = ValidProvider();
        provider.Scheme = "";

        var ctx = new IdentityProviderConfigurationValidationContext(provider);
        await _validator.ValidateAsync(ctx, default);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ToLowerInvariant().ShouldContain("scheme");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_idp_entity_id_should_fail()
    {
        var provider = ValidProvider();
        provider.IdpEntityId = null;

        var ctx = new IdentityProviderConfigurationValidationContext(provider);
        await _validator.ValidateAsync(ctx, default);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ShouldContain("IdpEntityId");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_sso_url_should_fail()
    {
        var provider = ValidProvider();
        provider.SingleSignOnServiceUrl = null;

        var ctx = new IdentityProviderConfigurationValidationContext(provider);
        await _validator.ValidateAsync(ctx, default);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ShouldContain("SingleSignOnServiceUrl");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_sso_url_should_fail()
    {
        var provider = ValidProvider();
        provider.SingleSignOnServiceUrl = "not-a-url";

        var ctx = new IdentityProviderConfigurationValidationContext(provider);
        await _validator.ValidateAsync(ctx, default);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ShouldContain("SingleSignOnServiceUrl");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_slo_url_should_fail()
    {
        var provider = ValidProvider();
        provider.SingleLogoutServiceUrl = "not-a-url";

        var ctx = new IdentityProviderConfigurationValidationContext(provider);
        await _validator.ValidateAsync(ctx, default);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ShouldContain("SingleLogoutServiceUrl");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_slo_url_should_succeed()
    {
        var provider = ValidProvider();
        provider.SingleLogoutServiceUrl = "https://idp.example.com/slo";

        var ctx = new IdentityProviderConfigurationValidationContext(provider);
        await _validator.ValidateAsync(ctx, default);

        ctx.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_signing_cert_base64_should_fail()
    {
        var provider = ValidProvider();
        provider.SigningCertificateBase64 = "not-valid-base64!!!";

        var ctx = new IdentityProviderConfigurationValidationContext(provider);
        await _validator.ValidateAsync(ctx, default);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ShouldContain("SigningCertificateBase64");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_signing_cert_base64_should_succeed()
    {
        var provider = ValidProvider();
        provider.SigningCertificateBase64 = CreateSelfSignedCertBase64();

        var ctx = new IdentityProviderConfigurationValidationContext(provider);
        await _validator.ValidateAsync(ctx, default);

        ctx.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_binding_type_should_fail()
    {
        var provider = ValidProvider();
        provider.BindingType = "soap";

        var ctx = new IdentityProviderConfigurationValidationContext(provider);
        await _validator.ValidateAsync(ctx, default);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ShouldContain("BindingType");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task post_binding_type_should_succeed()
    {
        var provider = ValidProvider();
        provider.BindingType = "post";

        var ctx = new IdentityProviderConfigurationValidationContext(provider);
        await _validator.ValidateAsync(ctx, default);

        ctx.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task redirect_binding_type_should_succeed()
    {
        var provider = ValidProvider();
        provider.BindingType = "redirect";

        var ctx = new IdentityProviderConfigurationValidationContext(provider);
        await _validator.ValidateAsync(ctx, default);

        ctx.IsValid.ShouldBeTrue();
    }
}
